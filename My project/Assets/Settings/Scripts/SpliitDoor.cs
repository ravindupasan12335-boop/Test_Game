using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SplitSideDoor : MonoBehaviour
{
    public enum SlideAxis
    {
        LocalX,
        LocalY,
        LocalZ,
        WorldX,
        WorldY,
        WorldZ
    }

    [Header("Two Door Panels (Left & Right)")]
    [Tooltip("Door panel that slides in the negative direction when opening.")]
    public Transform leftDoorPanel;

    [Tooltip("Door panel that slides in the positive direction when opening.")]
    public Transform rightDoorPanel;

    [Tooltip("Axis along which left and right panels slide apart.")]
    public SlideAxis slideAxis = SlideAxis.LocalX;

    [Tooltip("Distance each panel slides outward when opened.")]
    [Min(0.01f)]
    public float slideDistance = 1.75f;

    [Tooltip("How fast the two panels slide open and closed.")]
    [Min(0.01f)]
    public float moveSpeed = 3.5f;

    [Header("Behavior & Optional Power Link")]
    [Tooltip("If true, pressing either button toggles open/closed. If false, pressing opens it.")]
    public bool toggleOpenClose = true;

    [Tooltip("Optional: link a BatteryTrayDoorSystem if this door should only open once powered.")]
    public BatteryTrayDoorSystem requiredPowerSystem;

    [Header("Red / Green Indicator Lights")]
    [Tooltip("Optional indicator renderers (e.g., on the door frame or both side buttons) that turn Red when closed and Green when open.")]
    public List<Renderer> indicatorRenderers = new List<Renderer>();

    [Tooltip("Optional Light components that turn Red when closed and Green when open.")]
    public List<Light> indicatorLights = new List<Light>();

    public Color closedRedColor = Color.red;
    public Color openGreenColor = Color.green;

    public bool tintBaseColor = true;
    public bool useEmission = true;

    [Min(0f)]
    public float emissionIntensity = 2.5f;

    [Min(0f)]
    public float lightIntensity = 2.0f;

    [Header("Events")]
    public UnityEvent onDoorOpened;
    public UnityEvent onDoorClosed;

    private bool isOpen = false;
    private bool closedPositionsCached = false;
    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private MaterialPropertyBlock propBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsOpen => isOpen;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        CacheClosedPositions(force: true);
        ApplyIndicatorVisuals(isOpen);
    }

    void OnValidate()
    {
        slideDistance = Mathf.Max(0.01f, slideDistance);
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lightIntensity = Mathf.Max(0f, lightIntensity);

        if (!Application.isPlaying)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            ApplyIndicatorVisuals(isOpen);
        }
    }

    public void CacheClosedPositions(bool force = false)
    {
        if (closedPositionsCached && !force) return;

        if (leftDoorPanel != null)
        {
            leftClosedPos = leftDoorPanel.position;
        }

        if (rightDoorPanel != null)
        {
            rightClosedPos = rightDoorPanel.position;
        }

        closedPositionsCached = true;
    }

    private Vector3 GetSlideDirection()
    {
        switch (slideAxis)
        {
            case SlideAxis.LocalX: return transform.right.normalized;
            case SlideAxis.LocalY: return transform.up.normalized;
            case SlideAxis.LocalZ: return transform.forward.normalized;
            case SlideAxis.WorldX: return Vector3.right;
            case SlideAxis.WorldY: return Vector3.up;
            case SlideAxis.WorldZ: return Vector3.forward;
            default: return transform.right.normalized;
        }
    }

    void Update()
    {
        if (!closedPositionsCached)
        {
            CacheClosedPositions(force: true);
        }

        Vector3 dir = GetSlideDirection();
        float dist = Mathf.Max(0.01f, slideDistance);

        Vector3 targetLeft = isOpen ? leftClosedPos - dir * dist : leftClosedPos;
        Vector3 targetRight = isOpen ? rightClosedPos + dir * dist : rightClosedPos;

        if (leftDoorPanel != null)
        {
            leftDoorPanel.position = Vector3.MoveTowards(
                leftDoorPanel.position,
                targetLeft,
                moveSpeed * Time.deltaTime
            );
        }

        if (rightDoorPanel != null)
        {
            rightDoorPanel.position = Vector3.MoveTowards(
                rightDoorPanel.position,
                targetRight,
                moveSpeed * Time.deltaTime
            );
        }
    }

    public void PressSideButton(string buttonLabel = "Side Button")
    {
        if (requiredPowerSystem != null && !requiredPowerSystem.IsDoorPowered)
        {
            Debug.Log($"[{buttonLabel}] Door is not powered yet.");
            return;
        }

        if (!isOpen)
        {
            SetDoorOpen(true);
            Debug.Log($"[{buttonLabel}] Opened split door.");
        }
        else if (toggleOpenClose)
        {
            SetDoorOpen(false);
            Debug.Log($"[{buttonLabel}] Closed split door.");
        }
    }

    public void SetDoorOpen(bool open)
    {
        CacheClosedPositions(force: false);
        if (isOpen == open) return;
        isOpen = open;
        ApplyIndicatorVisuals(isOpen);

        if (isOpen)
        {
            onDoorOpened?.Invoke();
        }
        else
        {
            onDoorClosed?.Invoke();
        }
    }

    private void ApplyIndicatorVisuals(bool openState)
    {
        Color targetCol = openState ? openGreenColor : closedRedColor;

        for (int i = 0; i < indicatorRenderers.Count; i++)
        {
            Renderer rend = indicatorRenderers[i];
            if (rend == null) continue;

            rend.GetPropertyBlock(propBlock);

            if (tintBaseColor)
            {
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
                {
                    propBlock.SetColor(BaseColorId, targetCol);
                }
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(ColorId))
                {
                    propBlock.SetColor(ColorId, targetCol);
                }
            }

            if (useEmission)
            {
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(EmissionColorId))
                {
                    propBlock.SetColor(EmissionColorId, targetCol * emissionIntensity);
                }
            }

            rend.SetPropertyBlock(propBlock);
        }

        for (int i = 0; i < indicatorLights.Count; i++)
        {
            Light l = indicatorLights[i];
            if (l == null) continue;
            l.color = targetCol;
            l.intensity = lightIntensity;
        }
    }
}