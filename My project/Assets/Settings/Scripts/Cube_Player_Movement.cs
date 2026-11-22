using System;
using System.Collections.Generic;
using UnityEngine;

public class Cube_Player_Movement : MonoBehaviour
{
    [Header("Movement & Jump")]
    public float moveSpeed = 6f;
    public float sprintSpeedMultiplier = 2f; // 2x walk speed when sprinting
    public float crouchMoveSpeed = 3f;
    public float jumpForce = 7f;
    public KeyCode crouchKey = KeyCode.LeftControl;
    public KeyCode altCrouchKey = KeyCode.C; // Backup key in case Ctrl+WASD hits a Unity Editor shortcut

    [Header("Camera Reference (Auto-Found if Empty)")]
    public ThirdPerson_Camera thirdPersonCamera;

    [Header("Crosshair Interaction (Press F)")]
    public KeyCode interactKey = KeyCode.F;
    public float interactRange = 12f;

    [Header("Safe Crouch Collider")]
    public float crouchColliderScaleY = 0.65f;
    public float crouchTransitionSpeed = 14f;

    [Header("Animator Settings")]
    public Animator animator;
    public string walkBoolParameter = "isWalking";
    public string crouchBoolParameter = "isCrouching";
    public string crouchWalkBoolParameter = "isCrouchWalking";
    public float walkAnimSpeed = 1f;        // Normal walking animation speed
    public float sprintAnimSpeed = 1.8f;    // Sped-up animation speed while holding Shift
    public float crouchAnimSpeed = 1.5f;    // Speed for crouching / crouch-idle animation
    public float crouchWalkAnimSpeed = 1.5f; // Speed for crouch-walking animation
    public float animSpeedSmooth = 10f;

    private Rigidbody rb;
    private BoxCollider boxCol;
    private bool isGrounded = true;
    private bool isCrouching = false;
    private bool isSprinting = false;
    private bool isMovingInput = false;
    private bool jumpQueued = false;

    private bool hasWalkParameter = false;
    private bool hasCrouchParameter = false;
    private bool hasCrouchWalkParameter = false;
    private string resolvedWalkParameter = "";
    private string resolvedCrouchParameter = "";
    private string resolvedCrouchWalkParameter = "";

    private bool loggedNegativeScale = false;
    private bool loggedLoopWarning = false;

    private float moveHorizontal = 0f;
    private float moveVertical = 0f;
    private float lastJumpTime = -10f;
    private float lastCeilingLogTime = -10f;
    private Vector3 conveyorVelocity = Vector3.zero;

    private readonly HashSet<int> collectedKeys = new HashSet<int>();
    private readonly HashSet<Collider> groundColliders = new HashSet<Collider>();

