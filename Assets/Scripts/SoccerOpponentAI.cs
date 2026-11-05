using UnityEngine;

/// <summary>Simple first-pass football opponent: approaches from behind the ball and shoots south.</summary>
public class SoccerOpponentAI : MonoBehaviour
{
    [Min(0.1f)] public float runSpeed = 7.2f;
    [Min(0.1f)] public float kickRange = 1.15f;
    [Min(0f)] public float kickForce = 5.1f;
    [Min(0f)] public float powerShotCooldown = 3f;
    [Min(0.1f)] public float powerShotAnimationDuration = 0.48f;
    [Header("Ball Challenges")]
    [Tooltip("How close the AI must be before it can knock a player-carried ball loose")]
    [Min(0.1f)] public float tackleRange = 1.08f;
    [Min(0f)] public float tackleForce = 1.4f;
    [Min(0.05f)] public float tackleCooldown = 1.1f;
    [Tooltip("Small speed boost used only while pressing a player who has the ball")]
    [Min(1f)] public float playerPressSpeedMultiplier = 1.035f;
    [Tooltip("Stronger pressure during the first seconds after each kickoff")]
    [Min(1f)] public float openingPressSpeedMultiplier = 1.10f;
    public Vector3 targetGoal = new Vector3(0f, 0f, -47f);

