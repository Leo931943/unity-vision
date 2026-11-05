using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private static readonly Vector3 SpawnPosition = new Vector3(0f, 0.02f, -8f);
    [Header("Model Alignment")]
    [Tooltip("Visual offset for model to match ground plane")]
    public Vector3 modelOffset = Vector3.zero;
    [Header("Movement Speeds")]
    [Tooltip("Walking speed in m/s")]
    public float walkSpeed = 4.0f;
    [Tooltip("Sprint / running speed in m/s")]
    public float runSpeed = 9.0f;
    [Tooltip("Acceleration and deceleration smoothing")]
    public float speedSmoothTime = 0.1f;
    [HideInInspector] public float possessionSpeedMultiplier = 1f;
    [Tooltip("Rotation turn smoothing")]
    public float turnSmoothSpeed = 15f;

    [Header("Jumping & Gravity")]
    [Tooltip("Jump velocity in m/s")]
    public float jumpVelocity = 6.5f;
    [Tooltip("Gravity acceleration")]
    public float gravity = -20.0f;
    [Tooltip("Airborne control multiplier")]
    [Range(0f, 1f)]
    public float airControl = 0.6f;

    [Header("Soccer Ball Physics Interaction")]
    [Tooltip("Maximum one-touch impulse applied to the soccer ball")]
    [Min(0f)] public float kickForce = 1.4f;
    [Tooltip("Minimum delay between touches of the same ball")]
    [Min(0.01f)] public float ballTouchCooldown = 0.12f;
    [Tooltip("How far from the player a ball can be controlled or kicked")]
    [Min(0.1f)] public float ballControlRange = 1.35f;
    [Tooltip("Impulse used for an E-key power shot")]
    [Min(0f)] public float powerShotForce = 7.5f;
    [Min(0f)] public float powerShotCooldown = 3f;
    [Header("Foot Ball Contact")]
    [Tooltip("Size of the invisible physical touch zone in front of the feet")]
    public Vector3 footTouchZoneSize = new Vector3(1.12f, 0.4f, 0.82f);
    [Tooltip("Local position of the invisible physical touch zone")]
    public Vector3 footTouchZoneOffset = new Vector3(0f, 0.28f, 0.34f);
    [Tooltip("Extra impulse from the foot touch zone when moving quickly")]
    [Min(0f)] public float footTouchKickForce = 0.9f;
    [Tooltip("Minimum delay between repeated touches from the invisible foot zone")]
    [Min(0.01f)] public float footTouchCooldown = 0.055f;
    [Header("Ball Challenges")]
    [Tooltip("Extra reach while sprinting to challenge an AI-carried ball")]
    [Min(0.1f)] public float challengeRange = 1.65f;
    [Tooltip("Impulse that knocks the ball clear during a close challenge")]
    [Min(0f)] public float challengeKickForce = 1.7f;
    [Min(0.05f)] public float challengeCooldown = 0.18f;
    [Header("Power Shot Animation")]
    [Min(0.1f)] public float powerShotAnimationDuration = 0.58f;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    // Components
    private CharacterController controller;
    private float currentSpeed;
    private float speedSmoothVelocity;
    private float verticalVelocity;
    private bool isGrounded;
    private bool jumpRequested;
    private bool isJumping;
    private Rigidbody lastTouchedBall;
    private float lastBallTouchTime;
    private float nextChallengeTime;
    private float powerShotAnimationTime = -1f;
    private float nextPowerShotTime;
    private Transform rightUpperLeg;
    private Transform rightLowerLeg;
    private Transform rightFoot;
    private Quaternion rightUpperLegBaseRotation;
    private Quaternion rightLowerLegBaseRotation;
    private Quaternion rightFootBaseRotation;

    // Status properties for HUD
    public float CurrentSpeed => currentSpeed;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting { get; private set; }
    public float PowerShotCooldownRemaining => Mathf.Max(0f, nextPowerShotTime - Time.time);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.skinWidth = 0.02f;
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0, 0.85f, 0);
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Movement is controlled by CharacterController.  Letting a humanoid
        // clip also apply root motion offsets the visible mesh and made it sink
        // into the pitch while idle.
        if (animator != null)
            animator.applyRootMotion = false;

        // The CharacterController's origin sits at ground level, while this FBX
        // is authored around the hips.  Keep the rendered mesh above that origin
        // so only its feet, rather than its torso, meet the pitch.
        Transform model = transform.Find("Meshy_Model");
        if (model != null)
        {
            model.localPosition = modelOffset;
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        CreateFootTouchZone();
    }

    void Update()
    {
        ReadInput(out Vector2 moveInput, out bool sprintInput, out bool jumpInput, out bool resetInput, out bool powerShotInput);

        if (resetInput)
        {
            ResetPosition();
            return;
        }

        if (jumpInput && controller.isGrounded)
        {
            jumpRequested = true;
        }

        IsSprinting = sprintInput && moveInput.sqrMagnitude > 0.05f;
        float targetSpeed = (IsSprinting ? runSpeed : walkSpeed) * moveInput.magnitude * possessionSpeedMultiplier;

        // Grounded check
        isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Small downward velocity to keep stick to slopes
            isJumping = false;
        }

        // Smooth speed transition
        float smoothTime = isGrounded ? speedSmoothTime : (speedSmoothTime / Mathf.Max(0.1f, airControl));
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, smoothTime);

        // Direction relative to camera
        Vector3 moveDir = Vector3.zero;
        if (cameraTransform != null)
        {
            Vector3 camForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized;
            moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            moveDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        }

        // Rotate character towards moving direction
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSmoothSpeed * Time.deltaTime);
        }

        // Jump execution
        if (jumpRequested)
        {
            verticalVelocity = jumpVelocity;
            jumpRequested = false;
            isJumping = true;
        }

        // Apply gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Sweep the invisible foot contact volume before the controller moves.
        // A CharacterController can otherwise cross a small ball between frames
        // at sprint speed without reporting a collision.
        SweepFootTouchZone(moveDir);
        TryChallengeOpponentBall(moveDir);

        // Final motion
        Vector3 velocity = moveDir * currentSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (powerShotInput)
            TryPowerShot(moveDir);

        // Update Animator parameters if present
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Pass Speed and Ground state
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsJumping", isJumping || (!isGrounded && verticalVelocity > 1.0f));
        animator.SetFloat("VerticalVelocity", verticalVelocity);

        // Adjust animation playback speed for walk vs run
        float motionSpeed = 1.0f;
        if (currentSpeed > 0.1f)
        {
            if (IsSprinting)
            {
                // Run playback speed remains untouched (1.25x at runSpeed as requested)
                motionSpeed = Mathf.Lerp(1.0f, 1.25f, Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, runSpeed)));
            }
            else
            {
                // Walk playback speed scaled dynamically to eliminate slow/fake foot sliding
                motionSpeed = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, walkSpeed)));
            }
        }
        animator.SetFloat("MotionSpeed", motionSpeed);
    }

    private void LateUpdate()
    {
        if (powerShotAnimationTime < 0f)
            return;

        powerShotAnimationTime += Time.deltaTime;
        float t = Mathf.Clamp01(powerShotAnimationTime / powerShotAnimationDuration);
        if (t >= 1f)
        {
            RestorePowerShotPose();
            powerShotAnimationTime = -1f;
            return;
        }

        // Wind the kicking leg back, strike through, then recover. Applying this
        // after Animator evaluation keeps the current body pose and never moves
        // the hips/root below the ground.
        float upperLegAngle = t < 0.26f
            ? Mathf.Lerp(0f, 24f, t / 0.26f)
            : t < 0.58f
                ? Mathf.Lerp(24f, -58f, (t - 0.26f) / 0.32f)
                : Mathf.Lerp(-58f, 0f, (t - 0.58f) / 0.42f);
        float lowerLegAngle = t < 0.30f
            ? Mathf.Lerp(0f, 14f, t / 0.30f)
            : t < 0.58f
                ? Mathf.Lerp(14f, -42f, (t - 0.30f) / 0.28f)
                : Mathf.Lerp(-42f, 0f, (t - 0.58f) / 0.42f);

        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = rightUpperLegBaseRotation * Quaternion.AngleAxis(upperLegAngle, Vector3.right);
        if (rightLowerLeg != null)
            rightLowerLeg.localRotation = rightLowerLegBaseRotation * Quaternion.AngleAxis(lowerLegAngle, Vector3.right);
        if (rightFoot != null)
            rightFoot.localRotation = rightFootBaseRotation * Quaternion.AngleAxis(-upperLegAngle * 0.35f, Vector3.right);
    }

    private void ReadInput(out Vector2 moveInput, out bool sprint, out bool jump, out bool reset, out bool powerShot)
    {
        moveInput = Vector2.zero;
        sprint = false;
        jump = false;
        reset = false;
        powerShot = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float x = 0;
            float y = 0;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1;
            moveInput = new Vector2(x, y);

            sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            powerShot = kb.eKey.wasPressedThisFrame;
            jump = kb.spaceKey.wasPressedThisFrame;
            reset = kb.rKey.wasPressedThisFrame;
        }
