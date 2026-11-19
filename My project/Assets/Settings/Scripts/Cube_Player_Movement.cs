using UnityEngine;

public class Cube_Player_Movement : MonoBehaviour
{
    [Header("Movement & Jump")]
    public float moveSpeed = 6f;
    public float sprintSpeedMultiplier = 2f; // 2x walk speed when sprinting
    public float crouchMoveSpeed = 3f;
    public float jumpForce = 6f;
    public KeyCode crouchKey = KeyCode.C;

    [Header("Safe Crouch Collider")]
    public float crouchColliderScaleY = 0.65f;
    public float crouchTransitionSpeed = 14f;

    [Header("Limbs (Drag Pivots from Hierarchy)")]
    public Transform Arm_1;
    public Transform Arm_2;
    public Transform Leg_1; // Left leg pivot
    public Transform Leg_2; // Right leg pivot

    [Header("Standing Swing Settings")]
    public float swingSpeed = 10f;
    public float swingAngle = 35f;

    [Header("Sprint Swing Settings")]
    public float sprintSwingSpeed = 18f; // Faster limb wave when sprinting
    public float sprintSwingAngle = 60f; // Wider swing angle when sprinting

    [Header("Crouch Leg Pose & Wiper Sweep")]
    public float crouchArmAngle = -20f;
    public float leftLegCrouchZ = -80f;
    public float rightLegCrouchZ = 80f;
    public float maxWiperAngle = 180f; // Sweeps 0 -> +180 -> 0
    public float wiperSpeed = 6f;

    private Rigidbody rb;
    private BoxCollider boxCol;
    private bool isGrounded = true;
    private bool isCrouching = false;
    private bool isSprinting = false;
    private float walkTimer = 0f;
    private Vector3 conveyorVelocity = Vector3.zero;

    private Vector3 standingColliderSize;
    private Vector3 standingColliderCenter;

    private Quaternion arm1StartRot;
    private Quaternion arm2StartRot;
    private Quaternion leg1StartRot;
    private Quaternion leg2StartRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCol = GetComponent<BoxCollider>();

        if (boxCol != null)
        {
            standingColliderSize = boxCol.size;
            standingColliderCenter = boxCol.center;
        }

