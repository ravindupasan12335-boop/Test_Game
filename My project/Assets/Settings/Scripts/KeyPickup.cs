using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    [Header("Key Settings")]
    [Tooltip("Match this Key ID (e.g., 1, 2, 3, 4) to the corresponding lock on the door.")]
    public int keyId = 1;

    [Header("Optional Idle Spin & Bob")]
    public bool spinAndBob = true;
    public float spinSpeed = 70f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2.5f;

    private Vector3 startPos;
    private bool collected = false;

    public bool IsCollected => collected;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (collected || !spinAndBob) return;

        transform.Rotate(Vector3.up * (spinSpeed * Time.deltaTime), Space.World);
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }

    public bool TryCollect(Cube_Player_Movement player)
    {
        if (collected || player == null) return false;

        collected = true;
        player.CollectKey(keyId);
        Debug.Log($"Grabbed Key #{keyId} with [F]!");
        gameObject.SetActive(false);
        return true;
    }
}