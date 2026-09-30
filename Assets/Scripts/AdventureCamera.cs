using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class AdventureCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField, Range(35f, 75f)] private float initialPitch = 55f;
    [SerializeField] private float initialYaw = 30f;
    [SerializeField, Min(1f)] private float initialDistance = 18f;
    [SerializeField, Min(1f)] private float minimumDistance = 3.5f;
    [SerializeField, Min(1f)] private float maximumDistance = 28f;
    [SerializeField, Min(0f)] private float rotationSensitivity = 3f;
    [SerializeField, Min(0f)] private float zoomSensitivity = 5f;
    [SerializeField, Min(0f)] private float panSensitivity = 0.09f;
    [SerializeField, Min(0f)] private float maximumPan = 12f;
    [SerializeField, Min(0f)] private float followSharpness = 7f;
    [SerializeField, Min(0f)] private float orbitSharpness = 12f;
    [SerializeField, Min(0f)] private float collisionRadius = 0.25f;

    private float yaw;
    private float pitch;
    private float desiredDistance;
    private float currentDistance;
    private Vector3 panOffset;
    private Vector3 currentFocus;
    private Quaternion currentRotation;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError("AdventureCamera needs a player target.", this);
            enabled = false;
            return;
        }

        yaw = initialYaw;
        pitch = initialPitch;
        desiredDistance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
        currentDistance = desiredDistance;
        currentFocus = target.position + focusOffset;
        currentRotation = Quaternion.Euler(pitch, yaw, 0f);
        PlaceCamera();
    }

    private void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * rotationSensitivity, 35f, 75f);
        }

        if (Input.GetMouseButton(2))
        {
            Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = horizontalRotation * Vector3.right;
            Vector3 forward = horizontalRotation * Vector3.forward;
            panOffset -= (right * Input.GetAxis("Mouse X") + forward * Input.GetAxis("Mouse Y"))
                         * (panSensitivity * currentDistance);
            panOffset = Vector3.ClampMagnitude(panOffset, maximumPan);
        }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Home))
            panOffset = Vector3.zero;

        desiredDistance = Mathf.Clamp(desiredDistance - Input.GetAxis("Mouse ScrollWheel") * zoomSensitivity,
            minimumDistance, maximumDistance);

        float followWeight = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        float orbitWeight = 1f - Mathf.Exp(-orbitSharpness * Time.deltaTime);
        currentFocus = Vector3.Lerp(currentFocus, target.position + focusOffset + panOffset, followWeight);
        currentRotation = Quaternion.Slerp(currentRotation, Quaternion.Euler(pitch, yaw, 0f), orbitWeight);
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, orbitWeight);
        PlaceCamera();
    }

    private void PlaceCamera()
    {
        Vector3 direction = -(currentRotation * Vector3.forward);
        float safeDistance = currentDistance;
        RaycastHit hit;
        // The player root uses Ignore Raycast (layer 2), so the camera only reacts to scenery.
        if (Physics.SphereCast(currentFocus, collisionRadius, direction, out hit, currentDistance,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            safeDistance = Mathf.Max(0.5f, hit.distance - collisionRadius);

        transform.SetPositionAndRotation(currentFocus + direction * safeDistance, currentRotation);
    }
}
