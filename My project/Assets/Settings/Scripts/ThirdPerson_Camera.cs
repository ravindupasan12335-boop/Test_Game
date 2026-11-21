using UnityEngine;

public class ThirdPerson_Camera : MonoBehaviour
{
    [Header("Target & Framing")]
    public Transform target; // Drag Cube_Player here
    public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 5f;
    public float heightOffset = 0.5f;
    public float faceYawOffset = 90f; // Aligns robot's visual front with camera forward

    [Header("Camera Collision (Anti-Wall Clip)")]
    public bool avoidObstacles = true;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;
    public float collisionBuffer = 0.2f;
    public float minDistance = 0.5f;

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

    void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            currentPos = transform.position;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
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

        // 1. Read mouse movement
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 2. Rotate the player character horizontally with the mouse
        target.rotation = Quaternion.Euler(0f, yaw + faceYawOffset, 0f);

        // 3. Calculate camera rotation (flip 180 degrees horizontally when holding V)
        bool lookingBack = Input.GetKey(rearViewKey);
        float activeYaw = lookingBack ? yaw + 180f : yaw;
        float activePitch = lookingBack ? Mathf.Clamp(pitch * 0.5f, -5f, 25f) : pitch;

        Quaternion camRotation = Quaternion.Euler(activePitch, activeYaw, 0f);
        Vector3 focusPoint = target.position + pivotOffset;
        Vector3 desiredPos = focusPoint - (camRotation * Vector3.forward * distance) + (Vector3.up * heightOffset);

        // 4. Pull camera inward if a wall/obstacle sits between focusPoint and desiredPos
        if (avoidObstacles)
        {
            desiredPos = ResolveCameraCollision(focusPoint, desiredPos);
        }

        // 5. Smoothly move camera into position
        currentPos = Vector3.Lerp(currentPos, desiredPos, Time.deltaTime * smoothSpeed);
        transform.position = currentPos;
        transform.LookAt(focusPoint + Vector3.up * 0.4f);
    }

    Vector3 ResolveCameraCollision(Vector3 origin, Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - origin;
        float maxDist = toTarget.magnitude;
        if (maxDist <= 0.001f) return targetPos;

        Vector3 dir = toTarget / maxDist;
        RaycastHit[] hits = Physics.SphereCastAll(origin, collisionRadius, dir, maxDist, collisionMask, QueryTriggerInteraction.Ignore);

        float closestHitDist = maxDist;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform == target || hitTransform.IsChildOf(target))
            {
                continue;
            }

            if (hits[i].distance < closestHitDist)
            {
                closestHitDist = hits[i].distance;
            }
        }

        if (closestHitDist < maxDist)
        {
            float safeDist = Mathf.Clamp(closestHitDist - collisionBuffer, minDistance, maxDist);
            return origin + dir * safeDist;
        }

        return targetPos;
    }
}