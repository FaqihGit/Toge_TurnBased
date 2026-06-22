using UnityEngine;

/// <summary>
/// Generic 2D-style camera follow for a 3D scene where movement is restricted
/// to the X/Y plane (Z is unused/constant on the player). Smooth damping
/// per-axis, a configurable dead-zone (camera only corrects once the target
/// leaves a box around it, rather than tracking 1:1), and velocity-based
/// look-ahead (camera leans slightly in the direction of travel).
///
/// Uses the standard 3D Rigidbody rather than Rigidbody2D, since the scene
/// itself is 3D - only the gameplay movement is constrained to two axes.
/// Z from the Rigidbody's velocity is simply ignored.
///
/// Architecture follows the standard side-scroller camera pattern described
/// in Itay Keren's GDC talk "Scroll Back: The Theory and Practice of Cameras
/// in Side-Scrollers" (dead-zone + look-ahead), with the actual easing done
/// via Unity's SmoothDamp (a critically-damped spring) - the same underlying
/// math Cinemachine uses for its Damping fields.
///
/// Attach this to the Main Camera (or a camera rig parent) and assign Target.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object the camera follows (usually the player root transform).")]
    [SerializeField] private Transform target;
    [Tooltip("Optional. If assigned, look-ahead uses real physics velocity instead of estimating it from frame-to-frame position deltas. X/Y components are used, Z is ignored.")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Header("Follow Offset")]
    [Tooltip("Offset from the target that acts as the camera's tracking center.")]
    [SerializeField] private Vector2 followOffset;

    [Header("Smoothing (seconds-to-target, lower = snappier, 0 = instant)")]
    [SerializeField] private float smoothTimeX = 0.25f;
    [SerializeField] private float smoothTimeY = 0.35f;
    [SerializeField] private float maxSpeed = Mathf.Infinity;

    [Header("Dead Zone")]
    [Tooltip("Camera only moves once the target exits this box (centered on the camera). Set to (0,0) to always track the target directly.")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.5f, 1f);

    [Header("Look Ahead")]
    [SerializeField] private bool useLookAhead = true;
    [Tooltip("World units of look-ahead offset per unit of target speed.")]
    [SerializeField] private float lookAheadFactor = 0.3f;
    [Tooltip("Hard cap on look-ahead offset distance, regardless of speed.")]
    [SerializeField] private float maxLookAhead = 2.5f;
    [Tooltip("How quickly the look-ahead offset itself eases in/out (prevents jitter on quick direction taps).")]
    [SerializeField] private float lookAheadSmoothTime = 0.2f;

    [Header("Vertical Bounds")]
    [SerializeField] private bool useMinYBound = true;

    [Tooltip("Lowest world Y position the camera center is allowed to reach.")]
    [SerializeField] private float minCameraY = 0f;

    [Header("Z Offset")]
    [SerializeField] private float zOffset = -10f;

    // --- internal state ---
    private Vector3 _currentVelocity;   // SmoothDamp velocity for camera position
    private Vector2 _lookAheadCurrent;  // current eased look-ahead offset
    private Vector2 _lookAheadVelocity; // SmoothDamp velocity for the offset above
    private Vector3 _lastTargetPosition;
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (target != null)
            _lastTargetPosition = target.position;
    }

    /// <summary>Swap targets at runtime (e.g. on player respawn/possession change).</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
            _lastTargetPosition = target.position;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        Vector3 pos = target.position + (Vector3)followOffset;
        pos.z = transform.position.z;

        transform.position = pos;

        _currentVelocity = Vector3.zero;
        _lookAheadCurrent = Vector2.zero;
        _lookAheadVelocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector2 targetVelocity = GetTargetVelocity();
        Vector3 desiredPosition = ComputeDesiredPosition(targetVelocity);

        // Independent X/Y smooth times: e.g. horizontal travel can settle
        // faster than vertical bobbing/jumping, which reads more naturally.
        float newX = Mathf.SmoothDamp(transform.position.x, desiredPosition.x, ref _currentVelocity.x, smoothTimeX, maxSpeed, Time.deltaTime);
        float newY = Mathf.SmoothDamp(transform.position.y, desiredPosition.y, ref _currentVelocity.y, smoothTimeY, maxSpeed, Time.deltaTime);

        Vector3 newPosition = new(
            newX,
            newY,
            target.position.z + zOffset);

        // Prevent camera from going too low.
        if (useMinYBound)
        {
            float camHalfHeight = (_cam != null && _cam.orthographic)
                ? _cam.orthographicSize
                : 0f;

            // Keep the bottom of the screen above the ground.
            newPosition.y = Mathf.Max(
                newPosition.y,
                minCameraY + camHalfHeight);
        }

        transform.position = newPosition;
    }

    private Vector2 GetTargetVelocity()
    {
        Vector2 velocity;
        if (targetRigidbody != null)
        {
            // 3D Rigidbody.velocity (Unity 6+: linearVelocity) returns a Vector3;
            // we only care about X/Y since Z is unused in this game's movement.
            // On Unity versions before 6, swap this for targetRigidbody.velocity.
            Vector3 v = targetRigidbody.linearVelocity;
            velocity = new Vector2(v.x, v.y);
        }
        else
        {
            velocity = ((Vector2)target.position - (Vector2)_lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastTargetPosition = target.position;
        }
        return velocity;
    }

    private Vector3 ComputeDesiredPosition(Vector2 targetVelocity)
    {
        // Shift the target by the offset before all camera calculations.
        Vector2 targetPos = (Vector2)target.position + followOffset;
        Vector2 camPos = transform.position;

        // --- Dead zone ---
        Vector2 deltaFromCamera = targetPos - camPos;
        Vector2 halfDeadZone = deadZoneSize * 0.5f;

        Vector2 correction = Vector2.zero;

        if (Mathf.Abs(deltaFromCamera.x) > halfDeadZone.x)
            correction.x = deltaFromCamera.x -
                           Mathf.Sign(deltaFromCamera.x) * halfDeadZone.x;

        if (Mathf.Abs(deltaFromCamera.y) > halfDeadZone.y)
            correction.y = deltaFromCamera.y -
                           Mathf.Sign(deltaFromCamera.y) * halfDeadZone.y;

        Vector2 desired = camPos + correction;

        // --- Look ahead ---
        if (useLookAhead)
        {
            Vector2 rawLookAhead = targetVelocity * lookAheadFactor;
            rawLookAhead = Vector2.ClampMagnitude(rawLookAhead, maxLookAhead);

            _lookAheadCurrent = Vector2.SmoothDamp(
                _lookAheadCurrent,
                rawLookAhead,
                ref _lookAheadVelocity,
                lookAheadSmoothTime);

            desired += _lookAheadCurrent;
        }

        return new Vector3(desired.x, desired.y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));

        // Show the offset center.
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (Vector3)followOffset, 0.15f);
    }
}