        if (Arm_1 != null) arm1StartRot = Arm_1.localRotation;
        if (Arm_2 != null) arm2StartRot = Arm_2.localRotation;
        if (Leg_1 != null) leg1StartRot = Leg_1.localRotation;
        if (Leg_2 != null) leg2StartRot = Leg_2.localRotation;
    }

    void Update()
    {
        isCrouching = Input.GetKey(crouchKey) || Input.GetKey(KeyCode.LeftControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprinting = shiftHeld && !isCrouching;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isCrouching)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        UpdateCrouchCollider();
    }

    void UpdateCrouchCollider()
    {
        if (boxCol == null) return;

        float targetHeight = isCrouching
            ? standingColliderSize.y * crouchColliderScaleY
            : standingColliderSize.y;

        // Keep the bottom of the collider fixed so it never pushes down into the floor
        float bottomY = standingColliderCenter.y - (standingColliderSize.y * 0.5f);
        float targetCenterY = bottomY + (targetHeight * 0.5f);

        Vector3 targetSize = new Vector3(standingColliderSize.x, targetHeight, standingColliderSize.z);
        Vector3 targetCenter = new Vector3(standingColliderCenter.x, targetCenterY, standingColliderCenter.z);

        boxCol.size = Vector3.Lerp(boxCol.size, targetSize, Time.deltaTime * crouchTransitionSpeed);
        boxCol.center = Vector3.Lerp(boxCol.center, targetCenter, Time.deltaTime * crouchTransitionSpeed);
    }

    void FixedUpdate()
    {
        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        Vector3 forwardAxis = transform.forward * (-moveHorizontal);
        Vector3 rightAxis = transform.right * moveVertical;

        Vector3 moveDir = (forwardAxis + rightAxis).normalized;
        bool isMoving = moveDir.magnitude > 0.01f;

        float currentSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchMoveSpeed;
        }
        else if (isSprinting && isMoving)
        {
            currentSpeed = moveSpeed * sprintSpeedMultiplier;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        rb.linearVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);

        if (conveyorVelocity.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity += new Vector3(conveyorVelocity.x, 0f, conveyorVelocity.z);
            conveyorVelocity = Vector3.zero;
        }

        AnimateLimbs(isMoving, isSprinting && isMoving);
    }

    public void ApplyConveyorPush(Vector3 pushVelocity)
    {
        conveyorVelocity += new Vector3(pushVelocity.x, 0f, pushVelocity.z);
    }

    void AnimateLimbs(bool isMoving, bool isSprintingNow)
    {
        if (isCrouching)
        {
            float sweep01 = 0f;
            float armSwing = 0f;

            if (isMoving)
            {
                walkTimer += Time.fixedDeltaTime * wiperSpeed;
                // Smoothly oscillates 0 -> 1 -> 0 -> 1 so Y goes 0 -> maxWiperAngle -> 0
                sweep01 = Mathf.Abs(Mathf.Sin(walkTimer));
                armSwing = Mathf.Sin(walkTimer) * 10f;
            }
            else
            {
                walkTimer = 0f;
            }

            SmoothRotateLimb(Arm_1, arm1StartRot * Quaternion.Euler(0f, 0f, crouchArmAngle + armSwing));
            SmoothRotateLimb(Arm_2, arm2StartRot * Quaternion.Euler(0f, 0f, -crouchArmAngle - armSwing));

            float leftY = sweep01 * maxWiperAngle;    // 0 -> +180 -> 0
            float rightY = sweep01 * maxWiperAngle;   // 0 -> +180 -> 0

            // Keep X at 0 while Y sweeps and Z stays at ±80
            SmoothEulerLimb(Leg_1, new Vector3(0f, leftY, leftLegCrouchZ));
            SmoothEulerLimb(Leg_2, new Vector3(0f, rightY, rightLegCrouchZ));
        }
        else if (isMoving)
        {
            float activeSpeed = isSprintingNow ? sprintSwingSpeed : swingSpeed;
            float activeAngle = isSprintingNow ? sprintSwingAngle : swingAngle;

            walkTimer += Time.fixedDeltaTime * activeSpeed;
            float angle = Mathf.Sin(walkTimer) * activeAngle;

            SmoothRotateLimb(Arm_1, arm1StartRot * Quaternion.Euler(0f, 0f, angle));
            SmoothRotateLimb(Arm_2, arm2StartRot * Quaternion.Euler(0f, 0f, -angle));
            SmoothRotateLimb(Leg_1, leg1StartRot * Quaternion.Euler(0f, 0f, -angle));
            SmoothRotateLimb(Leg_2, leg2StartRot * Quaternion.Euler(0f, 0f, angle));
        }
        else
        {
            walkTimer = 0f;
            SmoothRotateLimb(Arm_1, arm1StartRot);
            SmoothRotateLimb(Arm_2, arm2StartRot);
            SmoothRotateLimb(Leg_1, leg1StartRot);
            SmoothRotateLimb(Leg_2, leg2StartRot);
        }
    }

    void SmoothRotateLimb(Transform limb, Quaternion targetRot)
    {
        if (limb != null)
        {
            limb.localRotation = Quaternion.Lerp(limb.localRotation, targetRot, Time.fixedDeltaTime * 12f);
        }
    }

    void SmoothEulerLimb(Transform limb, Vector3 targetEuler)
    {
        if (limb == null) return;

        Vector3 current = limb.localEulerAngles;
        float t = Mathf.Clamp01(Time.fixedDeltaTime * 12f);

        float nextY = Mathf.LerpAngle(current.y, targetEuler.y, t);
        float nextZ = Mathf.LerpAngle(current.z, targetEuler.z, t);

        // X stays locked at 0 throughout crouching and crouch-walking
        limb.localEulerAngles = new Vector3(0f, nextY, nextZ);
    }

    void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;
    }
}