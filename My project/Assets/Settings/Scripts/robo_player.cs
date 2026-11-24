using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Cube_Player_Movement : MonoBehaviour
{
    [Header("Movement & Jump")]
    public float moveSpeed = 6f;
    public float sprintSpeedMultiplier = 2f; // 2x walk speed when sprinting
    public float crouchMoveSpeed = 3f;
    public float jumpForce = 7f;
    public KeyCode crouchKey = KeyCode.C;
    public KeyCode altCrouchKey = KeyCode.LeftControl;
    [Tooltip("Check this if W and S are swapped.")]
    public bool invertForwardBack = false;
    [Tooltip("Check this if A and D are swapped.")]
    public bool invertLeftRight = false;

    [Header("Mid-Air Control / Wall Slide")]
    [Range(0f, 1f)]
    [Tooltip("0 = no mid-air steering/velocity change (keeps air momentum until landing). 1 = full instant control like ground.")]
    public float airControllability = 0.25f;
    [Tooltip("How fast mid-air horizontal velocity blends toward target input when Air Controllability is between 0 and 1.")]
    public float airAccelerationRate = 8f;
    [Tooltip("Prevents pushing straight into a vertical wall mid-air from pinning/stalling the player until movement keys are released.")]
    public bool preventMidAirWallStick = true;
    [Tooltip("Optional frictionless/low-friction PhysicsMaterial applied to the player's BoxCollider to prevent snagging on walls.")]
    public PhysicsMaterial lowFrictionColliderMaterial;
    [Tooltip("How close to a vertical wall surface (in meters) mid-air push-into-wall is projected along the wall.")]
    public float wallProbeSkin = 0.08f;

    [Header("Camera Reference (Auto-Found if Empty)")]
    public ThirdPerson_Camera thirdPersonCamera;

    [Header("Crosshair Interaction (Press F)")]
    public KeyCode interactKey = KeyCode.F;
    public float interactRange = 12f;

    [Header("Left-Hand Ray Lift (Crosshair Aim)")]
    public float holdDistance = 2.5f;
    public float minHoldDistanceFromPlayer = 1.1f;
    public float shoulderPivotHeight = 1.35f;
    [Tooltip("Minimum allowed height relative to player feet so items don't clip through the floor.")]
    public float minHoldHeightAbovePlayer = 0.25f;
    [Tooltip("Maximum allowed height relative to player feet so items don't go too high up.")]
    public float maxHoldHeightAbovePlayer = 3.2f;
    [Tooltip("Layers that block/shorten the held item position along the reach vector.")]
    public LayerMask holdObstacleMask = ~0;

    [Header("Heavy Push / Pull (Right-Click Toggle)")]
    public float pushPullEngageRange = 4.5f;
    public float pushPullHoldGap = 2.1f;

    [Header("Throw Charge Settings")]
    public KeyCode chargeThrowKey = KeyCode.G;
    [Tooltip("How many seconds it takes to charge from 0% to 100% (e.g., 5 = 5 seconds, 10 = 10 seconds).")]
    public float secondsToMaxCharge = 5f;
    public float minThrowForce = 5f;
    public float maxThrowForce = 22f;

    [Header("Throw Charge Visual Bar")]
    [Tooltip("Width of the power bar in pixels. Updates dynamically in the Inspector.")]
    public float visualBarLength = 260f;
    public float visualBarHeight = 22f;
    public float barBottomMargin = 70f;
    [Tooltip("Gentle horizontal shake amount (pixels) when charge reaches 100%.")]
    public float maxChargeShakeAmplitude = 4f;
    [Tooltip("Speed of the gentle horizontal shake at 100% charge.")]
    public float maxChargeShakeFrequency = 28f;

    [Header("Safe Crouch Collider")]
    public float crouchColliderScaleY = 0.65f;
    public float crouchTransitionSpeed = 14f;

    [Header("Blender Character Animator")]
    [Tooltip("Drag the Animator component from the child Robo/robo_1 object here.")]
    public Animator roboAnimator;
    public string moveBoolParameter = "IsMoving";
    public string crouchBoolParameter = "IsCrouching";
    public string walkBoolParameter = "isWalking";
    public string crouchWalkBoolParameter = "isCrouchWalking";
    public float walkAnimSpeed = 1f;
    public float sprintAnimSpeed = 1.8f;
    public float crouchAnimSpeed = 1.5f;
    public float crouchWalkAnimSpeed = 1.5f;
    public float animSpeedSmooth = 10f;

    [Header("Procedural Limb Animation (Optional)")]
    [Tooltip("Keep OFF when using a model's own Blender/Animator clips.")]
    public bool useProceduralLimbAnimation = false;

    [Header("Limbs (Drag Pivots from Hierarchy)")]
    public Transform Arm_1;
    public Transform Arm_2;
    public Transform Leg_1; // Left leg pivot
    public Transform Leg_2; // Right leg pivot

    [Header("Standing Swing Settings")]
    public float swingSpeed = 10f;
    public float swingAngle = 35f;

    [Header("Sprint Swing Settings")]
    public float sprintSwingSpeed = 18f;
    public float sprintSwingAngle = 60f;

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
    private bool isMovingInput = false;
    private bool jumpQueued = false;
    private float walkTimer = 0f;
    private float lastJumpTime = -10f;
    private float lastCeilingLogTime = -10f;
    private Vector3 conveyorVelocity = Vector3.zero;
    private Vector3 lastWallNormal = Vector3.zero;
    private bool touchingVerticalWall = false;

    private LiftableObject heldObject = null;
    private LiftableObject pushedObject = null;

    public bool IsPushingOrPullingObject => pushedObject != null && pushedObject.IsBeingPushed;
    public Transform PushedObjectTransform => pushedObject != null ? pushedObject.transform : null;

    private bool isChargingThrow = false;
    private bool throwCancelled = false;
    private float chargeNormalized = 0f;

    // Runtime UI elements for the throw charge bar + fire emoji
    private GameObject chargeHudRoot;
    private RectTransform hudContainerRect;
    private RectTransform barTrackRect;
    private RectTransform barFillRect;
    private Image barFillImage;
    private Text fireEmojiText;
    private Vector2 hudBaseAnchoredPos;

    private readonly HashSet<int> collectedKeys = new HashSet<int>();
    private readonly HashSet<Collider> groundColliders = new HashSet<Collider>();

    private Vector3 standingColliderSize;
    private Vector3 standingColliderCenter;

    private Quaternion arm1StartRot;
    private Quaternion arm2StartRot;
    private Quaternion leg1StartRot;
    private Quaternion leg2StartRot;

    private bool hasMoveParam = false;
    private bool hasCrouchParam = false;
    private bool hasWalkParam = false;
    private bool hasCrouchWalkParam = false;
    private string resolvedMoveParam = "";
    private string resolvedCrouchParam = "";
    private string resolvedWalkParam = "";
    private string resolvedCrouchWalkParam = "";

    private bool loggedNegativeScale = false;
    private bool loggedLoopWarning = false;

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

        if (boxCol != null && lowFrictionColliderMaterial != null)
        {
            boxCol.sharedMaterial = lowFrictionColliderMaterial;
        }

        if (thirdPersonCamera == null && Camera.main != null)
        {
            thirdPersonCamera = Camera.main.GetComponent<ThirdPerson_Camera>();
        }
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindFirstObjectByType<ThirdPerson_Camera>();
        }

        if (roboAnimator == null)
        {
            roboAnimator = GetComponentInChildren<Animator>();
        }

        ResolveAnimatorParameters();

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

        if (Arm_1 != null) arm1StartRot = Arm_1.localRotation;
        if (Arm_2 != null) arm2StartRot = Arm_2.localRotation;
        if (Leg_1 != null) leg1StartRot = Leg_1.localRotation;
        if (Leg_2 != null) leg2StartRot = Leg_2.localRotation;

        CheckForNegativeScale();
        EnsureThrowChargeUI();
        HideThrowChargeUI();
    }

    // Prevents root motion on the child Animator from fighting Rigidbody velocity or blocking jumps.
    void OnAnimatorMove()
    {
        // Intentionally empty: Rigidbody in FixedUpdate handles movement and jumping.
    }

    void ResolveAnimatorParameters()
    {
        hasMoveParam = false;
        hasCrouchParam = false;
        hasWalkParam = false;
        hasCrouchWalkParam = false;

        if (roboAnimator == null) return;

        hasMoveParam = TryResolveParameter(AnimatorControllerParameterType.Bool, out resolvedMoveParam, moveBoolParameter, "IsMoving");
        hasCrouchParam = TryResolveParameter(AnimatorControllerParameterType.Bool, out resolvedCrouchParam, crouchBoolParameter, "IsCrouching");
        hasWalkParam = TryResolveParameter(AnimatorControllerParameterType.Bool, out resolvedWalkParam, walkBoolParameter, "isWalking");
        hasCrouchWalkParam = TryResolveParameter(AnimatorControllerParameterType.Bool, out resolvedCrouchWalkParam, crouchWalkBoolParameter, "isCrouchWalking");
    }

    bool TryResolveParameter(
        AnimatorControllerParameterType paramType,
        out string matchedName,
        params string[] candidates)
    {
        matchedName = "";
        if (roboAnimator == null) return false;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate)) continue;

            foreach (AnimatorControllerParameter param in roboAnimator.parameters)
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

    void OnValidate()
    {
        visualBarLength = Mathf.Max(40f, visualBarLength);
        visualBarHeight = Mathf.Max(8f, visualBarHeight);
        secondsToMaxCharge = Mathf.Max(0.1f, secondsToMaxCharge);
        minThrowForce = Mathf.Max(0f, minThrowForce);
        maxThrowForce = Mathf.Max(minThrowForce, maxThrowForce);
        maxChargeShakeAmplitude = Mathf.Max(0f, maxChargeShakeAmplitude);
        maxHoldHeightAbovePlayer = Mathf.Max(minHoldHeightAbovePlayer, maxHoldHeightAbovePlayer);
        holdDistance = Mathf.Max(minHoldDistanceFromPlayer, holdDistance);
        pushPullEngageRange = Mathf.Max(0.5f, pushPullEngageRange);
        pushPullHoldGap = Mathf.Max(0.8f, pushPullHoldGap);
        wallProbeSkin = Mathf.Max(0.01f, wallProbeSkin);
        airControllability = Mathf.Clamp01(airControllability);
        airAccelerationRate = Mathf.Max(0.1f, airAccelerationRate);

        if (Application.isPlaying && chargeHudRoot != null)
        {
            ApplyBarLayoutDimensions();
        }
    }

    void Update()
    {
        groundColliders.RemoveWhere(c => c == null || !c.enabled);
        if (groundColliders.Count > 0 && Time.time - lastJumpTime > 0.15f)
        {
            isGrounded = true;
        }

        float rawH = Input.GetAxisRaw("Horizontal");
        float rawV = Input.GetAxisRaw("Vertical");
        if (invertLeftRight) rawH = -rawH;
        if (invertForwardBack) rawV = -rawV;
        isMovingInput = Mathf.Abs(rawH) > 0.01f || Mathf.Abs(rawV) > 0.01f;

        bool crouchHeld =
            (crouchKey != KeyCode.None && Input.GetKey(crouchKey)) ||
            (altCrouchKey != KeyCode.None && Input.GetKey(altCrouchKey)) ||
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);

        isCrouching = crouchHeld && isGrounded;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprinting = shiftHeld && !isCrouching && pushedObject == null;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isCrouching && pushedObject == null && !jumpQueued && rb != null)
        {
            jumpQueued = true;
        }

        if (Input.GetKeyDown(interactKey))
        {
            HandleInteractKey();
        }

        HandleRightClickPushPullOrCancel();
        HandleThrowCharging();
        UpdateCrouchCollider();
    }

    void UpdateRoboAnimator(bool isMoving, bool isSprintingNow = false)
    {
        if (roboAnimator == null) return;

        bool isStandingWalking = isMoving && isGrounded && !isCrouching;
        bool isCrouchingOnGround = isCrouching && isGrounded;
        bool isCrouchWalking = isCrouchingOnGround && isMoving;

        if (hasMoveParam)
        {
            roboAnimator.SetBool(resolvedMoveParam, isMoving);
        }

        if (hasCrouchParam)
        {
            roboAnimator.SetBool(resolvedCrouchParam, isCrouchingOnGround);
        }

        if (hasWalkParam)
        {
            roboAnimator.SetBool(resolvedWalkParam, isStandingWalking);
        }

        if (hasCrouchWalkParam)
        {
            roboAnimator.SetBool(resolvedCrouchWalkParam, isCrouchWalking);
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

        // Loop fallback if a clip isn't flagged as looping in import settings
        if ((isStandingWalking || isCrouchWalking) && !roboAnimator.IsInTransition(0))
        {
            AnimatorClipInfo[] clipInfos = roboAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos.Length > 0 && clipInfos[0].clip != null && !clipInfos[0].clip.isLooping)
            {
                if (!loggedLoopWarning)
                {
                    loggedLoopWarning = true;
                    Debug.LogWarning($"Animation clip '{clipInfos[0].clip.name}' has Loop Time unchecked. Enable Loop Time in the FBX Animation tab for smooth looping.");
                }

                AnimatorStateInfo stateInfo = roboAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 1f)
                {
                    roboAnimator.Play(stateInfo.fullPathHash, 0, 0f);
                }
            }
        }

        roboAnimator.speed = Mathf.Lerp(roboAnimator.speed, targetAnimSpeed, Time.deltaTime * animSpeedSmooth);
    }

    void HandleRightClickPushPullOrCancel()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        if (isChargingThrow)
        {
            isChargingThrow = false;
            throwCancelled = true;
            chargeNormalized = 0f;
            HideThrowChargeUI();
            return;
        }

        if (pushedObject != null)
        {
            ReleasePushedObject();
            return;
        }

        LiftableObject candidate = FindHeavyPushCandidate();
        if (candidate != null)
        {
            if (heldObject != null)
            {
                heldObject.Drop();
                heldObject = null;
            }

            candidate.BeginPushPull(gameObject);
            pushedObject = candidate;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    LiftableObject FindHeavyPushCandidate()
    {
        Camera cam = thirdPersonCamera != null ? thirdPersonCamera.GetComponent<Camera>() : Camera.main;
        if (cam == null) cam = Camera.main;
        float maxCamDist = pushPullEngageRange + 6f;

        if (cam != null)
        {
            Ray camRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] camHits = Physics.RaycastAll(camRay, maxCamDist, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(camHits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < camHits.Length; i++)
            {
                Transform hitTr = camHits[i].transform;
                if (hitTr == transform || hitTr.IsChildOf(transform)) continue;

                LiftableObject obj = hitTr.GetComponentInParent<LiftableObject>();
                if (obj != null)
                {
                    float distToPlayer = Vector3.Distance(transform.position, obj.transform.position);
                    if (obj.CanBePushedOrPulled() && distToPlayer <= pushPullEngageRange)
                    {
                        return obj;
                    }
                    break;
                }

                break;
            }
        }

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.5f, shoulderPivotHeight * 0.75f);
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3[] candidateDirs = new Vector3[]
        {
            flatForward,
            -flatForward,
            cam != null ? new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized : flatForward
        };

        for (int d = 0; d < candidateDirs.Length; d++)
        {
            Vector3 dir = candidateDirs[d];
            if (dir.sqrMagnitude < 0.0001f) continue;

            RaycastHit[] sphereHits = Physics.SphereCastAll(origin, 0.65f, dir, pushPullEngageRange, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < sphereHits.Length; i++)
            {
                Transform hitTr = sphereHits[i].transform;
                if (hitTr == transform || hitTr.IsChildOf(transform)) continue;

                LiftableObject obj = hitTr.GetComponentInParent<LiftableObject>();
                if (obj != null && obj.CanBePushedOrPulled())
                {
                    return obj;
                }
            }
        }

        Collider[] nearby = Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, Mathf.Min(pushPullEngageRange, 3f), ~0, QueryTriggerInteraction.Ignore);
        LiftableObject closestHeavy = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < nearby.Length; i++)
        {
            Transform hitTr = nearby[i].transform;
            if (hitTr == transform || hitTr.IsChildOf(transform)) continue;

            LiftableObject obj = hitTr.GetComponentInParent<LiftableObject>();
            if (obj != null && obj.CanBePushedOrPulled())
            {
                float dist = Vector3.Distance(transform.position, obj.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    closestHeavy = obj;
                }
            }
        }

        return closestHeavy;
    }

    void ReleasePushedObject()
    {
        if (pushedObject != null)
        {
            pushedObject.EndPushPull();
            pushedObject = null;
        }
    }

    void HandleThrowCharging()
    {
        if (heldObject == null)
        {
            isChargingThrow = false;
            throwCancelled = false;
            chargeNormalized = 0f;
            HideThrowChargeUI();
            return;
        }

        if (Input.GetKeyDown(chargeThrowKey))
        {
            isChargingThrow = true;
            throwCancelled = false;
            chargeNormalized = 0f;
            ShowThrowChargeUI();
        }

        if (isChargingThrow && Input.GetKey(chargeThrowKey))
        {
            float chargeRate = 1f / Mathf.Max(0.1f, secondsToMaxCharge);
            chargeNormalized = Mathf.Clamp01(chargeNormalized + chargeRate * Time.deltaTime);
            UpdateThrowChargeUI(chargeNormalized);
        }

        if (Input.GetKeyUp(chargeThrowKey))
        {
            if (isChargingThrow && !throwCancelled)
            {
                float power = Mathf.Lerp(minThrowForce, maxThrowForce, chargeNormalized);
                Vector3 throwDir = GetThrowDirection();
                LiftableObject toThrow = heldObject;
                heldObject = null;
                isChargingThrow = false;
                chargeNormalized = 0f;
                HideThrowChargeUI();
                toThrow.Throw(throwDir, power);
            }
            else
            {
                isChargingThrow = false;
                throwCancelled = false;
                chargeNormalized = 0f;
                HideThrowChargeUI();
            }
        }
    }

    void EnsureThrowChargeUI()
    {
        if (chargeHudRoot != null) return;

        Canvas targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("ThrowCharge_Canvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        chargeHudRoot = new GameObject("ThrowCharge_HUD");
        chargeHudRoot.transform.SetParent(targetCanvas.transform, false);

        hudContainerRect = chargeHudRoot.AddComponent<RectTransform>();
        hudContainerRect.anchorMin = new Vector2(0.5f, 0f);
        hudContainerRect.anchorMax = new Vector2(0.5f, 0f);
        hudContainerRect.pivot = new Vector2(0.5f, 0f);

        HorizontalLayoutGroup layout = chargeHudRoot.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject emojiObj = new GameObject("FireEmoji_Left");
        emojiObj.transform.SetParent(hudContainerRect, false);
        fireEmojiText = emojiObj.AddComponent<Text>();
        fireEmojiText.text = "🔥";
        fireEmojiText.alignment = TextAnchor.MiddleCenter;
        fireEmojiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fireEmojiText.font == null)
        {
            fireEmojiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        fireEmojiText.resizeTextForBestFit = true;
        fireEmojiText.resizeTextMinSize = 14;
        fireEmojiText.resizeTextMaxSize = 36;

        RectTransform emojiRect = emojiObj.GetComponent<RectTransform>();
        emojiRect.sizeDelta = new Vector2(36f, 36f);

        GameObject trackObj = new GameObject("ChargeBar_Track");
        trackObj.transform.SetParent(hudContainerRect, false);
        barTrackRect = trackObj.AddComponent<RectTransform>();

        Image trackBg = trackObj.AddComponent<Image>();
        trackBg.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

        GameObject fillObj = new GameObject("ChargeBar_Fill");
        fillObj.transform.SetParent(barTrackRect, false);
        barFillRect = fillObj.AddComponent<RectTransform>();
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.pivot = new Vector2(0f, 0.5f);
        barFillRect.anchoredPosition = Vector2.zero;

        barFillImage = fillObj.AddComponent<Image>();
        barFillImage.color = new Color(1f, 0.55f, 0.1f, 1f);

        ApplyBarLayoutDimensions();
    }

    void ApplyBarLayoutDimensions()
    {
        if (hudContainerRect == null || barTrackRect == null || barFillRect == null) return;

        float emojiWidth = fireEmojiText != null ? fireEmojiText.rectTransform.sizeDelta.x + 10f : 0f;
        float totalWidth = visualBarLength + emojiWidth;
        float totalHeight = Mathf.Max(visualBarHeight, 36f);

        hudContainerRect.sizeDelta = new Vector2(totalWidth, totalHeight);
        hudBaseAnchoredPos = new Vector2(0f, barBottomMargin);
        hudContainerRect.anchoredPosition = hudBaseAnchoredPos;

        barTrackRect.sizeDelta = new Vector2(visualBarLength, visualBarHeight);
        barFillRect.sizeDelta = new Vector2(visualBarLength * chargeNormalized, visualBarHeight);
    }

    void ShowThrowChargeUI()
    {
        EnsureThrowChargeUI();
        if (chargeHudRoot != null)
        {
            chargeHudRoot.SetActive(true);
            ApplyBarLayoutDimensions();
            UpdateThrowChargeUI(0f);
        }
    }

    void HideThrowChargeUI()
    {
        if (chargeHudRoot != null)
        {
            if (hudContainerRect != null)
            {
                hudContainerRect.anchoredPosition = hudBaseAnchoredPos;
            }
            chargeHudRoot.SetActive(false);
        }
    }

    void UpdateThrowChargeUI(float progress01)
    {
        if (chargeHudRoot == null || !chargeHudRoot.activeSelf) return;

        if (barFillRect != null)
        {
            barFillRect.sizeDelta = new Vector2(visualBarLength * progress01, visualBarHeight);
        }

        if (barFillImage != null)
        {
            Color lowColor = new Color(1f, 0.65f, 0.15f, 1f);
            Color maxColor = new Color(1f, 0.2f, 0.08f, 1f);
            barFillImage.color = Color.Lerp(lowColor, maxColor, progress01);
        }

        if (hudContainerRect != null)
        {
            float shakeOffsetX = 0f;
            if (progress01 >= 0.999f && maxChargeShakeAmplitude > 0f)
            {
                shakeOffsetX = Mathf.Sin(Time.time * maxChargeShakeFrequency) * maxChargeShakeAmplitude;
            }
            hudContainerRect.anchoredPosition = hudBaseAnchoredPos + new Vector2(shakeOffsetX, 0f);
        }
    }

    void HandleInteractKey()
    {
        if (pushedObject != null)
        {
            ReleasePushedObject();
            return;
        }

        if (heldObject != null)
        {
            heldObject.Drop();
            heldObject = null;
            isChargingThrow = false;
            chargeNormalized = 0f;
            HideThrowChargeUI();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
        {
            return;
        }

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable != null)
        {
            interactable.Interact(this);
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

    public void ToggleHoldObject(LiftableObject obj)
    {
        if (obj == null) return;

        if (pushedObject != null)
        {
            ReleasePushedObject();
        }

        if (heldObject == obj)
        {
            obj.Drop();
            heldObject = null;
            isChargingThrow = false;
            chargeNormalized = 0f;
            HideThrowChargeUI();
            return;
        }

        if (heldObject != null)
        {
            heldObject.Drop();
            heldObject = null;
        }

        if (obj.CanBeLiftedByRay())
        {
            obj.PickUp(gameObject);
            heldObject = obj;
        }
    }

    Vector3 GetCrosshairHoldPoint()
    {
        Vector3 playerPivot = transform.position + Vector3.up * shoulderPivotHeight;
        Camera cam = Camera.main;

        Vector3 aimDir;
        if (cam != null)
        {
            Ray camRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 crosshairWorldTarget = camRay.origin + camRay.direction.normalized * (holdDistance + 3f);

            if (Physics.Raycast(camRay, out RaycastHit camHit, holdDistance + 5f, holdObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (camHit.transform != transform && !camHit.transform.IsChildOf(transform) &&
                    (heldObject == null || (camHit.transform != heldObject.transform && !camHit.transform.IsChildOf(heldObject.transform))))
                {
                    crosshairWorldTarget = camHit.point;
                }
            }

            aimDir = crosshairWorldTarget - playerPivot;
            if (aimDir.sqrMagnitude < 0.001f)
            {
                aimDir = camRay.direction;
            }
        }
        else
        {
            aimDir = transform.forward;
        }

        aimDir.Normalize();

        float maxReach = Mathf.Max(minHoldDistanceFromPlayer, holdDistance);
        float safeReach = maxReach;

        RaycastHit[] hits = Physics.RaycastAll(playerPivot, aimDir, maxReach, holdObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTr = hits[i].transform;
            if (hitTr == transform || hitTr.IsChildOf(transform)) continue;
            if (heldObject != null && (hitTr == heldObject.transform || hitTr.IsChildOf(heldObject.transform))) continue;

            float allowedDist = Mathf.Max(minHoldDistanceFromPlayer, hits[i].distance - 0.25f);
            if (allowedDist < safeReach)
            {
                safeReach = allowedDist;
            }
        }

        Vector3 point = playerPivot + aimDir * safeReach;

        float minY = transform.position.y + minHoldHeightAbovePlayer;
        float maxY = transform.position.y + maxHoldHeightAbovePlayer;
        point.y = Mathf.Clamp(point.y, minY, maxY);

        return point;
    }

    Vector3 GetThrowDirection()
    {
        if (Camera.main != null)
        {
            return Camera.main.transform.forward.normalized;
        }
        return (transform.forward + Vector3.up * 0.12f).normalized;
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

    private Vector3 ApplyAirWallSlide(Vector3 desiredHorizontalDir)
    {
        if (!preventMidAirWallStick || isGrounded || desiredHorizontalDir.sqrMagnitude < 0.0001f)
        {
            return desiredHorizontalDir;
        }

        Vector3 flatDir = new Vector3(desiredHorizontalDir.x, 0f, desiredHorizontalDir.z);
        if (flatDir.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }
        flatDir.Normalize();

        float castRadius = 0.25f;
        float castDist = wallProbeSkin + 0.15f;
        if (boxCol != null)
        {
            Vector3 worldExtents = Vector3.Scale(boxCol.size * 0.5f, transform.lossyScale);
            castRadius = Mathf.Max(0.1f, Mathf.Min(worldExtents.x, worldExtents.z) * 0.6f);
        }

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.35f, shoulderPivotHeight * 0.65f);
        RaycastHit[] hits = Physics.SphereCastAll(origin, castRadius, flatDir, castDist, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTr = hits[i].transform;
            if (hitTr == transform || hitTr.IsChildOf(transform)) continue;
            if (heldObject != null && (hitTr == heldObject.transform || hitTr.IsChildOf(heldObject.transform))) continue;

            Vector3 n = hits[i].normal;
            // Vertical wall normal has small Y component
            if (Mathf.Abs(n.y) < 0.35f)
            {
                Vector3 flatNormal = new Vector3(n.x, 0f, n.z);
                if (flatNormal.sqrMagnitude > 0.0001f)
                {
                    flatNormal.Normalize();
                    // If moving into the wall (dot < 0), project horizontal velocity onto the wall plane
                    float intoWall = Vector3.Dot(flatDir, flatNormal);
                    if (intoWall < 0f)
                    {
                        Vector3 slid = Vector3.ProjectOnPlane(flatDir, flatNormal);
                        return slid.magnitude > 0.001f ? slid.normalized * flatDir.magnitude : Vector3.zero;
                    }
                }
            }
        }

        if (touchingVerticalWall && lastWallNormal.sqrMagnitude > 0.0001f)
        {
            float intoWall = Vector3.Dot(flatDir, lastWallNormal);
            if (intoWall < 0f)
            {
                Vector3 slid = Vector3.ProjectOnPlane(flatDir, lastWallNormal);
                return slid.magnitude > 0.001f ? slid.normalized * flatDir.magnitude : Vector3.zero;
            }
        }

        return desiredHorizontalDir;
    }

    private Vector3 ComputeHorizontalVelocity(Vector3 desiredHorizontalVel, float dt)
    {
        if (isGrounded)
        {
            return desiredHorizontalVel;
        }

        float control = Mathf.Clamp01(airControllability);
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // 0 = zero mid-air control (preserve air momentum until landing)
        if (control <= 0.0001f)
        {
            return currentHorizontalVel;
        }

        // 1 = full instant control (current snapping behavior)
        if (control >= 0.9999f)
        {
            return desiredHorizontalVel;
        }

        // Partial control: blend toward target horizontal velocity at a rate scaled by airControllability
        float effectiveRate = airAccelerationRate * control;
        return Vector3.MoveTowards(currentHorizontalVel, desiredHorizontalVel, effectiveRate * dt);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        if (invertLeftRight) moveHorizontal = -moveHorizontal;
        if (invertForwardBack) moveVertical = -moveVertical;

        if (pushedObject != null)
        {
            if (!pushedObject.CanBePushedOrPulled() && !pushedObject.IsBeingPushed)
            {
                ReleasePushedObject();
            }
            else
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                float faceOffset = thirdPersonCamera != null
                    ? thirdPersonCamera.faceYawOffset
                    : (Camera.main != null && Camera.main.GetComponent<ThirdPerson_Camera>() != null
                        ? Camera.main.GetComponent<ThirdPerson_Camera>().faceYawOffset
                        : 90f);
                bool isPushMoving = Mathf.Abs(moveVertical) > 0.01f;
                pushedObject.StepPushPull(transform, moveVertical, moveHorizontal, pushPullHoldGap, faceOffset, Time.fixedDeltaTime);
                UpdateRoboAnimator(isPushMoving, false);
                if (useProceduralLimbAnimation)
                {
                    AnimateLimbs(isPushMoving, false);
                }
                return;
            }
        }

        Vector3 forwardDir;
        Vector3 rightDir;

        if (thirdPersonCamera != null)
        {
            forwardDir = thirdPersonCamera.FlatForward;
            rightDir = thirdPersonCamera.FlatRight;
        }
        else if (Camera.main != null)
        {
            forwardDir = Camera.main.transform.forward;
            rightDir = Camera.main.transform.right;
            forwardDir.y = 0f;
            rightDir.y = 0f;
            if (forwardDir.sqrMagnitude > 0.0001f) forwardDir.Normalize();
            if (rightDir.sqrMagnitude > 0.0001f) rightDir.Normalize();
        }
        else
        {
            forwardDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            rightDir = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        }

        Vector3 moveDir = (forwardDir * moveVertical + rightDir * moveHorizontal).normalized;
        moveDir = ApplyAirWallSlide(moveDir);

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
            nextYVelocity = jumpForce;
        }
        else if (isMoving)
        {
            rb.WakeUp();
        }

        Vector3 desiredHorizontalVel = new Vector3(moveDir.x * currentSpeed, 0f, moveDir.z * currentSpeed);
        Vector3 finalHorizontalVel = ComputeHorizontalVelocity(desiredHorizontalVel, Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(finalHorizontalVel.x, nextYVelocity, finalHorizontalVel.z);

        if (conveyorVelocity.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity += new Vector3(conveyorVelocity.x, 0f, conveyorVelocity.z);
            conveyorVelocity = Vector3.zero;
        }

        if (heldObject != null)
        {
            Vector3 desiredHoldPos = GetCrosshairHoldPoint();
            heldObject.MoveTowardHoldPoint(desiredHoldPos);
        }

        UpdateRoboAnimator(isMoving, isSprintingNow);

        if (useProceduralLimbAnimation)
        {
            AnimateLimbs(isMoving, isSprintingNow);
        }
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
                sweep01 = Mathf.Abs(Mathf.Sin(walkTimer));
                armSwing = Mathf.Sin(walkTimer) * 10f;
            }
            else
            {
                walkTimer = 0f;
            }

            SmoothRotateLimb(Arm_1, arm1StartRot * Quaternion.Euler(0f, 0f, crouchArmAngle + armSwing));
            SmoothRotateLimb(Arm_2, arm2StartRot * Quaternion.Euler(0f, 0f, -crouchArmAngle - armSwing));

            float leftY = sweep01 * maxWiperAngle;
            float rightY = sweep01 * maxWiperAngle;

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
            limb.localRotation = Quaternion.Lerp(limb.localRotation, targetRot, Time.deltaTime * 12f);
        }
    }

    void SmoothEulerLimb(Transform limb, Vector3 targetEuler)
    {
        if (limb == null) return;

        Vector3 current = limb.localEulerAngles;
        float t = Mathf.Clamp01(Time.deltaTime * 12f);

        float nextY = Mathf.LerpAngle(current.y, targetEuler.y, t);
        float nextZ = Mathf.LerpAngle(current.z, targetEuler.z, t);

        limb.localEulerAngles = new Vector3(0f, nextY, nextZ);
    }

    void UpdateGroundContact(Collision collision)
    {
        bool touchingFloor = false;
        bool foundWall = false;
        Vector3 bestWallNormal = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float normalY = normal.y;

            if (normalY > 0.5f)
            {
                touchingFloor = true;
            }
            else if (Mathf.Abs(normalY) < 0.35f)
            {
                foundWall = true;
                Vector3 flatN = new Vector3(normal.x, 0f, normal.z);
                if (flatN.sqrMagnitude > 0.0001f)
                {
                    bestWallNormal = flatN.normalized;
                }
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

        if (foundWall && !touchingFloor)
        {
            touchingVerticalWall = true;
            if (bestWallNormal.sqrMagnitude > 0.0001f)
            {
                lastWallNormal = bestWallNormal;
            }
        }
        else if (touchingFloor)
        {
            touchingVerticalWall = false;
        }

        if (Time.time - lastJumpTime <= 0.15f) return;

        if (touchingFloor)
        {
            groundColliders.Add(collision.collider);
            isGrounded = true;
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
        touchingVerticalWall = false;
    }
}