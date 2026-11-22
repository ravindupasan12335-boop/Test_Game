using UnityEngine;

public class ThirdPerson_Camera : MonoBehaviour
{
    [Header("Target & Framing")]
    public Transform target; // Drag your Player object here
    public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 5f;
    public float heightOffset = 0.5f;
    public float faceYawOffset = 0f; // Set to 0 for normal models (change to 90, -90, or 180 only if model faces wrong way)

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.5f;
    public float minPitch = -20f;
    public float maxPitch = 65f;
    public bool lockCursor = true;

    [Header("Rear View (Hold V)")]
    public KeyCode rearViewKey = KeyCode.V;
    public float smoothSpeed = 14f;

    private float yaw = 0f;
    private float pitch = 15f;
    private Vector3 currentPos;
    private Quaternion initialTilt = Quaternion.identity;
    private Rigidbody targetRb;

    // Horizontal directions used by Cube_Player_Movement so W/A/S/D always matches the camera
    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

    void Start()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetRb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Preserve the character's starting X/Z tilt so imported models never lay flat on the floor
            float initialY = target.eulerAngles.y;
            initialTilt = Quaternion.Inverse(Quaternion.Euler(0f, initialY, 0f)) * target.rotation;
            yaw = initialY;

            // Snap camera immediately behind the player on frame 1
            Quaternion startCamRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + pivotOffset;
            currentPos = focusPoint - (startCamRot * Vector3.forward * distance) + (Vector3.up * heightOffset);
            transform.position = currentPos;
            transform.LookAt(focusPoint + Vector3.up * 0.4f);
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (target == null) return;

        // Allow pressing Escape to free the cursor in the editor if needed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursor && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Only turn camera when cursor is locked (or lockCursor is disabled)
        if (!lockCursor || Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // Rotate the player smoothly around world Y while preserving starting upright tilt
        Quaternion targetRot = Quaternion.Euler(0f, yaw + faceYawOffset, 0f) * initialTilt;

        if (targetRb != null)
        {
            targetRb.MoveRotation(targetRot);
        }
        else
        {
            target.rotation = targetRot;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate camera rotation (flip 180 degrees horizontally when holding V)
        bool lookingBack = Input.GetKey(rearViewKey);
        float activeYaw = lookingBack ? yaw + 180f : yaw;
        float activePitch = lookingBack ? Mathf.Clamp(pitch * 0.5f, -5f, 25f) : pitch;

        Quaternion camRotation = Quaternion.Euler(activePitch, activeYaw, 0f);
        Vector3 focusPoint = target.position + pivotOffset;
        Vector3 desiredPos = focusPoint - (camRotation * Vector3.forward * distance) + (Vector3.up * heightOffset);

        // Smoothly move camera into position
        currentPos = Vector3.Lerp(currentPos, desiredPos, Time.deltaTime * smoothSpeed);
        transform.position = currentPos;
        transform.LookAt(focusPoint + Vector3.up * 0.4f);
    }
}