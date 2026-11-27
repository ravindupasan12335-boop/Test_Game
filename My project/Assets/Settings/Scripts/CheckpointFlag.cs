using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointFlag : MonoBehaviour, IInteractable
{
    [Header("Respawn Location")]
    [Tooltip("Optional exact spot where the player respawns. If empty, uses a point slightly in front/above this flag.")]
    public Transform respawnPoint;

    [Tooltip("Used if Respawn Point is empty so the player doesn't spawn inside the flag mesh.")]
    public Vector3 fallbackSpawnOffset = new Vector3(0f, 0.25f, -1.2f);

    [Header("Flag Visual Feedback")]
    [Tooltip("Mesh renderer on the flag (or light strip) that changes color when saved.")]
    public Renderer flagRenderer;

    [Tooltip("Optional light on or near the flag.")]
    public Light flagLight;

    public Color inactiveColor = Color.white;
    public Color savedColor = Color.green;

    [Min(0f)]
    public float inactiveEmissionIntensity = 0f;

    [Min(0f)]
    public float savedEmissionIntensity = 2.5f;

    public bool IsSaved { get; private set; }

    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (flagRenderer == null)
        {
            flagRenderer = GetComponentInChildren<Renderer>();
        }
        SetSavedVisual(false);
    }

    void OnValidate()
    {
        inactiveEmissionIntensity = Mathf.Max(0f, inactiveEmissionIntensity);
        savedEmissionIntensity = Mathf.Max(0f, savedEmissionIntensity);
    }

    public void Interact(Cube_Player_Movement player)
    {
        CheckpointController controller = player != null
            ? player.GetComponent<CheckpointController>()
            : null;

        if (controller == null)
        {
            controller = FindFirstObjectByType<CheckpointController>();
        }

        if (controller != null)
        {
            controller.SaveCheckpoint(this);
        }
        else
        {
            Debug.LogWarning($"[{name}] No CheckpointController found in scene.");
        }
    }

    public Vector3 GetRespawnPosition()
    {
        if (respawnPoint != null)
        {
            return respawnPoint.position;
        }

        return transform.position + transform.TransformDirection(fallbackSpawnOffset);
    }

    public Quaternion GetRespawnRotation()
    {
        return respawnPoint != null ? respawnPoint.rotation : transform.rotation;
    }

    public void SetSavedVisual(bool saved)
    {
        IsSaved = saved;

        Color col = saved ? savedColor : inactiveColor;
        float emission = saved ? savedEmissionIntensity : inactiveEmissionIntensity;

        if (flagRenderer != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            flagRenderer.GetPropertyBlock(propBlock);

            propBlock.SetColor(BaseColorId, col);
            propBlock.SetColor(ColorId, col);
            propBlock.SetColor(EmissionColorId, col * emission);

            flagRenderer.SetPropertyBlock(propBlock);
        }

        if (flagLight != null)
        {
            flagLight.color = col;
            flagLight.intensity = saved ? savedEmissionIntensity : 0f;
        }
    }
}