    private Vector3 standingColliderSize;
    private Vector3 standingColliderCenter;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = GetComponentInParent<Rigidbody>();
        }

        boxCol = GetComponent<BoxCollider>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        else
        {
            Debug.LogError("Cube_Player_Movement: No Rigidbody found on this player!");
        }

        if (thirdPersonCamera == null && Camera.main != null)
        {
            thirdPersonCamera = Camera.main.GetComponent<ThirdPerson_Camera>();
        }
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindFirstObjectByType<ThirdPerson_Camera>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            hasWalkParameter = TryResolveAnimatorParameter(
                AnimatorControllerParameterType.Bool,
                out resolvedWalkParameter,
                walkBoolParameter,
                "isWalking"
            );

            hasCrouchParameter = TryResolveAnimatorParameter(
                AnimatorControllerParameterType.Bool,
                out resolvedCrouchParameter,
                crouchBoolParameter,
                "isCrouching"
            );

            hasCrouchWalkParameter = TryResolveAnimatorParameter(
                AnimatorControllerParameterType.Bool,
                out resolvedCrouchWalkParameter,
                crouchWalkBoolParameter,
                "isCrouchWalking"
            );

            if (!hasWalkParameter)
            {
                Debug.LogWarning($"Cube_Player_Movement: Bool parameter '{walkBoolParameter}' was not found in the Animator Controller.");
            }
            if (!hasCrouchParameter)
            {
                Debug.LogWarning($"Cube_Player_Movement: Bool parameter '{crouchBoolParameter}' was not found in the Animator Controller.");
            }
            if (!hasCrouchWalkParameter)
            {
                Debug.LogWarning($"Cube_Player_Movement: Bool parameter '{crouchWalkBoolParameter}' was not found in the Animator Controller.");
            }
        }
        else
        {
            Debug.LogWarning("Cube_Player_Movement: No Animator found on this player or its children.");
        }

        if (boxCol != null)
        {
            standingColliderSize = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(boxCol.size.x)),
                Mathf.Max(0.01f, Mathf.Abs(boxCol.size.y)),
                Mathf.Max(0.01f, Mathf.Abs(boxCol.size.z))
            );
            boxCol.size = standingColliderSize;
            standingColliderCenter = boxCol.center;
        }

        CheckForNegativeScale();
    }

    // Intercepts Unity's built-in root motion so keeping Apply Root Motion checked
    // does not clamp vertical velocity or block jumping on the Rigidbody.
    void OnAnimatorMove()
    {
        // Intentionally empty: Rigidbody in FixedUpdate handles all movement and jumping.
    }

    bool TryResolveAnimatorParameter(
        AnimatorControllerParameterType paramType,
        out string matchedName,
        params string[] candidateNames)
    {
        matchedName = "";
        if (animator == null) return false;

        foreach (string candidate in candidateNames)
        {
            if (string.IsNullOrEmpty(candidate)) continue;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.type == paramType &&
                    string.Equals(param.name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    matchedName = param.name;
                    return true;
                }
            }
        }

        return false;
    }

    bool CheckForNegativeScale()
    {
        Vector3 worldScale = transform.lossyScale;
        bool hasNegative = worldScale.x < 0f || worldScale.y < 0f || worldScale.z < 0f;

        if (hasNegative && !loggedNegativeScale)
        {
            loggedNegativeScale = true;
            Transform culprit = FindNegativeScaleTransform(transform);
            Debug.LogError(
                $"Negative Scale detected on '{culprit.name}' (localScale = {culprit.localScale}). " +
                "BoxCollider cannot use negative scale. Set Scale X, Y, Z to positive values in the Inspector."
            );
        }

        return hasNegative;
    }

    Transform FindNegativeScaleTransform(Transform current)
    {
        Transform t = current;
        while (t != null)
        {
            if (t.localScale.x < 0f || t.localScale.y < 0f || t.localScale.z < 0f)
            {
                return t;
            }
            t = t.parent;
        }
        return current;
    }

    void Update()
    {
        // Clean up any disabled/destroyed ground colliders and update grounded state
        groundColliders.RemoveWhere(c => c == null || !c.enabled);
        isGrounded = groundColliders.Count > 0 && (Time.time - lastJumpTime > 0.15f);

        // Read W, A, S, D directly every frame
        moveHorizontal = 0f;
        moveVertical = 0f;

        if (Input.GetKey(KeyCode.W)) moveVertical += 1f;
        if (Input.GetKey(KeyCode.S)) moveVertical -= 1f;
        if (Input.GetKey(KeyCode.D)) moveHorizontal += 1f;
        if (Input.GetKey(KeyCode.A)) moveHorizontal -= 1f;

        isMovingInput = (moveHorizontal != 0f || moveVertical != 0f);

        bool ctrlHeld = Input.GetKey(crouchKey)
            || Input.GetKey(KeyCode.LeftControl)
            || Input.GetKey(KeyCode.RightControl)
            || (altCrouchKey != KeyCode.None && Input.GetKey(altCrouchKey));

        // Only crouch while on the ground; jumping plays Idle automatically
        isCrouching = ctrlHeld && isGrounded;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprinting = shiftHeld && !isCrouching;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isCrouching && !jumpQueued && rb != null)
        {
            jumpQueued = true;
        }

        if (Input.GetKeyDown(interactKey))
        {
            HandleCrosshairInteract();
        }

        UpdateCrouchCollider();
        UpdateAnimator(isMovingInput, isSprinting && isMovingInput);
    }

    void HandleCrosshairInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            return;
        }

        KeyPickup key = hit.collider.GetComponentInParent<KeyPickup>();
        if (key != null && !key.IsCollected)
        {
            key.TryCollect(this);
            return;
        }

        DoorLockSlot lockSlot = hit.collider.GetComponentInParent<DoorLockSlot>();
        if (lockSlot != null && !lockSlot.IsUnlocked)
        {
            lockSlot.TryUnlockWithPlayer(this);
        }
    }

    public void CollectKey(int keyId)
    {
        collectedKeys.Add(keyId);
    }

    public bool HasKey(int keyId)
    {
        return collectedKeys.Contains(keyId);
    }

    void UpdateCrouchCollider()
    {
        if (boxCol == null) return;
        if (CheckForNegativeScale()) return;

        float safeCrouchScale = Mathf.Clamp(Mathf.Abs(crouchColliderScaleY), 0.1f, 1f);
        float targetHeight = isCrouching
            ? standingColliderSize.y * safeCrouchScale
            : standingColliderSize.y;

        // Keep the bottom of the collider fixed so it never pushes down into the floor
        float bottomY = standingColliderCenter.y - (standingColliderSize.y * 0.5f);
        float targetCenterY = bottomY + (targetHeight * 0.5f);

        Vector3 targetSize = new Vector3(
            Mathf.Max(0.01f, standingColliderSize.x),
            Mathf.Max(0.01f, targetHeight),
            Mathf.Max(0.01f, standingColliderSize.z)
        );
        Vector3 targetCenter = new Vector3(standingColliderCenter.x, targetCenterY, standingColliderCenter.z);

        boxCol.size = Vector3.Lerp(boxCol.size, targetSize, Time.deltaTime * crouchTransitionSpeed);
        boxCol.center = Vector3.Lerp(boxCol.center, targetCenter, Time.deltaTime * crouchTransitionSpeed);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 forwardDir;
        Vector3 rightDir;

        if (thirdPersonCamera != null)
        {
            forwardDir = thirdPersonCamera.FlatForward;
            rightDir = thirdPersonCamera.FlatRight;
        }
        else
        {
            forwardDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            rightDir = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        }

        Vector3 moveDir = (forwardDir * moveVertical + rightDir * moveHorizontal).normalized;
        bool isMoving = moveDir.sqrMagnitude > 0.01f;
        bool isSprintingNow = isSprinting && isMoving;

        float currentSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchMoveSpeed;
        }
        else if (isSprintingNow)
        {
            currentSpeed = moveSpeed * sprintSpeedMultiplier;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        float nextYVelocity = rb.linearVelocity.y;

        if (jumpQueued)
        {
            jumpQueued = false;
            lastJumpTime = Time.time;
            groundColliders.Clear();
            isGrounded = false;
            isCrouching = false;

            rb.WakeUp();
            // Apply jump velocity directly so Rigidbody.mass never weakens jump height
            nextYVelocity = jumpForce;
        }
        else if (isMoving)
        {
            rb.WakeUp();
        }

        rb.linearVelocity = new Vector3(moveDir.x * currentSpeed, nextYVelocity, moveDir.z * currentSpeed);

        if (conveyorVelocity.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity += new Vector3(conveyorVelocity.x, 0f, conveyorVelocity.z);
            conveyorVelocity = Vector3.zero;
        }
    }

    void UpdateAnimator(bool isMoving, bool isSprintingNow)
    {
        if (animator == null) return;

        // While airborne (jumping), all three stay false so the Animator plays Idle
        bool isStandingWalking = isMoving && isGrounded && !isCrouching;
        bool isCrouchingOnGround = isCrouching && isGrounded;
        bool isCrouchWalking = isCrouchingOnGround && isMoving;

        if (hasWalkParameter)
        {
            animator.SetBool(resolvedWalkParameter, isStandingWalking);
        }

        if (hasCrouchParameter)
        {
            animator.SetBool(resolvedCrouchParameter, isCrouchingOnGround);
        }

        if (hasCrouchWalkParameter)
        {
            animator.SetBool(resolvedCrouchWalkParameter, isCrouchWalking);
        }

        float targetAnimSpeed = 1f;
        if (isCrouchWalking)
        {
            targetAnimSpeed = crouchWalkAnimSpeed;
        }
        else if (isCrouchingOnGround)
        {
            targetAnimSpeed = crouchAnimSpeed;
        }
        else if (isStandingWalking)
        {
            targetAnimSpeed = isSprintingNow ? sprintAnimSpeed : walkAnimSpeed;
        }

        // Fallback: keep looping Walk or Crouch Walk while WASD is held even if Loop Time was left unchecked
        if ((isStandingWalking || isCrouchWalking) && !animator.IsInTransition(0))
        {
            AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos.Length > 0 && clipInfos[0].clip != null && !clipInfos[0].clip.isLooping)
            {
                if (!loggedLoopWarning)
                {
                    loggedLoopWarning = true;
                    Debug.LogWarning($"Animation clip '{clipInfos[0].clip.name}' has Loop Time unchecked. Check Loop Time in the Inspector for smoother looping.");
                }

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 1f)
                {
                    animator.Play(stateInfo.fullPathHash, 0, 0f);
                }
            }
        }

        animator.speed = Mathf.Lerp(animator.speed, targetAnimSpeed, Time.deltaTime * animSpeedSmooth);
    }

    public void ApplyConveyorPush(Vector3 pushVelocity)
    {
        conveyorVelocity += new Vector3(pushVelocity.x, 0f, pushVelocity.z);
    }

    void UpdateGroundContact(Collision collision)
    {
        bool touchingFloor = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            float normalY = collision.GetContact(i).normal.y;
            if (normalY > 0.5f)
            {
                touchingFloor = true;
            }
            else if (normalY < -0.5f && Time.time - lastJumpTime < 0.35f && Time.time - lastCeilingLogTime > 0.5f)
            {
                lastCeilingLogTime = Time.time;
                Debug.LogWarning(
                    $"Jump blocked from above by '{collision.collider.name}'! " +
                    "Remove extra colliders on child body parts or move the overhead collider higher."
                );
            }
        }

        if (Time.time - lastJumpTime <= 0.15f) return;

        if (touchingFloor)
        {
            groundColliders.Add(collision.collider);
        }
        else
        {
            groundColliders.Remove(collision.collider);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        UpdateGroundContact(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        UpdateGroundContact(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        groundColliders.Remove(collision.collider);
    }
}