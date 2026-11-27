using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BatteryTrayDoorSystem : MonoBehaviour
{
    [System.Serializable]
    public class BatterySlot
    {
        public string slotName = "Slot";
        [Tooltip("Exact position & rotation where a snapped battery sits on the flat tray (e.g., Pivot_1).")]
        public Transform snapPoint;
        [Tooltip("How close a released/nearby battery must be to snap into this slot.")]
        public float snapRadius = 1.25f;

        [Header("Slot Ring Indicator (Lights Up White When Occupied)")]
        [Tooltip("Ring mesh around this pivot (e.g., Pivot_1_ring) that lights up white when a battery is attached.")]
        public Renderer slotIndicatorRing;
        [Tooltip("Optional point/spot light near this pivot ring.")]
        public Light slotIndicatorLight;
        [Min(0f)]
        public float ringEmissionIntensity = 2.5f;
        [Min(0f)]
        public float emptyRingEmissionIntensity = 0f;

        [HideInInspector] public Battery slottedBattery;
        [HideInInspector] public LiftableObject slottedLiftable;
        [HideInInspector] public Rigidbody slottedRb;
        [HideInInspector] public bool prevUseGravity;
        [HideInInspector] public bool prevIsKinematic;
        [HideInInspector] public RigidbodyConstraints prevConstraints;
        [HideInInspector] public RigidbodyInterpolation prevInterpolation;
        [HideInInspector] public bool lastRingLitState;
        [HideInInspector] public bool ringVisualInitialized;
    }

    [Header("Tray Snap Slots (3 Required)")]
    public List<BatterySlot> slots = new List<BatterySlot>(3);

    [Tooltip("Color applied to a slot's ring when a battery is attached.")]
    public Color attachedRingColor = Color.white;

    [Tooltip("Color applied to a slot's ring when no battery is attached.")]
    public Color emptyRingColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Tooltip("If true, batteries can be taken back out with Interact (F) when not locked.")]
    public bool allowRemovingBatteries = true;

    [Header("Door & Side Buttons")]
    [Tooltip("Drag your door GameObject here (moves up when triggered after power is full).")]
    public Transform doorTransform;

    [Tooltip("How high (in meters along +Y) the door opens.")]
    [Min(0f)]
    public float openHeight = 3.5f;

    [Min(0.01f)]
    public float doorMoveSpeed = 3.0f;

    [Tooltip("If true, pressing either side button toggles open/closed once powered; if false, it opens and stays open.")]
    public bool allowToggleClose = true;

    [Header("Optional Visual / Audio Events")]
    public UnityEvent onPowerReady;
    public UnityEvent onPowerLost;
    public UnityEvent onDoorOpened;
    public UnityEvent onDoorClosed;

    private bool isDoorPowered = false;
    private bool isDoorOpen = false;
    private bool doorClosedPosCached = false;
    private Vector3 doorClosedPos;
    private MaterialPropertyBlock ringPropBlock;
    private readonly List<Battery> scannedBatteries = new List<Battery>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsDoorPowered => isDoorPowered;
    public bool IsDoorOpen => isDoorOpen;

    public int SlottedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].slottedBattery != null) count++;
            }
            return count;
        }
    }

    public int FullBatteryCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                Battery b = slots[i].slottedBattery;
                if (b != null && b.IsFull) count++;
            }
            return count;
        }
    }

    void Awake()
    {
        ringPropBlock = new MaterialPropertyBlock();

        if (slots.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                slots.Add(new BatterySlot { slotName = $"Battery_Slot_{i + 1}", snapPoint = transform, snapRadius = 1.25f });
            }
        }

        CacheClosedDoorPosition(force: true);
        RefreshAllSlotRings(force: true);
    }

    void OnValidate()
    {
        openHeight = Mathf.Max(0f, openHeight);
        doorMoveSpeed = Mathf.Max(0.01f, doorMoveSpeed);

        if (ringPropBlock == null)
        {
            ringPropBlock = new MaterialPropertyBlock();
        }

        if (!Application.isPlaying)
        {
            RefreshAllSlotRings(force: true);
        }
    }

    public void CacheClosedDoorPosition(bool force = false)
    {
        if (doorClosedPosCached && !force) return;
        if (doorTransform != null)
        {
            doorClosedPos = doorTransform.position;
            doorClosedPosCached = true;
        }
    }

    void Update()
    {
        CheckForReleasedBatteriesToSnap();
        KeepSlottedBatteriesSeated();
        RefreshAllSlotRings(force: false);
        EvaluateDoorPowerState();
        AnimateDoor();
    }

    private void CheckForReleasedBatteriesToSnap()
    {
        Battery[] allBatteries = FindObjectsByType<Battery>(FindObjectsSortMode.None);
        scannedBatteries.Clear();
        scannedBatteries.AddRange(allBatteries);

        for (int i = 0; i < scannedBatteries.Count; i++)
        {
            Battery bat = scannedBatteries[i];
            if (bat == null) continue;
            if (IsBatteryAlreadySlotted(bat)) continue;

            LiftableObject lift = bat.GetComponent<LiftableObject>();
            // Only snap once the player lets go (not currently held or being pushed)
            if (lift != null && (lift.IsHeld || lift.IsBeingPushed)) continue;

            BatterySlot bestSlot = FindClosestFreeSlot(bat.transform.position);
            if (bestSlot != null)
            {
                SnapBatteryIntoSlot(bat, lift, bestSlot);
            }
        }
    }

    private BatterySlot FindClosestFreeSlot(Vector3 worldPos)
    {
        BatterySlot best = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < slots.Count; i++)
        {
            BatterySlot slot = slots[i];
            if (slot.slottedBattery != null || slot.snapPoint == null) continue;

            float distSqr = (worldPos - slot.snapPoint.position).sqrMagnitude;
            float maxR = Mathf.Max(0.1f, slot.snapRadius);
            if (distSqr <= maxR * maxR && distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = slot;
            }
        }

        return best;
    }

    private bool IsBatteryAlreadySlotted(Battery bat)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].slottedBattery == bat) return true;
        }
        return false;
    }

    public void SnapBatteryIntoSlot(Battery bat, LiftableObject lift, BatterySlot slot)
    {
        if (bat == null || slot == null || slot.snapPoint == null) return;

        Rigidbody rb = bat.GetComponent<Rigidbody>();
        slot.slottedBattery = bat;
        slot.slottedLiftable = lift;
        slot.slottedRb = rb;

        if (rb != null)
        {
            slot.prevUseGravity = rb.useGravity;
            slot.prevIsKinematic = rb.isKinematic;
            slot.prevConstraints = rb.constraints;
            slot.prevInterpolation = rb.interpolation;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        bat.transform.SetPositionAndRotation(slot.snapPoint.position, slot.snapPoint.rotation);
        ApplySlotRingVisual(slot, isAttached: true);
    }

    public void ReleaseBatteryFromSlot(BatterySlot slot)
    {
        if (slot == null || slot.slottedBattery == null) return;

        Battery bat = slot.slottedBattery;
        Rigidbody rb = slot.slottedRb;

        if (rb != null)
        {
            rb.isKinematic = slot.prevIsKinematic;
            rb.useGravity = slot.prevUseGravity;
            rb.constraints = slot.prevConstraints;
            rb.interpolation = slot.prevInterpolation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        slot.slottedBattery = null;
        slot.slottedLiftable = null;
        slot.slottedRb = null;

        // Nudge slightly forward from snap point so it doesn't immediately re-snap on the exact same frame
        if (slot.snapPoint != null)
        {
            bat.transform.position = slot.snapPoint.position + slot.snapPoint.forward * 0.35f;
        }

        ApplySlotRingVisual(slot, isAttached: false);
    }

    private void KeepSlottedBatteriesSeated()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            BatterySlot slot = slots[i];
            if (slot.slottedBattery == null || slot.snapPoint == null) continue;

            // If player picks it up again (if LiftableObject allows), release slot cleanly
            if (slot.slottedLiftable != null && slot.slottedLiftable.IsHeld)
            {
                slot.slottedBattery = null;
                slot.slottedLiftable = null;
                slot.slottedRb = null;
                ApplySlotRingVisual(slot, isAttached: false);
                continue;
            }

            slot.slottedBattery.transform.SetPositionAndRotation(slot.snapPoint.position, slot.snapPoint.rotation);
            if (slot.slottedRb != null && slot.slottedRb.isKinematic)
            {
                slot.slottedRb.linearVelocity = Vector3.zero;
                slot.slottedRb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void RefreshAllSlotRings(bool force)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            BatterySlot slot = slots[i];
            if (slot == null) continue;

            bool attached = slot.slottedBattery != null;
            if (force || !slot.ringVisualInitialized || slot.lastRingLitState != attached)
            {
                ApplySlotRingVisual(slot, attached);
            }
        }
    }

    private void ApplySlotRingVisual(BatterySlot slot, bool isAttached)
    {
        if (slot == null) return;

        slot.lastRingLitState = isAttached;
        slot.ringVisualInitialized = true;

        Color targetColor = isAttached ? attachedRingColor : emptyRingColor;
        float targetEmission = isAttached ? slot.ringEmissionIntensity : slot.emptyRingEmissionIntensity;

        if (slot.slotIndicatorRing != null)
        {
            if (ringPropBlock == null) ringPropBlock = new MaterialPropertyBlock();
            slot.slotIndicatorRing.GetPropertyBlock(ringPropBlock);

            Material mat = slot.slotIndicatorRing.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                {
                    ringPropBlock.SetColor(BaseColorId, targetColor);
                }
                if (mat.HasProperty(ColorId))
                {
                    ringPropBlock.SetColor(ColorId, targetColor);
                }
                if (mat.HasProperty(EmissionColorId))
                {
                    ringPropBlock.SetColor(EmissionColorId, targetColor * targetEmission);
                }
            }
            else
            {
                ringPropBlock.SetColor(BaseColorId, targetColor);
                ringPropBlock.SetColor(ColorId, targetColor);
                ringPropBlock.SetColor(EmissionColorId, targetColor * targetEmission);
            }

            slot.slotIndicatorRing.SetPropertyBlock(ringPropBlock);
        }

        if (slot.slotIndicatorLight != null)
        {
            slot.slotIndicatorLight.color = targetColor;
            slot.slotIndicatorLight.intensity = isAttached ? slot.ringEmissionIntensity : 0f;
        }
    }

    private void EvaluateDoorPowerState()
    {
        int requiredCount = Mathf.Max(1, slots.Count);
        bool allFull = (FullBatteryCount >= requiredCount);

        if (allFull && !isDoorPowered)
        {
            isDoorPowered = true;
            onPowerReady?.Invoke();
            Debug.Log("[BatteryTrayDoorSystem] All 3 batteries full! Door is powered (press either side button to open).");
        }
        else if (!allFull && isDoorPowered)
        {
            isDoorPowered = false;
            onPowerLost?.Invoke();
        }
    }

    /// <summary>
    /// Called by either side's TrayDoorButton. Does NOT drain battery energy.
    /// </summary>
    public void PressSideButton(string sideLabel = "Side Button")
    {
        if (!isDoorPowered)
        {
            Debug.Log($"[{sideLabel}] Door not powered yet ({FullBatteryCount}/{slots.Count} full batteries in tray).");
            return;
        }

        if (!isDoorOpen)
        {
            isDoorOpen = true;
            onDoorOpened?.Invoke();
            Debug.Log($"[{sideLabel}] Door opening!");
        }
        else if (allowToggleClose)
        {
            isDoorOpen = false;
            onDoorClosed?.Invoke();
            Debug.Log($"[{sideLabel}] Door closing!");
        }
    }

    private void AnimateDoor()
    {
        if (doorTransform == null) return;
        if (!doorClosedPosCached)
        {
            CacheClosedDoorPosition(force: true);
        }

        Vector3 doorOpenPos = doorClosedPos + Vector3.up * Mathf.Max(0f, openHeight);
        Vector3 targetPos = isDoorOpen ? doorOpenPos : doorClosedPos;
        doorTransform.position = Vector3.MoveTowards(doorTransform.position, targetPos, doorMoveSpeed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            BatterySlot slot = slots[i];
            if (slot == null || slot.snapPoint == null) continue;

            Gizmos.color = slot.slottedBattery != null && slot.slottedBattery.IsFull ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(slot.snapPoint.position, slot.snapRadius);
            Gizmos.DrawCube(slot.snapPoint.position, Vector3.one * 0.14f);
        }
    }
}