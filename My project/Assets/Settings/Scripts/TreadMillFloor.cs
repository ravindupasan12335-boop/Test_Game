using UnityEngine;

public class TreadmillFloor : MonoBehaviour
{
    [Header("Push Direction & Strength")]
    // Local direction the treadmill pushes (e.g., Vector3.forward or -Vector3.forward)
    public Vector3 localPushDirection = Vector3.forward;
    public float pushSpeed = 8f; // Above walk speed (6), below sprint speed (12)

    [Header("Optional Visual Belt Scroll")]
    public bool scrollMaterialTexture = true;
    public float textureScrollSpeed = 1.5f;

    [Header("Time Rewind Sync")]
    [Tooltip("If true, automatically reverses push and belt scroll while holding the rewind key (X).")]
    public bool reverseDuringTimeRewind = true;

    private Renderer rend;
    private TimeRewindManager rewindManager;
    private float beltScrollPosition = 0f;

    void Start()
    {
        rend = GetComponent<Renderer>();
        rewindManager = FindFirstObjectByType<TimeRewindManager>();
    }

    private float GetDirectionSign()
    {
        if (rewindManager == null)
        {
            rewindManager = FindFirstObjectByType<TimeRewindManager>();
        }

        if (reverseDuringTimeRewind && rewindManager != null && rewindManager.IsRewinding)
        {
            return -1f;
        }

        return 1f;
    }

    void Update()
    {
        float sign = GetDirectionSign();

        if (scrollMaterialTexture && rend != null && rend.sharedMaterial != null)
        {
            beltScrollPosition += sign * textureScrollSpeed * Time.deltaTime;
            rend.sharedMaterial.mainTextureOffset = new Vector2(0f, beltScrollPosition);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        float sign = GetDirectionSign();
        Vector3 pushDir = transform.TransformDirection(localPushDirection).normalized * sign;
        Vector3 pushVel = pushDir * pushSpeed;

        Cube_Player_Movement playerMove = collision.collider.GetComponentInParent<Cube_Player_Movement>();
        if (playerMove != null)
        {
            playerMove.ApplyConveyorPush(pushVel);
            return;
        }

        Rigidbody rb = collision.rigidbody;
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(pushVel.x, rb.linearVelocity.y, pushVel.z);
        }
    }
}