#else
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        powerShot = Input.GetKeyDown(KeyCode.E);
        jump = Input.GetKeyDown(KeyCode.Space);
        reset = Input.GetKeyDown(KeyCode.R);
#endif

        if (moveInput.sqrMagnitude > 1.0f)
            moveInput.Normalize();
    }

    public void ResetPosition()
    {
        controller.enabled = false;
        transform.position = SpawnPosition;
        transform.rotation = Quaternion.identity;
        verticalVelocity = 0;
        currentSpeed = 0;
        isJumping = false;
        controller.enabled = true;
    }

    public void SetMatchSpawn(Vector3 position, Vector3 facing)
    {
        controller.enabled = false;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        verticalVelocity = 0f;
        currentSpeed = 0f;
        speedSmoothVelocity = 0f;
        controller.enabled = true;
    }

    public void SetPossessionSlowdown(bool hasPossession)
    {
        // A meaningful pace gap is needed for the chasing player to catch a
        // dribbler. Keep it modest enough that carrying the ball still feels
        // responsive, but not so small that a straight run is unbeatable.
        possessionSpeedMultiplier = hasPossession ? 0.86f : 1f;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // CharacterController contacts are reported every frame.  Applying a full
        // impulse on each report made the ball accelerate unrealistically fast.
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic || currentSpeed < 0.2f)
            return;

        if (rb == lastTouchedBall && Time.time < lastBallTouchTime + ballTouchCooldown)
            return;

        Vector3 planarMove = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (planarMove.sqrMagnitude < 0.0001f)
            return;

        float speedFactor = Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, runSpeed));
        Vector3 pushDir = (planarMove.normalized + Vector3.up * 0.08f).normalized;
        float impulse = Mathf.Lerp(0.45f, Mathf.Min(kickForce, 1.8f), speedFactor);

        rb.AddForce(pushDir * impulse, ForceMode.Impulse);
        SoccerMatchManager.Instance?.RegisterBallTouch(true);
        if (rb.linearVelocity.magnitude > 12f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * 12f;
        }

        lastTouchedBall = rb;
        lastBallTouchTime = Time.time;
    }

    private void TryPowerShot(Vector3 moveDir)
    {
        if (Time.time < nextPowerShotTime)
            return;

        Vector3 shotDirection = moveDir.sqrMagnitude > 0.01f ? moveDir : transform.forward;
        Rigidbody ball = FindControllableBall(shotDirection, ballControlRange + 0.3f);
        if (ball == null)
            return;

        PlayPowerShotAnimation();
        Vector3 kickDirection = (shotDirection + Vector3.up * 0.12f).normalized;
        ball.linearVelocity = new Vector3(0f, ball.linearVelocity.y, 0f);
        ball.AddForce(kickDirection * powerShotForce, ForceMode.Impulse);
        nextPowerShotTime = Time.time + powerShotCooldown;
        SoccerMatchManager.Instance?.RegisterBallTouch(true);
        lastTouchedBall = ball;
        lastBallTouchTime = Time.time;
    }

    private Rigidbody FindControllableBall(Vector3 preferredDirection, float range)
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position + Vector3.up * 0.25f, range, ~0, QueryTriggerInteraction.Ignore);
        Rigidbody bestBall = null;
        float bestScore = float.MaxValue;

        foreach (Collider candidate in nearby)
        {
            if (!candidate.CompareTag("SoccerBall"))
                continue;

            Rigidbody ball = candidate.attachedRigidbody;
            if (ball == null || ball.isKinematic)
                continue;

            Vector3 toBall = ball.position - transform.position;
            Vector3 flatToBall = Vector3.ProjectOnPlane(toBall, Vector3.up);
            if (flatToBall.sqrMagnitude < 0.001f)
                continue;

            // Prefer balls in the direction of travel, while allowing a generous
            // side/rear reach so a slightly missed touch still feels responsive.
            float directionPenalty = 1f - Mathf.Clamp01(Vector3.Dot(preferredDirection.normalized, flatToBall.normalized));
            float score = flatToBall.sqrMagnitude + directionPenalty * 0.4f;
            if (score < bestScore)
            {
                bestScore = score;
                bestBall = ball;
            }
        }

        return bestBall;
    }

    private void CreateFootTouchZone()
    {
        const string zoneName = "FootBallTouchZone";
        Transform existingZone = transform.Find(zoneName);
        GameObject zone = existingZone != null ? existingZone.gameObject : new GameObject(zoneName);
        zone.transform.SetParent(transform, false);
        zone.transform.localPosition = footTouchZoneOffset;
        zone.transform.localRotation = Quaternion.identity;

        BoxCollider box = zone.GetComponent<BoxCollider>();
        if (box == null)
            box = zone.AddComponent<BoxCollider>();

        // This is deliberately a solid collider rather than a trigger or a
        // force-based helper. It gives the foot a larger contact surface, while
        // the ball stays a fully independent Rigidbody and can be lost on turns.
        box.isTrigger = false;
        box.size = footTouchZoneSize;
        Physics.IgnoreCollision(box, controller, true);
    }

    private void SweepFootTouchZone(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.001f || currentSpeed < 0.2f)
            return;

        if (Time.time < lastBallTouchTime + footTouchCooldown)
            return;

        Vector3 center = transform.TransformPoint(footTouchZoneOffset);
        Vector3 halfExtents = footTouchZoneSize * 0.5f;
        float sweepDistance = currentSpeed * Time.deltaTime + 0.22f;
        RaycastHit[] hits = Physics.BoxCastAll(
            center,
            halfExtents,
            moveDir,
            transform.rotation,
            sweepDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag("SoccerBall"))
                continue;

            Rigidbody ball = hit.collider.attachedRigidbody;
            if (ball == null || ball.isKinematic)
                continue;

            Vector3 pushDirection = (moveDir + Vector3.up * 0.06f).normalized;
            ball.AddForce(pushDirection * footTouchKickForce, ForceMode.Impulse);
            SoccerMatchManager.Instance?.RegisterBallTouch(true);
            lastTouchedBall = ball;
            lastBallTouchTime = Time.time;
            return;
        }
    }

    private void TryChallengeOpponentBall(Vector3 moveDir)
    {
        SoccerMatchManager manager = SoccerMatchManager.Instance;
        if (manager == null || !manager.OpponentHasPossession || moveDir.sqrMagnitude < 0.01f || currentSpeed < 1f || Time.time < nextChallengeTime)
            return;

        Rigidbody ball = manager.MatchBall;
        if (ball == null) return;

        Vector3 toBall = Vector3.ProjectOnPlane(ball.position - transform.position, Vector3.up);
        float distance = toBall.magnitude;
        if (distance > challengeRange || distance < 0.05f)
            return;

        // This remains directional: running at the ball lets a player tackle;
        // merely standing beside the AI cannot steal it automatically.
        if (Vector3.Dot(moveDir, toBall / distance) < 0.25f)
            return;

        // The player always attacks toward the north goal. When stealing from
        // behind an AI, use that target direction for the first touch instead
        // of kicking the ball farther along the AI's run direction.
        Vector3 playerAttackDirection = Vector3.forward;
        Vector3 clearDirection = (playerAttackDirection * 0.9f + moveDir * 0.2f + Vector3.up * 0.05f).normalized;
        ball.AddForce(clearDirection * challengeKickForce, ForceMode.Impulse);
        manager.RegisterBallTouch(true);
        lastTouchedBall = ball;
        lastBallTouchTime = Time.time;
        nextChallengeTime = Time.time + challengeCooldown;
    }

    private void PlayPowerShotAnimation()
    {
        if (animator == null || !animator.isHuman)
            return;

        rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (rightUpperLeg == null || rightLowerLeg == null || rightFoot == null)
            return;

        rightUpperLegBaseRotation = rightUpperLeg.localRotation;
        rightLowerLegBaseRotation = rightLowerLeg.localRotation;
        rightFootBaseRotation = rightFoot.localRotation;
        powerShotAnimationTime = 0f;
    }

    private void RestorePowerShotPose()
    {
        if (rightUpperLeg != null) rightUpperLeg.localRotation = rightUpperLegBaseRotation;
        if (rightLowerLeg != null) rightLowerLeg.localRotation = rightLowerLegBaseRotation;
        if (rightFoot != null) rightFoot.localRotation = rightFootBaseRotation;
    }
}
