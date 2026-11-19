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

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (scrollMaterialTexture && rend != null && rend.sharedMaterial != null)
        {
            float offset = Time.time * textureScrollSpeed;
            rend.sharedMaterial.mainTextureOffset = new Vector2(0f, offset);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        Vector3 pushDir = transform.TransformDirection(localPushDirection).normalized;
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