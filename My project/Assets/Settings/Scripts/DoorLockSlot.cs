using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorLockSlot : MonoBehaviour
{
    [Header("Lock Settings")]
    [Tooltip("Must match the Key ID of the key that opens this lock.")]
    public int requiredKeyId = 1;

    [Header("Optional Visual Feedback")]
    public bool hideOnUnlock = true;
    public Material unlockedMaterial;

    public bool IsUnlocked { get; private set; } = false;

    private Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    public bool TryUnlockWithPlayer(Cube_Player_Movement player)
    {
        if (IsUnlocked || player == null) return false;

        if (!player.HasKey(requiredKeyId))
        {
            Debug.Log($"Need Key #{requiredKeyId} to unlock this lock!");
            return false;
        }

        IsUnlocked = true;

        if (unlockedMaterial != null && rend != null)
        {
            rend.sharedMaterial = unlockedMaterial;
        }

        if (hideOnUnlock)
        {
            gameObject.SetActive(false);
        }

        Debug.Log($"Unlocked Lock #{requiredKeyId} with [F]!");
        return true;
    }
}