using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Cube_Player_Movement : MonoBehaviour
{
    [Header("Movement & Jump")]
    public float moveSpeed = 6f;
    public float sprintSpeedMultiplier = 2f; // 2x walk speed when sprinting
    public float crouchMoveSpeed = 3f;
    public float jumpForce = 6f;
    public KeyCode crouchKey = KeyCode.C;
    [Tooltip("Check this if W and S are swapped.")]
    public bool invertForwardBack = false;
    [Tooltip("Check this if A and D are swapped.")]
    public bool invertLeftRight = false;

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
    [Tooltip("Drag the Animator component from the child Robo object here.")]
    public Animator roboAnimator;
    public string moveBoolParameter = "IsMoving";
    public string crouchBoolParameter = "IsCrouching";

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

        if (roboAnimator == null)
        {
            roboAnimator = GetComponentInChildren<Animator>();
        }

        if (boxCol != null)
        {
            standingColliderSize = boxCol.size;
            standingColliderCenter = boxCol.center;
        }

        if (Arm_1 != null) arm1StartRot = Arm_1.localRotation;
        if (Arm_2 != null) arm2StartRot = Arm_2.localRotation;
        if (Leg_1 != null) leg1StartRot = Leg_1.localRotation;
        if (Leg_2 != null) leg2StartRot = Leg_2.localRotation;

        EnsureThrowChargeUI();
        HideThrowChargeUI();
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

        if (Application.isPlaying && chargeHudRoot != null)
        {
            ApplyBarLayoutDimensions();
        }
    }

    void Update()
    {
        isCrouching = Input.GetKey(crouchKey) || Input.GetKey(KeyCode.LeftControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSprinting = shiftHeld && !isCrouching && pushedObject == null;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isCrouching && pushedObject == null)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        if (Input.GetKeyDown(interactKey))
        {
            HandleInteractKey();
        }

        HandleRightClickPushPullOrCancel();
        HandleThrowCharging();
        UpdateCrouchCollider();
    }

    void UpdateRoboAnimator(bool isMoving)
    {
        if (roboAnimator == null) return;

        if (!string.IsNullOrEmpty(moveBoolParameter))
        {
            roboAnimator.SetBool(moveBoolParameter, isMoving);
        }

        if (!string.IsNullOrEmpty(crouchBoolParameter))
        {
            roboAnimator.SetBool(crouchBoolParameter, isCrouching);
        }
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
            rb.linearVelocity = Vector3.zero;
        }
    }

    LiftableObject FindHeavyPushCandidate()
    {
        Camera cam = Camera.main;
        float maxCamDist = pushPullEngageRange + 6f;

        if (cam != null)
        {
            Ray camRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] camHits = Physics.RaycastAll(camRay, maxCamDist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(camHits, (a, b) => a.distance.CompareTo(b.distance));

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
            System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));

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

        Canvas targetCanvas = FindObjectOfType<Canvas>();
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

        float targetHeight = isCrouching
            ? standingColliderSize.y * crouchColliderScaleY
            : standingColliderSize.y;

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
                float faceOffset = Camera.main != null && Camera.main.GetComponent<ThirdPerson_Camera>() != null
                    ? Camera.main.GetComponent<ThirdPerson_Camera>().faceYawOffset
                    : 90f;
                bool isPushMoving = Mathf.Abs(moveVertical) > 0.01f;
                pushedObject.StepPushPull(transform, moveVertical, moveHorizontal, pushPullHoldGap, faceOffset, Time.fixedDeltaTime);
                UpdateRoboAnimator(isPushMoving);
                if (useProceduralLimbAnimation)
                {
                    AnimateLimbs(isPushMoving, false);
                }
                return;
            }
        }

        Vector3 camFwd = Camera.main != null ? Camera.main.transform.forward : transform.forward;
        Vector3 camRight = Camera.main != null ? Camera.main.transform.right : transform.right;

        camFwd.y = 0f;
        camRight.y = 0f;
        if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
        if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();

        Vector3 moveDir = (camFwd * moveVertical + camRight * moveHorizontal).normalized;
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

        if (heldObject != null)
        {
            Vector3 desiredHoldPos = GetCrosshairHoldPoint();
            heldObject.MoveTowardHoldPoint(desiredHoldPos);
        }

        UpdateRoboAnimator(isMoving);

        if (useProceduralLimbAnimation)
        {
            AnimateLimbs(isMoving, isSprinting && isMoving);
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

    void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;
    }
}