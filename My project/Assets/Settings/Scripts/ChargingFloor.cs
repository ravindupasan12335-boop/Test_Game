using System.Collections.Generic;
using UnityEngine;

public class ChargingFloor : MonoBehaviour
{
    [Header("Round Pad Detection Zone")]
    [Tooltip("Horizontal radius (in world units) around the center of the round floor where batteries can charge.")]
    [Min(0.1f)]
    public float chargeRadius = 2.0f;

    [Tooltip("Minimum height above the floor's Y position for a battery to count as sitting on it.")]
    public float minHeightAboveFloor = -0.1f;

    [Tooltip("Maximum height above the floor's Y position (vertical range) for a battery to charge.")]
    [Min(0.1f)]
    public float maxHeightAboveFloor = 1.25f;

    [Tooltip("Optional LayerMask filter for battery objects. Leave as Everything if batteries have no special layer.")]
    public LayerMask batterySearchMask = ~0;

    [Header("Charging Rate")]
    [Tooltip("How many energy units are added per second while charging.")]
    [Min(0.1f)]
    public float energyPerSecond = 10f;

    [Tooltip("If true, a battery currently held by the player won't charge until placed/dropped on the pad.")]
    public bool blockChargingWhileHeld = false;

    [Header("White Ring & Light Effects")]
    [Tooltip("Drag your single ring Renderer around the round floor here.")]
    public Renderer floorRingRenderer;

    [Tooltip("Optional Light components on the round floor / ring.")]
    public List<Light> floorLights = new List<Light>();

