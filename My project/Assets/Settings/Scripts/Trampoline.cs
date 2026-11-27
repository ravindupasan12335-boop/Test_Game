using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Trampoline : MonoBehaviour
{
    public enum LaunchMode
    {
        SetUpwardVelocity,
        AddImpulse
    }

    [Header("Launch Settings")]
    [Tooltip("Upward velocity (if SetUpwardVelocity) or impulse force (if AddImpulse) applied on bounce.")]
    [Min(0f)]
    public float launchForce = 14f;

    [Tooltip("SetUpwardVelocity gives a consistent skyward launch regardless of fall speed; AddImpulse adds force on top.")]
    public LaunchMode launchMode = LaunchMode.SetUpwardVelocity;

    [Tooltip("If true, keeps existing X/Z horizontal velocity so moving onto the pad carries forward momentum.")]
    public bool preserveHorizontalVelocity = true;

    [Tooltip("Minimum time (in seconds) before the same object can be launched again.")]
    [Min(0.01f)]
    public float bounceCooldown = 0.15f;

    [Header("Vertical Trigger Zone")]
    [Tooltip("Vertical height (in meters) above the trampoline surface that counts as touching the bounce zone.")]
    [Min(0.05f)]
    public float triggerVerticalDistance = 0.5f;

    [Tooltip("Vertical offset from this object's transform position to the top/surface of the trampoline.")]
    public float surfaceYOffset = 0.25f;

    [Tooltip("Optional explicit trigger/pad bounds; if assigned, uses its X/Z footprint when checking objects.")]
    public Collider bounceAreaCollider;

    [Header("Optional Visual / Audio Feedback")]
    [Tooltip("Optional mesh renderer (such as a top pad or light strip) that flashes when triggered.")]
    public Renderer padRenderer;

    public Color idleColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color bounceColor = Color.white;

    [Min(0f)]
    public float bounceEmissionIntensity = 2.5f;

    [Tooltip("How long (seconds) the bounce color stays bright after launching something.")]
    [Min(0.01f)]
    public float flashDuration = 0.2f;

    public UnityEvent onBounce;

    private readonly Dictionary<EntityId, float> nextAllowedBounceTime = new Dictionary<EntityId, float>();
    private readonly Collider[] overlapResults = new Collider[32];
    private MaterialPropertyBlock propBlock;
    private float flashTimer = 0f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (padRenderer == null)
        {
            padRenderer = GetComponent<Renderer>();
        }
        if (bounceAreaCollider == null)
        {
            bounceAreaCollider = GetComponent<Collider>();
        }

        ApplyPadVisual(isBouncing: false);
    }

    void OnValidate()
    {
        launchForce = Mathf.Max(0f, launchForce);
        bounceCooldown = Mathf.Max(0.01f, bounceCooldown);
        triggerVerticalDistance = Mathf.Max(0.05f, triggerVerticalDistance);
        bounceEmissionIntensity = Mathf.Max(0f, bounceEmissionIntensity);
        flashDuration = Mathf.Max(0.01f, flashDuration);
    }

    void FixedUpdate()
    {
        CheckVerticalBounceZone();
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                ApplyPadVisual(isBouncing: false);
            }
        }
    }

    private void CheckVerticalBounceZone()
    {
        float surfaceY = transform.position.y + surfaceYOffset;
        float zoneMinY = surfaceY - 0.05f;
        float zoneMaxY = surfaceY + triggerVerticalDistance;
        float zoneHeight = Mathf.Max(0.05f, zoneMaxY - zoneMinY);
        float zoneCenterY = (zoneMinY + zoneMaxY) * 0.5f;

        Vector3 halfExtents;
        Vector3 center;

        if (bounceAreaCollider != null)
        {
            Bounds b = bounceAreaCollider.bounds;
            center = new Vector3(b.center.x, zoneCenterY, b.center.z);
            halfExtents = new Vector3(
                Mathf.Max(0.1f, b.extents.x),
                zoneHeight * 0.5f,
                Mathf.Max(0.1f, b.extents.z)
            );
        }
        else
        {
            Vector3 scale = transform.lossyScale;
            center = new Vector3(transform.position.x, zoneCenterY, transform.position.z);
            halfExtents = new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(scale.x) * 0.5f),
                zoneHeight * 0.5f,
                Mathf.Max(0.1f, Mathf.Abs(scale.z) * 0.5f)
            );
        }

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapResults, Quaternion.identity);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapResults[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                TryLaunchRigidbody(rb);
            }
        }
    }

    private void TryLaunchRigidbody(Rigidbody rb)
    {
        EntityId id = rb.GetEntityId();
        if (nextAllowedBounceTime.TryGetValue(id, out float allowedTime) && Time.time < allowedTime)
        {
            return;
        }

        // Only launch objects within the vertical distance band above the trampoline surface
        float surfaceY = transform.position.y + surfaceYOffset;
        float objY = rb.worldCenterOfMass.y;
        if (objY < surfaceY - 0.5f || objY > surfaceY + triggerVerticalDistance + 0.5f)
        {
            return;
        }

        nextAllowedBounceTime[id] = Time.time + bounceCooldown;

        Vector3 vel = rb.linearVelocity;
        if (!preserveHorizontalVelocity)
        {
            vel.x = 0f;
            vel.z = 0f;
        }

        if (launchMode == LaunchMode.SetUpwardVelocity)
        {
            vel.y = launchForce;
            rb.linearVelocity = vel;
        }
        else
        {
            rb.linearVelocity = vel;
            rb.AddForce(Vector3.up * launchForce, ForceMode.Impulse);
        }

        flashTimer = flashDuration;
        ApplyPadVisual(isBouncing: true);
        onBounce?.Invoke();
    }

    private void ApplyPadVisual(bool isBouncing)
    {
        if (padRenderer == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        padRenderer.GetPropertyBlock(propBlock);

        Color col = isBouncing ? bounceColor : idleColor;
        float emission = isBouncing ? bounceEmissionIntensity : 0f;

        propBlock.SetColor(BaseColorId, col);
        propBlock.SetColor(ColorId, col);
        propBlock.SetColor(EmissionColorId, col * emission);

        padRenderer.SetPropertyBlock(propBlock);
    }

    void OnDrawGizmosSelected()
    {
        float surfaceY = transform.position.y + surfaceYOffset;
        float zoneMinY = surfaceY - 0.05f;
        float zoneMaxY = surfaceY + triggerVerticalDistance;
        float zoneHeight = Mathf.Max(0.05f, zoneMaxY - zoneMinY);
        float zoneCenterY = (zoneMinY + zoneMaxY) * 0.5f;

        Vector3 size;
        Vector3 center;

        if (bounceAreaCollider != null)
        {
            Bounds b = bounceAreaCollider.bounds;
            center = new Vector3(b.center.x, zoneCenterY, b.center.z);
            size = new Vector3(Mathf.Max(0.2f, b.size.x), zoneHeight, Mathf.Max(0.2f, b.size.z));
        }
        else
        {
            Vector3 scale = transform.lossyScale;
            center = new Vector3(transform.position.x, zoneCenterY, transform.position.z);
            size = new Vector3(Mathf.Max(0.2f, Mathf.Abs(scale.x)), zoneHeight, Mathf.Max(0.2f, Mathf.Abs(scale.z)));
        }

        Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.35f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }
}