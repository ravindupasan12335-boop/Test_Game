using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiftableObject))]
public class Battery : MonoBehaviour
{
    [Header("Energy & Step Resolution")]
    [Min(1)]
    [Tooltip("Total energy units / color steps (e.g. 50 = smooth 50-step shift, 5 = chunky stepped jumps).")]
    public int maxEnergy = 50;

    [Min(0)]
    [Tooltip("Starting energy value.")]
    public int currentEnergy = 0;

    [Header("Color Gradient Anchors")]
    public Color emptyColor = Color.red;
    public Color halfColor = Color.yellow;
    public Color fullColor = Color.green;

    [Header("3 Ring Visuals")]
    [Tooltip("Drag your 3 ring objects (or their child renderers) here. Auto-finds child renderers if left empty.")]
    public List<Renderer> ringRenderers = new List<Renderer>();

    [Tooltip("Optional Light components on the battery rings/body.")]
    public List<Light> batteryLights = new List<Light>();

    [Header("Light Effect Tuning")]
    [Tooltip("Set true to apply the charge color to the rings. Set false if you only want emission/lights.")]
    public bool tintRingBaseColor = true;

    public bool useEmission = true;

    [Min(0f)]
    public float emissionIntensity = 2.0f;

    [Min(0f)]
    public float lightIntensity = 1.5f;

    [Tooltip("Smoothly interpolates visual color/light changes. Set 0 for instant step snaps.")]
    [Min(0f)]
    public float visualSmoothSpeed = 18f;

    private LiftableObject liftable;
    private MaterialPropertyBlock propBlock;
    private Color currentVisualColor;
    private int lastAppliedEnergy = -1;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public int CurrentEnergy => currentEnergy;
    public int MaxEnergy => Mathf.Max(1, maxEnergy);
    public bool IsFull => currentEnergy >= MaxEnergy;
    public bool IsEmpty => currentEnergy <= 0;
    public float NormalizedCharge => Mathf.Clamp01((float)currentEnergy / MaxEnergy);

    void Awake()
    {
        liftable = GetComponent<LiftableObject>();
        if (liftable != null)
        {
            liftable.weight = 2.5f;
        }

        propBlock = new MaterialPropertyBlock();

        if (ringRenderers.Count == 0)
        {
            // Collect child renderers excluding the main body if named "Body" or "Capsule"
            Renderer[] found = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in found)
            {
                string lower = r.gameObject.name.ToLower();
                if (!lower.Contains("capsule") && !lower.Contains("body"))
                {
                    ringRenderers.Add(r);
                }
            }
        }

        maxEnergy = Mathf.Max(1, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
        currentVisualColor = EvaluateSteppedEnergyColor(currentEnergy);
        ApplyVisuals(currentVisualColor, instant: true);
        lastAppliedEnergy = currentEnergy;
    }

    void OnValidate()
    {
        maxEnergy = Mathf.Max(1, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        visualSmoothSpeed = Mathf.Max(0f, visualSmoothSpeed);

        if (Application.isPlaying)
        {
            ApplyVisuals(EvaluateSteppedEnergyColor(currentEnergy), instant: true);
        }
    }

    void Update()
    {
        Color targetColor = EvaluateSteppedEnergyColor(currentEnergy);

        if (visualSmoothSpeed <= 0f)
        {
            currentVisualColor = targetColor;
        }
        else
        {
            currentVisualColor = Color.Lerp(currentVisualColor, targetColor, Time.deltaTime * visualSmoothSpeed);
        }

        if (currentEnergy != lastAppliedEnergy || visualSmoothSpeed > 0f)
        {
            ApplyVisuals(currentVisualColor, instant: visualSmoothSpeed <= 0f);
            lastAppliedEnergy = currentEnergy;
        }
    }

    /// <summary>
    /// Evaluates color using discrete steps from 0..maxEnergy so maxEnergy=50 gives 50 steps
    /// and maxEnergy=5 gives 5 coarse steps.
    /// </summary>
    public Color EvaluateSteppedEnergyColor(int energyValue)
    {
        int clamped = Mathf.Clamp(energyValue, 0, MaxEnergy);
        float t = (float)clamped / MaxEnergy; // Quantized to maxEnergy steps

        if (t <= 0.5f)
        {
            float halfT = t * 2f; // 0.0 -> 1.0 from empty (0) to half (maxEnergy/2)
            return Color.Lerp(emptyColor, halfColor, halfT);
        }
        else
        {
            float fullT = (t - 0.5f) * 2f; // 0.0 -> 1.0 from half to full (maxEnergy)
            return Color.Lerp(halfColor, fullColor, fullT);
        }
    }

    public bool AddEnergy(int amount)
    {
        if (amount <= 0 || IsFull) return false;

        int previous = currentEnergy;
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0, MaxEnergy);
        return currentEnergy != previous;
    }

    public bool ConsumeEnergy(int amount)
    {
        if (amount <= 0 || currentEnergy < amount) return false;

        currentEnergy = Mathf.Clamp(currentEnergy - amount, 0, MaxEnergy);
        return true;
    }

    public void SetEnergy(int value)
    {
        currentEnergy = Mathf.Clamp(value, 0, MaxEnergy);
    }

    private void ApplyVisuals(Color col, bool instant)
    {
        for (int i = 0; i < ringRenderers.Count; i++)
        {
            Renderer rend = ringRenderers[i];
            if (rend == null) continue;

            rend.GetPropertyBlock(propBlock);

            if (tintRingBaseColor)
            {
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
                {
                    propBlock.SetColor(BaseColorId, col);
                }
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(ColorId))
                {
                    propBlock.SetColor(ColorId, col);
                }
            }

            if (useEmission)
            {
                Color emissive = col * emissionIntensity;
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(EmissionColorId))
                {
                    propBlock.SetColor(EmissionColorId, emissive);
                }
            }

            rend.SetPropertyBlock(propBlock);
        }

        for (int i = 0; i < batteryLights.Count; i++)
        {
            Light l = batteryLights[i];
            if (l == null) continue;

            l.color = col;
            float chargeRatio = NormalizedCharge;
            l.intensity = chargeRatio > 0.01f ? lightIntensity * Mathf.Lerp(0.35f, 1f, chargeRatio) : lightIntensity * 0.15f;
        }
    }
}