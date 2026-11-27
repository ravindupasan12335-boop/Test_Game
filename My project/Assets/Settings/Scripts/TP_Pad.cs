using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TeleportPad : MonoBehaviour
{
    [Header("Linked Pad (Works Both Ways)")]
    [Tooltip("Drag the other TeleportPad here. Set Pad A -> Pad B and Pad B -> Pad A.")]
    public TeleportPad targetPad;

    [Tooltip("Optional exact landing point on the destination pad. If empty, uses targetPad's position + landingHeightOffset.")]
    public Transform arrivalPoint;

    [Tooltip("Vertical lift above the destination pad so the player doesn't clip into the floor.")]
    public float landingHeightOffset = 0.25f;

    [Header("Teleport Timing")]
    [Tooltip("Seconds the player must stay on this pad before teleporting.")]
    [Min(0.1f)]
    public float chargeDuration = 3.0f;

    [Header("Separate White Light-Up Objects")]
    [Tooltip("Set how many objects should light up white when stepped on, then drag each mesh Renderer into its slot.")]
    public List<Renderer> lightUpRenderers = new List<Renderer>();

    [Tooltip("Optional Light component that turns on white when stepped on.")]
    public Light lightUpLight;

    [Tooltip("Color applied when active/charging.")]
    public Color activeWhiteColor = Color.white;

    [Tooltip("Color applied when idle/off.")]
    public Color offColor = Color.black;

    [Min(0f)]
    public float emissionIntensity = 2.5f;

    private Cube_Player_Movement activePlayer;
    private float chargeTimer = 0f;
    private bool lockUntilExit = false;
    private bool isTeleporting = false;
    private MaterialPropertyBlock propBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }

        if (lightUpRenderers.Count == 0)
        {
            Renderer selfRenderer = GetComponent<Renderer>();
            if (selfRenderer != null)
            {
                lightUpRenderers.Add(selfRenderer);
            }
        }

        SetLitState(false);
    }

    void OnValidate()
    {
        chargeDuration = Mathf.Max(0.1f, chargeDuration);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);

        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }

        if (!Application.isPlaying)
        {
            SetLitState(false);
        }
    }

    void Update()
    {
        if (activePlayer == null || isTeleporting) return;

        // If the player just arrived here via teleport, wait until they walk off before allowing another teleport
        if (lockUntilExit)
        {
            SetLitState(false);
            return;
        }

        SetLitState(true);
        chargeTimer += Time.deltaTime;

        if (chargeTimer >= chargeDuration)
        {
            StartCoroutine(TeleportRoutine(activePlayer));
        }
    }

    private Cube_Player_Movement FindPlayer(Collider other)
    {
        if (other == null) return null;

        Cube_Player_Movement player = other.GetComponentInParent<Cube_Player_Movement>();
        if (player != null) return player;

        return other.GetComponentInChildren<Cube_Player_Movement>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Cube_Player_Movement player = FindPlayer(other);
        if (player == null) return;

        if (activePlayer == null)
        {
            activePlayer = player;
            if (!lockUntilExit)
            {
                chargeTimer = 0f;
                SetLitState(true);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        Cube_Player_Movement player = FindPlayer(other);
        if (player == null) return;

        if (activePlayer == null)
        {
            activePlayer = player;
            if (!lockUntilExit)
            {
                chargeTimer = 0f;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Cube_Player_Movement player = FindPlayer(other);
        if (player == null || player != activePlayer) return;

        // Only reset if no remaining collider of this player is still inside this trigger
        Collider[] overlapping = Physics.OverlapBox(
            transform.position,
            transform.lossyScale * 0.5f,
            transform.rotation
        );

        for (int i = 0; i < overlapping.Length; i++)
        {
            if (FindPlayer(overlapping[i]) == player)
            {
                return;
            }
        }

        // Player stepped off: reset timer, turn off light, and clear arrival lock so walking back works again
        chargeTimer = 0f;
        lockUntilExit = false;
        activePlayer = null;
        SetLitState(false);
    }

    private IEnumerator TeleportRoutine(Cube_Player_Movement player)
    {
        isTeleporting = true;
        chargeTimer = 0f;
        SetLitState(false);

        if (targetPad == null || player == null)
        {
            isTeleporting = false;
            yield break;
        }

        Vector3 destination = GetDestinationPosition();

        // Lock the destination pad until the player steps off it
        targetPad.ReceiveArrivingPlayer(player);

        // Clear current pad state
        activePlayer = null;
        lockUntilExit = false;

        CharacterController cc = player.GetComponent<CharacterController>();
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = destination;
        }

        player.transform.position = destination;

        if (cc != null) cc.enabled = true;

        yield return new WaitForFixedUpdate();

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        isTeleporting = false;
    }

    public void ReceiveArrivingPlayer(Cube_Player_Movement player)
    {
        activePlayer = player;
        chargeTimer = 0f;
        lockUntilExit = true;
        SetLitState(false);
    }

    private Vector3 GetDestinationPosition()
    {
        if (targetPad.arrivalPoint != null)
        {
            return targetPad.arrivalPoint.position + Vector3.up * targetPad.landingHeightOffset;
        }

        return targetPad.transform.position + Vector3.up * targetPad.landingHeightOffset;
    }

    private void SetLitState(bool isLit)
    {
        Color col = isLit ? activeWhiteColor : offColor;
        float emission = isLit ? emissionIntensity : 0f;

        if (lightUpRenderers != null)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < lightUpRenderers.Count; i++)
            {
                Renderer rend = lightUpRenderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, col);
                propBlock.SetColor(ColorId, col);
                propBlock.SetColor(EmissionColorId, col * emission);
                rend.SetPropertyBlock(propBlock);
            }
        }

        if (lightUpLight != null)
        {
            lightUpLight.color = activeWhiteColor;
            lightUpLight.intensity = isLit ? emissionIntensity : 0f;
            lightUpLight.enabled = isLit;
        }
    }
}