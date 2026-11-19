using System.Collections.Generic;
using UnityEngine;

public class MedievalDoorController : MonoBehaviour
{
    [Header("Locks (Drag the 4 Lock Objects Here)")]
    public List<DoorLockSlot> locks = new List<DoorLockSlot>();

    [Header("Slow Medieval Door Open Motion")]
    [Tooltip("Angle in degrees to swing open (e.g., 90 or -90 on Y).")]
    public Vector3 openRotationOffset = new Vector3(0f, 90f, 0f);

    [Tooltip("Optional position shift if your door slides/recesses instead of swinging.")]
    public Vector3 openPositionOffset = Vector3.zero;

    [Tooltip("Lower values = heavier, slower medieval door motion.")]
    public float openSpeed = 1.0f;

    private Quaternion closedRot;
    private Quaternion targetRot;
    private Vector3 closedPos;
    private Vector3 targetPos;
    private bool isOpen = false;

    void Start()
    {
        closedRot = transform.localRotation;
        targetRot = closedRot * Quaternion.Euler(openRotationOffset);

        closedPos = transform.localPosition;
        targetPos = closedPos + openPositionOffset;

        if (locks.Count == 0)
        {
            locks.AddRange(GetComponentsInChildren<DoorLockSlot>(true));
        }
    }

    void Update()
    {
        if (!isOpen && AreAllLocksUnlocked())
        {
            isOpen = true;
            Debug.Log("All 4 locks unlocked! Heavy medieval door opening...");
        }

        if (isOpen)
        {
            float step = openSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, step);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, step);
        }
    }

    bool AreAllLocksUnlocked()
    {
        if (locks.Count == 0) return false;

        foreach (DoorLockSlot lockSlot in locks)
        {
            if (lockSlot != null && !lockSlot.IsUnlocked)
            {
                return false;
            }
        }
        return true;
    }
}