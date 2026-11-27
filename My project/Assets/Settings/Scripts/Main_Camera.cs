using UnityEngine;

public class ThirdPerson_Camera : MonoBehaviour
{
    public enum CameraMode
    {
        ThirdPersonNormal = 0,
        ThirdPersonBack = 1,
        FirstPerson = 2
    }

    [Header("Target & Framing")]
    public Transform target; // Drag Cube_Player / Player here
    public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 5f;
    public float heightOffset = 0.5f;
    public float faceYawOffset = 0f; // Adjust (e.g., 90, -90, 180) if character front differs from forward

    [Header("First-Person Settings")]
    [Tooltip("Offset from focusPoint (target.position + pivotOffset). Push Z forward if head mesh clips lens.")]
    public Vector3 firstPersonOffset = new Vector3(0f, 0.25f, 0.65f);
    [Tooltip("Horizontal Y-axis rotation offset in First-Person (set to 180 to face outward away from the head).")]
    public float firstPersonYawOffset = 180f;
    [Tooltip("How strongly vertical mouse pitch tilts the First-Person view up/down.")]
    public float firstPersonPitchScale = 1f;
    [Tooltip("Optional: keep off if placing the camera right in front of the head/face so collision doesn't push it back.")]
    public bool firstPersonAvoidObstacles = false;

    [Header("Camera Collision (Anti-Wall Clip)")]
    public bool avoidObstacles = true;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;
    public float collisionBuffer = 0.2f;
    public float minDistance = 0.5f;

    [Header("Mouse Look & Vertical Limits")]
    public float mouseSensitivity = 2.5f;
    [Tooltip("How far up (in degrees) you can look.")]
    [Range(0f, 89f)]
    public float maxLookUpAngle = 55f;
    [Tooltip("How far down (in degrees) you can look.")]
    [Range(0f, 89f)]
    public float maxLookDownAngle = 65f;
    public bool lockCursor = true;

    [Header("Perspective Cycle (Press V)")]
    public KeyCode cycleViewKey = KeyCode.V;
    public CameraMode startMode = CameraMode.ThirdPersonNormal;
    public float smoothSpeed = 14f;

    private CameraMode currentMode;
    private float yaw = 0f;
    private float pitch = 15f;
    private Vector3 currentPos;
    private Quaternion initialTilt = Quaternion.identity;
    private Rigidbody targetRb;
    private Cube_Player_Movement playerMovement;
    private bool wasPushingLastFrame = false;

    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

    void Start()
    {
        currentMode = startMode;

        if (target != null)
        {
            playerMovement = target.GetComponent<Cube_Player_Movement>();
            targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetRb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            float initialY = target.eulerAngles.y;
            initialTilt = Quaternion.Inverse(Quaternion.Euler(0f, initialY, 0f)) * target.rotation;
            yaw = initialY - faceYawOffset;

            Quaternion startCamRot = Quaternion.Euler(Mathf.Clamp(pitch, -maxLookUpAngle, maxLookDownAngle), yaw, 0f);
            Vector3 focusPoint = target.position + pivotOffset;
            currentPos = focusPoint - (startCamRot * Vector3.forward * distance) + (Vector3.up * heightOffset);
            transform.position = currentPos;
            transform.LookAt(focusPoint + Vector3.up * 0.4f);
        }
        else
        {
            currentPos = transform.position;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnValidate()
    {
        maxLookUpAngle = Mathf.Clamp(maxLookUpAngle, 0f, 89f);
        maxLookDownAngle = Mathf.Clamp(maxLookDownAngle, 0f, 89f);
    }

    void Update()
    {
        if (target == null) return;

        if (playerMovement == null)
        {
            playerMovement = target.GetComponent<Cube_Player_Movement>();
        }

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

        if (Input.GetKeyDown(cycleViewKey))
        {
            currentMode = (CameraMode)(((int)currentMode + 1) % 3);
        }

        bool isPushingBox = playerMovement != null && playerMovement.IsPushingOrPullingObject;

        if (wasPushingLastFrame && !isPushingBox)
        {
            yaw = target.eulerAngles.y - faceYawOffset;
        }
        wasPushingLastFrame = isPushingBox;

        if (!lockCursor || Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -maxLookUpAngle, maxLookDownAngle);

            if (!isPushingBox)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                yaw += mouseX;
            }
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;

        bool isPushingBox = playerMovement != null && playerMovement.IsPushingOrPullingObject;
        if (isPushingBox) return;

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

        bool isPushingBox = playerMovement != null && playerMovement.IsPushingOrPullingObject;
        Vector3 focusPoint = target.position + pivotOffset;

        if (currentMode == CameraMode.FirstPerson)
        {
            Vector3 desiredFpPos = focusPoint + (target.rotation * firstPersonOffset);

            if (avoidObstacles && firstPersonAvoidObstacles)
            {
                desiredFpPos = ResolveCameraCollision(focusPoint, desiredFpPos);
            }

            // Snap directly in First-Person so sprinting never lets the character body outpace the camera
            currentPos = desiredFpPos;
            transform.position = currentPos;

            float fpPitch = Mathf.Clamp(pitch * firstPersonPitchScale, -maxLookUpAngle, maxLookDownAngle);

            if (isPushingBox && playerMovement.PushedObjectTransform != null)
            {
                Vector3 lookTarget = playerMovement.PushedObjectTransform.position + Vector3.up * 0.6f;
                Vector3 dirToBox = lookTarget - transform.position;
                if (dirToBox.sqrMagnitude > 0.0001f)
                {
                    float boxYaw = Quaternion.LookRotation(dirToBox.normalized, Vector3.up).eulerAngles.y;
                    Quaternion boxLookRot = Quaternion.Euler(fpPitch, boxYaw, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, boxLookRot, Time.deltaTime * smoothSpeed);
                }
            }
            else
            {
                float fpYaw = yaw + firstPersonYawOffset;
                Quaternion desiredFpRot = Quaternion.Euler(fpPitch, fpYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredFpRot, Time.deltaTime * smoothSpeed);
            }
        }
        else
        {
            bool lookingBack = currentMode == CameraMode.ThirdPersonBack;
            float activeYaw = lookingBack ? yaw + 180f : yaw;
            float activePitch = lookingBack
                ? Mathf.Clamp(pitch * 0.5f, -Mathf.Min(maxLookUpAngle, 15f), Mathf.Min(maxLookDownAngle, 25f))
                : pitch;

            Quaternion camRotation = Quaternion.Euler(activePitch, activeYaw, 0f);
            Vector3 desiredPos = focusPoint - (camRotation * Vector3.forward * distance) + (Vector3.up * heightOffset);

            if (avoidObstacles)
            {
                desiredPos = ResolveCameraCollision(focusPoint, desiredPos);
            }

            currentPos = Vector3.Lerp(currentPos, desiredPos, Time.deltaTime * smoothSpeed);
            transform.position = currentPos;

            if (isPushingBox && playerMovement.PushedObjectTransform != null)
            {
                Vector3 midpointFocus = Vector3.Lerp(
                    focusPoint + Vector3.up * 0.4f,
                    playerMovement.PushedObjectTransform.position + Vector3.up * 0.5f,
                    0.55f
                );
                transform.LookAt(midpointFocus);
            }
            else
            {
                transform.LookAt(focusPoint + Vector3.up * 0.4f);
            }
        }
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