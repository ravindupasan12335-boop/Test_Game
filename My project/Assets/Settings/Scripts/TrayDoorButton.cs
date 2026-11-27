using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrayDoorButton : MonoBehaviour, IInteractable
{
    [Header("Button Setup")]
    public string sideName = "Left Button";
    public BatteryTrayDoorSystem doorSystem;

    [Header("Button Visual Color")]
    [Tooltip("Mesh renderer on the button that changes color/emission.")]
    public Renderer buttonRenderer;

    [Tooltip("Optional Light on or next to the button.")]
    public Light buttonLight;

    [Tooltip("Color when the door system is NOT powered yet.")]
    public Color unpoweredColor = Color.red;

    [Tooltip("Color when powered and ready to open (or closed).")]
    public Color readyColor = Color.yellow;

    [Tooltip("Color when the door is open.")]
    public Color openColor = Color.green;

    [Min(0f)]
    public float emissionIntensity = 2.0f;

    [Min(0f)]
    public float lightIntensity = 2.0f;

    private DoorButton pairedDoorButton;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        pairedDoorButton = GetComponent<DoorButton>();

        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }
        RefreshVisualColor();
    }

    void OnValidate()
    {
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lightIntensity = Mathf.Max(0f, lightIntensity);

        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }
        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }

        RefreshVisualColor();
    }

    void Update()
    {
        if (doorSystem == null)
        {
            doorSystem = FindFirstObjectByType<BatteryTrayDoorSystem>();
        }

        RefreshVisualColor();
    }

    public void RefreshVisualColor()
    {
        Color targetCol = unpoweredColor;

        if (doorSystem != null)
        {
            if (doorSystem.IsDoorOpen)
            {
                targetCol = openColor;
            }
            else if (doorSystem.IsDoorPowered)
            {
                targetCol = readyColor;
            }
        }

        if (buttonRenderer != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            buttonRenderer.GetPropertyBlock(propBlock);

            Material mat = buttonRenderer.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                {
                    propBlock.SetColor(BaseColorId, targetCol);
                }
                if (mat.HasProperty(ColorId))
                {
                    propBlock.SetColor(ColorId, targetCol);
                }
                if (mat.HasProperty(EmissionColorId))
                {
                    propBlock.SetColor(EmissionColorId, targetCol * emissionIntensity);
                }
            }
            else
            {
                propBlock.SetColor(BaseColorId, targetCol);
                propBlock.SetColor(ColorId, targetCol);
                propBlock.SetColor(EmissionColorId, targetCol * emissionIntensity);
            }

            buttonRenderer.SetPropertyBlock(propBlock);
        }

        if (buttonLight != null)
        {
            buttonLight.color = targetCol;
            buttonLight.intensity = lightIntensity;
        }
    }

    public void Interact(Cube_Player_Movement player)
    {
        if (pairedDoorButton == null)
        {
            pairedDoorButton = GetComponent<DoorButton>();
        }

        // If DoorButton is also on this object, let DoorButton coordinate both tray & roller doors
        if (pairedDoorButton != null)
        {
            pairedDoorButton.Interact(player);
            return;
        }

        Press();
    }

    public void Press()
    {
        if (doorSystem == null)
        {
            doorSystem = FindFirstObjectByType<BatteryTrayDoorSystem>();
        }

        if (doorSystem != null)
        {
            doorSystem.PressSideButton(sideName);
            RefreshVisualColor();
        }
        else
        {
            Debug.LogWarning($"[{sideName}] No BatteryTrayDoorSystem assigned.");
        }
    }
}