    public Color activeWhiteColor = Color.white;
    public Color idleRingColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Tooltip("Set true to tint the ring's base/albedo color when active.")]
    public bool tintRingBaseColor = true;

    public bool useEmission = true;

    [Min(0f)]
    public float activeEmissionIntensity = 3.0f;

    [Min(0f)]
    public float idleEmissionIntensity = 0.0f;

    [Min(0f)]
    public float activeLightIntensity = 2.5f;

    [Min(0f)]
    public float idleLightIntensity = 0.0f;

    [Tooltip("How fast the white ring and lights fade in/out. Set 0 for instant snap.")]
    [Min(0f)]
    public float lightFadeSpeed = 14f;

    [Header("Debug / Gizmos")]
    public bool drawRangeGizmos = true;

    private float chargeAccumulator = 0f;
    private float currentLightBlend = 0f; // 0 = idle, 1 = active white charging
    private MaterialPropertyBlock propBlock;
    private readonly HashSet<Battery> activeBatteries = new HashSet<Battery>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsChargingAnyBattery => activeBatteries.Count > 0;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (floorRingRenderer == null)
        {
            floorRingRenderer = GetComponentInChildren<Renderer>(true);
        }

        ApplyRingVisuals(0f);
    }

    void OnValidate()
    {
        chargeRadius = Mathf.Max(0.1f, chargeRadius);
        maxHeightAboveFloor = Mathf.Max(minHeightAboveFloor + 0.05f, maxHeightAboveFloor);
        energyPerSecond = Mathf.Max(0.1f, energyPerSecond);
        activeEmissionIntensity = Mathf.Max(0f, activeEmissionIntensity);
        idleEmissionIntensity = Mathf.Max(0f, idleEmissionIntensity);
        activeLightIntensity = Mathf.Max(0f, activeLightIntensity);
        idleLightIntensity = Mathf.Max(0f, idleLightIntensity);
        lightFadeSpeed = Mathf.Max(0f, lightFadeSpeed);
    }

    void Update()
    {
        FindBatteriesInVerticalRange();

        bool anyCharging = false;
        if (activeBatteries.Count > 0)
        {
            chargeAccumulator += energyPerSecond * Time.deltaTime;
            int wholeUnits = Mathf.FloorToInt(chargeAccumulator);

            foreach (Battery bat in activeBatteries)
            {
                if (bat == null) continue;
                if (!bat.IsFull)
                {
                    anyCharging = true;
                    if (wholeUnits > 0)
                    {
                        bat.AddEnergy(wholeUnits);
                    }
                }
            }

            if (wholeUnits > 0)
            {
                chargeAccumulator -= wholeUnits;
            }
        }
        else
        {
            chargeAccumulator = 0f;
        }

        float targetBlend = anyCharging ? 1f : 0f;
        if (lightFadeSpeed <= 0f)
        {
            currentLightBlend = targetBlend;
        }
        else
        {
            currentLightBlend = Mathf.MoveTowards(currentLightBlend, targetBlend, Time.deltaTime * lightFadeSpeed);
        }

        ApplyRingVisuals(currentLightBlend);
    }

    private void FindBatteriesInVerticalRange()
    {
        activeBatteries.Clear();

        float floorY = transform.position.y;
        float minY = floorY + minHeightAboveFloor;
        float maxY = floorY + maxHeightAboveFloor;
        float centerY = (minY + maxY) * 0.5f;
        float halfHeight = Mathf.Max(0.05f, (maxY - minY) * 0.5f);

        Vector3 center = new Vector3(transform.position.x, centerY, transform.position.z);
        Vector3 halfExtents = new Vector3(chargeRadius, halfHeight, chargeRadius);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, batterySearchMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            Battery bat = hits[i].GetComponentInParent<Battery>();
            if (bat == null) continue;

            if (blockChargingWhileHeld)
            {
                LiftableObject lift = bat.GetComponent<LiftableObject>();
                if (lift != null && lift.IsHeld) continue;
            }

            // Verify circular horizontal radius & vertical height range strictly
            Vector3 batPos = bat.transform.position;
            float dx = batPos.x - transform.position.x;
            float dz = batPos.z - transform.position.z;
            float flatDistSqr = dx * dx + dz * dz;

            if (flatDistSqr <= chargeRadius * chargeRadius && batPos.y >= minY && batPos.y <= maxY)
            {
                activeBatteries.Add(bat);
            }
        }
    }

    private void ApplyRingVisuals(float blend)
    {
        Color blendedColor = Color.Lerp(idleRingColor, activeWhiteColor, blend);
        float blendedEmission = Mathf.Lerp(idleEmissionIntensity, activeEmissionIntensity, blend);
        float blendedLight = Mathf.Lerp(idleLightIntensity, activeLightIntensity, blend);

        if (floorRingRenderer != null)
        {
            floorRingRenderer.GetPropertyBlock(propBlock);

            if (tintRingBaseColor)
            {
                if (floorRingRenderer.sharedMaterial != null && floorRingRenderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    propBlock.SetColor(BaseColorId, blendedColor);
                }
                if (floorRingRenderer.sharedMaterial != null && floorRingRenderer.sharedMaterial.HasProperty(ColorId))
                {
                    propBlock.SetColor(ColorId, blendedColor);
                }
            }

            if (useEmission)
            {
                if (floorRingRenderer.sharedMaterial != null && floorRingRenderer.sharedMaterial.HasProperty(EmissionColorId))
                {
                    propBlock.SetColor(EmissionColorId, activeWhiteColor * blendedEmission);
                }
            }

            floorRingRenderer.SetPropertyBlock(propBlock);
        }

        for (int i = 0; i < floorLights.Count; i++)
        {
            Light l = floorLights[i];
            if (l == null) continue;
            l.color = activeWhiteColor;
            l.intensity = blendedLight;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawRangeGizmos) return;

        float floorY = transform.position.y;
        float minY = floorY + minHeightAboveFloor;
        float maxY = floorY + maxHeightAboveFloor;

        Gizmos.color = new Color(1f, 1f, 0.2f, 0.7f);
        DrawCircleGizmo(new Vector3(transform.position.x, minY, transform.position.z), chargeRadius, 36);

        Gizmos.color = new Color(0.2f, 1f, 1f, 0.85f);
        DrawCircleGizmo(new Vector3(transform.position.x, maxY, transform.position.z), chargeRadius, 36);
    }

    private void DrawCircleGizmo(Vector3 origin, float radius, int segments)
    {
        float step = (Mathf.PI * 2f) / segments;
        Vector3 prev = origin + new Vector3(Mathf.Cos(0f) * radius, 0f, Mathf.Sin(0f) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}