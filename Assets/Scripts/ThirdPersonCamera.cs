using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Camera Distance & Zoom")]
    public float defaultDistance = 5.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 14.0f;
    public float zoomSpeed = 15f;

    [Header("Sensitivity & Smoothing")]
    public float mouseSensitivity = 2.0f;
    public float rotationSmoothTime = 0.05f;
    public float minPitch = -25f;
    public float maxPitch = 70f;

    [Header("First Person")]
    [Tooltip("Camera position relative to the target while in first-person view")]
    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("Hide the local character mesh in first-person view to prevent face clipping")]
    public bool hideTargetRenderersInFirstPerson = true;

    [Header("Collision Avoidance")]
    public LayerMask collisionMask = ~0; // Everything by default
    public float collisionRadius = 0.25f;
    public float collisionOffset = 0.15f;

    private float yaw;
    private float pitch = 15f;
    private float targetDistance;
    private float currentDistance;
    private float currentDistanceVelocity;
    private Vector3 currentRotation;
    private Vector3 rotationSmoothVelocity;
    private bool cursorLocked = true;
    private bool isFirstPerson;
    private Renderer[] targetRenderers;

    public bool IsFirstPerson => isFirstPerson;

    void Start()
    {
        targetDistance = defaultDistance;
        currentDistance = defaultDistance;
        SetCursorLock(true);

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            currentRotation = new Vector3(pitch, yaw, 0);
            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
        }
    }

    void Update()
    {
        HandleCursorLockToggle();
        HandleViewToggle();
        ReadInput(out float mouseX, out float mouseY, out float scroll);

        if (cursorLocked)
        {
            yaw += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (!isFirstPerson)
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Smooth rotation
        Vector3 targetRot = new Vector3(pitch, yaw, 0);
        currentRotation = Vector3.SmoothDamp(currentRotation, targetRot, ref rotationSmoothVelocity, rotationSmoothTime);
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);

        if (isFirstPerson)
        {
            transform.SetPositionAndRotation(target.position + firstPersonOffset, rotation);
            return;
        }

        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredCameraPos = focusPoint - (rotation * Vector3.forward * targetDistance);

        // Collision detection
        float finalDist = targetDistance;
        RaycastHit hit;
        Vector3 rayDir = (desiredCameraPos - focusPoint).normalized;
        if (Physics.SphereCast(focusPoint, collisionRadius, rayDir, out hit, targetDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            finalDist = Mathf.Clamp(hit.distance - collisionOffset, minDistance, targetDistance);
        }

        currentDistance = Mathf.SmoothDamp(currentDistance, finalDist, ref currentDistanceVelocity, 0.05f);
        transform.position = focusPoint - (rotation * Vector3.forward * currentDistance);
        transform.LookAt(focusPoint);
    }

    private void ReadInput(out float mouseX, out float mouseY, out float scroll)
    {
        mouseX = 0;
        mouseY = 0;
        scroll = 0;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            mouseX = delta.x * 0.1f;
            mouseY = delta.y * 0.1f;
            // Input System reports very small values on some touchpads and
            // mice.  Boost this before applying zoomSpeed so one wheel step
            // produces a clearly visible camera-distance change.
            scroll = mouse.scroll.ReadValue().y * 0.25f;
        }
#else
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
        scroll = Input.GetAxis("Mouse ScrollWheel");
#endif
    }

    private void HandleCursorLockToggle()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(!cursorLocked);
        }
        else if (!cursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLock(true);
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!cursorLocked);
        }
        else if (!cursorLocked && Input.GetMouseButtonDown(0))
        {
            SetCursorLock(true);
        }
#endif
    }

    private void HandleViewToggle()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.V))
#endif
        {
            isFirstPerson = !isFirstPerson;
            SetTargetRenderersVisible(!isFirstPerson);
        }
    }

    private void SetTargetRenderersVisible(bool visible)
    {
        if (!hideTargetRenderersInFirstPerson || targetRenderers == null)
            return;

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = visible;
        }
    }

    private void OnDisable()
    {
        // Returning to a menu or changing scenes should never leave the player invisible.
        SetTargetRenderersVisible(true);
    }

    public void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
