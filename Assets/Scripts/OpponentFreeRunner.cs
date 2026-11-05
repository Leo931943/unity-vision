using UnityEngine;

/// <summary>
/// Temporary movement preview for the future AI opponent. It follows a wide
/// looping run path so its imported locomotion can be inspected in-game.
/// </summary>
public class OpponentFreeRunner : MonoBehaviour
{
    [Min(0.1f)] public float runSpeed = 5.2f;
    [Min(1f)] public float horizontalRadius = 12f;
    [Min(1f)] public float verticalRadius = 8f;
    public Vector3 pathCenter;

    private Animator animator;
    private float pathAngle;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            animator.Play("Base Layer.Run", 0, Random.value);
        }

        if (pathCenter == Vector3.zero)
            pathCenter = transform.position;
    }

    private void Update()
    {
        float circumferenceScale = Mathf.Max(horizontalRadius, verticalRadius);
        pathAngle += runSpeed / circumferenceScale * Time.deltaTime;

        Vector3 nextPosition = pathCenter + new Vector3(
            Mathf.Cos(pathAngle) * horizontalRadius,
            0f,
            Mathf.Sin(pathAngle) * verticalRadius);
        Vector3 tangent = new Vector3(
            -Mathf.Sin(pathAngle) * horizontalRadius,
            0f,
            Mathf.Cos(pathAngle) * verticalRadius).normalized;

        transform.SetPositionAndRotation(nextPosition, Quaternion.LookRotation(tangent, Vector3.up));
    }
}