    private Rigidbody ball;
    private Animator animator;
    private Transform playerTransform;
    private float nextKickTime;
    private float nextDribbleTime;
    private float nextTackleTime;
    private float possessionSpeedMultiplier = 1f;
    private float avoidanceSide;
    private float avoidanceUntil;
    private Vector3 movementHeading;
    private float recoveryUntil;
    private Vector3 recoveryHeading;
    private float powerShotTime = -1f;
    private Transform rightUpperLeg;
    private Transform rightLowerLeg;
    private Transform rightFoot;
    private Quaternion upperLegBase;
    private Quaternion lowerLegBase;
    private Quaternion footBase;
    public float PowerShotCooldownRemaining => Mathf.Max(0f, nextKickTime - Time.time);

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) playerTransform = player.transform;
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Play("Base Layer.Run", 0, Random.value);
        }
    }

    private void Update()
    {
        if (SoccerMatchManager.Instance == null || !SoccerMatchManager.Instance.RoundActive)
            return;

        if (ball == null)
            ball = SoccerMatchManager.Instance.MatchBall;
        if (ball == null)
            return;

        Vector3 ballPosition = ball.position;
        Vector3 shotDirection = Vector3.ProjectOnPlane(targetGoal - ballPosition, Vector3.up).normalized;
        bool playerHasPossession = SoccerMatchManager.Instance.PlayerHasPossession;
        Vector3 approachPoint = ballPosition - shotDirection * 0.62f;

        float ballDistance = Vector3.ProjectOnPlane(ballPosition - transform.position, Vector3.up).magnitude;
        // A player carrying the ball is a challenge target, not an obstacle to
        // route around. Approach from a side/front lane instead of following
        // straight behind the player's back.
        Vector3 destination = playerHasPossession
            ? GetChallengeApproachPoint(ballPosition)
            : ballDistance <= kickRange ? ballPosition + shotDirection * 0.42f : approachPoint;
        if (Time.time < recoveryUntil)
            destination = ballPosition + recoveryHeading * 0.7f;
        float pressSpeed = SoccerMatchManager.Instance.IsOpeningPhase
            ? openingPressSpeedMultiplier
            : playerPressSpeedMultiplier;
        MoveTowards(
            playerHasPossession || Time.time < recoveryUntil ? destination : ChooseAvoidanceRoute(destination),
            playerHasPossession ? pressSpeed : 1f);

        if (playerHasPossession)
        {
            TryTackle(ballPosition, shotDirection, ballDistance);
            return;
        }

        if (ballDistance > kickRange)
            return;

        if (Time.time >= nextKickTime)
        {
            Vector3 kickDirection = (shotDirection + Vector3.up * 0.05f).normalized;
            ball.AddForce(kickDirection * kickForce, ForceMode.Impulse);
            SoccerMatchManager.Instance?.RegisterBallTouch(false);
            PlayPowerShotAnimation();
            nextKickTime = Time.time + powerShotCooldown;
        }
        else if (Time.time >= nextDribbleTime)
        {
            // Keep advancing while the power shot is cooling down instead of
            // waiting next to the ball for the next long kick.
            ball.AddForce(shotDirection * 1.15f, ForceMode.Impulse);
            SoccerMatchManager.Instance?.RegisterBallTouch(false);
            nextDribbleTime = Time.time + 0.22f;
        }
    }

    private void TryTackle(Vector3 ballPosition, Vector3 shotDirection, float ballDistance)
    {
        if (ballDistance > tackleRange || Time.time < nextTackleTime)
            return;

        Vector3 awayFromOpponent = Vector3.ProjectOnPlane(ballPosition - transform.position, Vector3.up);
        if (awayFromOpponent.sqrMagnitude < 0.001f)
            awayFromOpponent = transform.forward;
        awayFromOpponent.Normalize();

        // Push it forward for the AI, but enough sideways/away force remains
        // that the player can immediately contest and win it back.
        Vector3 tackleDirection = (shotDirection * 0.8f + awayFromOpponent * 0.6f + Vector3.up * 0.04f).normalized;
        ball.AddForce(tackleDirection * tackleForce, ForceMode.Impulse);
        SoccerMatchManager.Instance.RegisterBallTouch(false);
        nextTackleTime = Time.time + tackleCooldown;
        // The first recovery step follows the cleared ball toward the AI's
        // scoring end, avoiding a slow, unnatural U-turn after a tackle.
        recoveryHeading = shotDirection;
        recoveryUntil = Time.time + 0.42f;
    }

    private Vector3 GetChallengeApproachPoint(Vector3 ballPosition)
    {
        if (playerTransform == null)
            return ballPosition;

        Vector3 playerHeading = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
        if (playerHeading.sqrMagnitude < 0.01f)
            playerHeading = Vector3.forward;
        playerHeading.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, playerHeading);
        Vector3 aiFromPlayer = Vector3.ProjectOnPlane(transform.position - playerTransform.position, Vector3.up);
        // Stay on the side the AI is already closest to, then aim slightly
        // ahead of the carrier. This creates a natural wrap-around line.
        float sideSign = Vector3.Dot(aiFromPlayer, side) >= 0f ? 1f : -1f;
        float ahead = Vector3.Dot(aiFromPlayer, playerHeading) < 0f ? 0.7f : 0.35f;
        return ballPosition + side * sideSign * 1.05f + playerHeading * ahead;
    }

    public void ResetForKickoff(Vector3 position, Vector3 facing)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        movementHeading = facing.normalized;
        nextKickTime = Time.time + 0.8f;
    }

    public void SetWaitingForKickoff(bool waiting)
    {
        if (animator == null) return;
        if (waiting)
        {
            animator.Play("Base Layer.Run", 0, 0f);
            animator.speed = 0f;
        }
        else
        {
            animator.speed = 1f;
        }
    }

    private void MoveTowards(Vector3 destination, float extraSpeedMultiplier = 1f)
    {
        Vector3 delta = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        if (delta.sqrMagnitude < 0.0025f)
            return;

        Vector3 direction = delta.normalized;
        if (movementHeading.sqrMagnitude < 0.01f)
            movementHeading = transform.forward;

        // Carrying the ball has a much lower steering rate, so the AI must take
        // a natural arc instead of instantly cutting back through the ball.
        float turnRate = Time.time < recoveryUntil ? 720f : possessionSpeedMultiplier < 0.99f ? 240f : 620f;
        movementHeading = Vector3.RotateTowards(movementHeading, direction, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        transform.rotation = Quaternion.LookRotation(movementHeading, Vector3.up);
        transform.position += movementHeading * runSpeed * possessionSpeedMultiplier * extraSpeedMultiplier * Time.deltaTime;
    }

    private Vector3 ChooseAvoidanceRoute(Vector3 destination)
    {
        Vector3 origin = transform.position;
        Vector3 path = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
        if (path.sqrMagnitude < 0.0025f)
            return destination;

        Vector3 direction = path.normalized;
        if (Time.time < avoidanceUntil)
            return destination + Vector3.Cross(Vector3.up, direction) * avoidanceSide * 2.1f;

        if (playerTransform == null)
            return destination;

        Vector3 toPlayer = Vector3.ProjectOnPlane(playerTransform.position - origin, Vector3.up);
        float pathLength = path.magnitude;
        float forwardDistance = Vector3.Dot(toPlayer, direction);
        float lateralDistance = Mathf.Abs(Vector3.Cross(direction, toPlayer).y);

        // Only detour when the player actually blocks the direct running lane.
        if (forwardDistance <= 0.1f || forwardDistance >= pathLength || lateralDistance > 1.35f)
            return destination;

        Vector3 side = Vector3.Cross(Vector3.up, direction);
        avoidanceSide = Vector3.Dot(toPlayer, side) >= 0f ? -1f : 1f;
        avoidanceUntil = Time.time + 0.7f;
        return destination + side * avoidanceSide * 2.1f;
    }

    private void LateUpdate()
    {
        if (powerShotTime < 0f) return;
        powerShotTime += Time.deltaTime;
        float t = Mathf.Clamp01(powerShotTime / powerShotAnimationDuration);
        if (t >= 1f)
        {
            if (rightUpperLeg != null) rightUpperLeg.localRotation = upperLegBase;
            if (rightLowerLeg != null) rightLowerLeg.localRotation = lowerLegBase;
            if (rightFoot != null) rightFoot.localRotation = footBase;
            powerShotTime = -1f;
            return;
        }

        float thigh = t < 0.28f ? Mathf.Lerp(0f, 22f, t / 0.28f)
            : t < 0.58f ? Mathf.Lerp(22f, -62f, (t - 0.28f) / 0.30f)
            : Mathf.Lerp(-62f, 0f, (t - 0.58f) / 0.42f);
        float shin = t < 0.30f ? Mathf.Lerp(0f, 12f, t / 0.30f)
            : t < 0.58f ? Mathf.Lerp(12f, -46f, (t - 0.30f) / 0.28f)
            : Mathf.Lerp(-46f, 0f, (t - 0.58f) / 0.42f);
        if (rightUpperLeg != null) rightUpperLeg.localRotation = upperLegBase * Quaternion.AngleAxis(thigh, Vector3.right);
        if (rightLowerLeg != null) rightLowerLeg.localRotation = lowerLegBase * Quaternion.AngleAxis(shin, Vector3.right);
        if (rightFoot != null) rightFoot.localRotation = footBase * Quaternion.AngleAxis(-thigh * 0.3f, Vector3.right);
    }

    private void PlayPowerShotAnimation()
    {
        if (animator == null || !animator.isHuman) return;
        rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (rightUpperLeg == null || rightLowerLeg == null || rightFoot == null) return;
        upperLegBase = rightUpperLeg.localRotation;
        lowerLegBase = rightLowerLeg.localRotation;
        footBase = rightFoot.localRotation;
        powerShotTime = 0f;
    }

    public void SetPossessionSlowdown(bool hasPossession)
    {
        possessionSpeedMultiplier = hasPossession ? 0.86f : 1f;
    }
}
