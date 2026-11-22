using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LiftableObject : MonoBehaviour, IInteractable
{
    [Header("Weight & Ray Lift Limit")]
    public float weight = 2f;
    public float maxRayLiftWeight = 12f;

    [Header("Hold & Throw Tuning")]
    [Tooltip("Higher = snappier follow. Automatically scaled down for heavier objects.")]
    public float baseFollowSpeed = 14f;
    public float minWeightFactor = 0.25f;

    [Header("Heavy Push / Pull Tuning")]
    [Tooltip("Base speed when pushing forward or pulling backward.")]
    public float pushPullSpeed = 4.5f;
    [Tooltip("Degrees per second when rotating around the box's Y axis.")]
    public float pushTurnSpeed = 85f;
    [Tooltip("Minimum multiplier applied to push/pull and turn speed for very heavy boxes.")]
    public float minHeavyMoveFactor = 0.35f;
    [Tooltip("Layers treated as walls/obstacles that block the player from orbiting into them.")]
    public LayerMask orbitObstacleMask = ~0;

    private Rigidbody rb;
    private bool isHeld = false;
    private bool isBeingPushed = false;

    private float originalDrag;
    private float originalAngularDrag;
    private bool originalUseGravity;
    private RigidbodyInterpolation originalInterpolation;
    private RigidbodyConstraints originalConstraints;

    private Collider[] itemColliders;
    private readonly List<Collider> ignoredPlayerColliders = new List<Collider>();

    public bool IsHeld => isHeld;
    public bool IsBeingPushed => isBeingPushed;
    public float Weight => weight;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemColliders = GetComponentsInChildren<Collider>();
        rb.mass = Mathf.Max(0.1f, weight);

        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;
        originalUseGravity = rb.useGravity;
        originalInterpolation = rb.interpolation;
        originalConstraints = rb.constraints;
    }

    public bool CanBeLiftedByRay()
    {
        return !isHeld && !isBeingPushed && weight <= maxRayLiftWeight;
    }

    public bool CanBePushedOrPulled()
    {
        return !isHeld && weight > maxRayLiftWeight;
    }

    public void Interact(Cube_Player_Movement player)
    {
        if (player == null) return;
        player.ToggleHoldObject(this);
    }

    public void PickUp()
    {
        PickUp(null);
    }

    public void PickUp(GameObject playerObj)
    {
        if (isHeld || isBeingPushed || !CanBeLiftedByRay()) return;

        isHeld = true;
        rb.mass = Mathf.Max(0.1f, weight);
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 8f;
        rb.angularDamping = 8f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        SetPlayerCollisionIgnore(playerObj, true);
    }

    public void MoveTowardHoldPoint(Vector3 holdWorldPos)
    {
        if (!isHeld) return;

        float weightScale = GetWeightScale();
        float effectiveSpeed = baseFollowSpeed * weightScale;

        Vector3 toTarget = holdWorldPos - rb.position;
        rb.linearVelocity = toTarget * effectiveSpeed;
    }

    public void Drop()
    {
        if (!isHeld) return;

        isHeld = false;
        rb.useGravity = originalUseGravity;
        rb.interpolation = originalInterpolation;
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;

        SetPlayerCollisionIgnore(null, false);
    }

    public void Throw(Vector3 direction, float chargedForce)
    {
        if (!isHeld) return;

        Drop();
        float weightScale = GetWeightScale();
        float finalForce = chargedForce * weightScale;
        rb.AddForce(direction.normalized * finalForce, ForceMode.Impulse);
    }

    public void BeginPushPull(GameObject playerObj)
    {
        if (isHeld || isBeingPushed || !CanBePushedOrPulled()) return;

        isBeingPushed = true;
        rb.mass = Mathf.Max(0.1f, weight);
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 4f;
        rb.angularDamping = 6f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        SetPlayerCollisionIgnore(playerObj, true);
    }

    public void EndPushPull()
    {
        if (!isBeingPushed) return;

        isBeingPushed = false;
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        rb.interpolation = originalInterpolation;
        rb.constraints = originalConstraints;

        SetPlayerCollisionIgnore(null, false);
    }

    public void StepPushPull(Transform playerTransform, float forwardPullInput, float turnInput, float holdGap, float faceYawOffset, float fixedDelta)
    {
        if (!isBeingPushed || playerTransform == null) return;

        float heavyScale = GetHeavyMoveScale();
        float effectiveMoveSpeed = pushPullSpeed * heavyScale;
        float effectiveTurnSpeed = pushTurnSpeed * heavyScale;

        // Horizontal vector from box toward player defines the push/pull axis
        Vector3 boxToPlayerFlat = playerTransform.position - rb.position;
        boxToPlayerFlat.y = 0f;
        if (boxToPlayerFlat.sqrMagnitude < 0.0001f)
        {
            boxToPlayerFlat = -playerTransform.forward;
            boxToPlayerFlat.y = 0f;
        }
        boxToPlayerFlat.Normalize();

        // Forward input pushes box away from player; backward input pulls box toward player
        Vector3 slideDir = -boxToPlayerFlat * forwardPullInput;
        Vector3 desiredBoxVel = slideDir * effectiveMoveSpeed;
        rb.linearVelocity = new Vector3(desiredBoxVel.x, rb.linearVelocity.y, desiredBoxVel.z);

        // Rotate box around its Y axis and orbit player around box Y if not blocked by a wall
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            float yawDelta = turnInput * effectiveTurnSpeed * fixedDelta;
            Quaternion yawRot = Quaternion.Euler(0f, yawDelta, 0f);

            Vector3 offsetFromBox = playerTransform.position - rb.position;
            offsetFromBox.y = 0f;
            Vector3 rotatedOffset = yawRot * offsetFromBox;
            Vector3 targetPlayerPos = new Vector3(
                rb.position.x + rotatedOffset.x,
                playerTransform.position.y,
                rb.position.z + rotatedOffset.z
            );

            if (!IsOrbitBlocked(playerTransform, targetPlayerPos))
            {
                rb.MoveRotation(rb.rotation * yawRot);
                playerTransform.position = targetPlayerPos;
                boxToPlayerFlat = (rotatedOffset.sqrMagnitude > 0.0001f) ? rotatedOffset.normalized : boxToPlayerFlat;
            }
        }

        // Maintain push/pull gap when not blocked by a wall
        Vector3 desiredPlayerPos = rb.position + boxToPlayerFlat * Mathf.Max(0.8f, holdGap);
        desiredPlayerPos.y = playerTransform.position.y;
        if (!IsOrbitBlocked(playerTransform, desiredPlayerPos))
        {
            playerTransform.position = desiredPlayerPos;
        }

        // Keep player oriented consistently toward the box so they never look away while orbiting
        Vector3 toBoxFlat = rb.position - playerTransform.position;
        toBoxFlat.y = 0f;
        if (toBoxFlat.sqrMagnitude > 0.0001f)
        {
            float lookYaw = Quaternion.LookRotation(toBoxFlat.normalized, Vector3.up).eulerAngles.y;
            playerTransform.rotation = Quaternion.Euler(0f, lookYaw + faceYawOffset, 0f);
        }
    }

    private bool IsOrbitBlocked(Transform playerTransform, Vector3 candidatePos)
    {
        float checkRadius = 0.42f;
        float halfHeight = 0.55f;
        Vector3 bottom = candidatePos + Vector3.up * (checkRadius + 0.1f);
        Vector3 top = candidatePos + Vector3.up * Mathf.Max(bottom.y + 0.1f, halfHeight * 2f);

        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, checkRadius, orbitObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider col = overlaps[i];
            if (col == null) continue;
            Transform tr = col.transform;
            if (tr == playerTransform || tr.IsChildOf(playerTransform)) continue;
            if (tr == transform || tr.IsChildOf(transform)) continue;
            if (ignoredPlayerColliders.Contains(col)) continue;
            return true;
        }

        return false;
    }

    private void SetPlayerCollisionIgnore(GameObject playerObj, bool ignore)
    {
        if (itemColliders == null || itemColliders.Length == 0) return;

        if (ignore && playerObj != null)
        {
            ignoredPlayerColliders.Clear();
            Collider[] playerCols = playerObj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < playerCols.Length; i++)
            {
                if (playerCols[i] == null) continue;
                ignoredPlayerColliders.Add(playerCols[i]);
                for (int j = 0; j < itemColliders.Length; j++)
                {
                    if (itemColliders[j] != null)
                    {
                        Physics.IgnoreCollision(itemColliders[j], playerCols[i], true);
                    }
                }
            }
        }
        else if (!ignore)
        {
            for (int i = 0; i < ignoredPlayerColliders.Count; i++)
            {
                Collider pCol = ignoredPlayerColliders[i];
                if (pCol == null) continue;
                for (int j = 0; j < itemColliders.Length; j++)
                {
                    if (itemColliders[j] != null)
                    {
                        Physics.IgnoreCollision(itemColliders[j], pCol, false);
                    }
                }
            }
            ignoredPlayerColliders.Clear();
        }
    }

    private float GetWeightScale()
    {
        float ratio = 1f / Mathf.Max(1f, weight / 3f);
        return Mathf.Clamp(ratio, minWeightFactor, 1f);
    }

    private float GetHeavyMoveScale()
    {
        float excess = Mathf.Max(0f, weight - maxRayLiftWeight);
        float factor = 1f / (1f + excess * 0.04f);
        return Mathf.Clamp(factor, minHeavyMoveFactor, 1f);
    }
}