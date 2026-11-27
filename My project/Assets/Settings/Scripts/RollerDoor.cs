using UnityEngine;

public class RollerDoor_Open : MonoBehaviour
{
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    public enum SlideDirection
    {
        Up,
        Down,
        Right,
        Left,
        Forward,
        Back
    }

    [Header("Slide Direction & Distance")]
    [Tooltip("Direction the door moves when opening (and reverses back along to close).")]
    public SlideDirection slideDirection = SlideDirection.Up;

    [Tooltip("If checked, uses the door's own size along the chosen slide axis as the open distance.")]
    public bool useOwnAxisSize = true;

    [Tooltip("Used if Use Own Axis Size is unchecked.")]
    [Min(0f)]
    public float customOpenDistance = 3f;

    [Header("Acceleration Motion (0 -> Max Speed)")]
    [Min(0.01f)]
    public float maxSpeed = 5f;

    [Min(0.01f)]
    public float acceleration = 4f;

    [Header("Collider Behavior")]
    public bool disableColliderWhenOpen = true;

    public DoorState CurrentState { get; private set; } = DoorState.Closed;
    public bool IsOpenOrOpening => CurrentState == DoorState.Open || CurrentState == DoorState.Opening;

    private Vector3 closedLocalPos;
    private Vector3 openLocalPos;
    private float currentSpeed = 0f;
    private Collider doorCollider;

    void Awake()
    {
        doorCollider = GetComponent<Collider>();
        closedLocalPos = transform.localPosition;

        Vector3 dirVector = GetDirectionVector(slideDirection);
        float distance = useOwnAxisSize
            ? GetEffectiveAxisSize(slideDirection)
            : customOpenDistance;

        openLocalPos = closedLocalPos + dirVector * distance;
        CurrentState = DoorState.Closed;
    }

    Vector3 GetDirectionVector(SlideDirection dir)
    {
        switch (dir)
        {
            case SlideDirection.Up:      return Vector3.up;
            case SlideDirection.Down:    return Vector3.down;
            case SlideDirection.Right:   return Vector3.right;
            case SlideDirection.Left:    return Vector3.left;
            case SlideDirection.Forward: return Vector3.forward;
            case SlideDirection.Back:    return Vector3.back;
            default:                     return Vector3.up;
        }
    }

    float GetEffectiveAxisSize(SlideDirection dir)
    {
        Vector3 boundsSize = Vector3.zero;
        bool hasBounds = false;

        if (doorCollider != null)
        {
            boundsSize = doorCollider.bounds.size;
            hasBounds = true;
        }
        else
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                boundsSize = rend.bounds.size;
                hasBounds = true;
            }
        }

        switch (dir)
        {
            case SlideDirection.Up:
            case SlideDirection.Down:
                return hasBounds ? boundsSize.y : transform.localScale.y;

            case SlideDirection.Right:
            case SlideDirection.Left:
                return hasBounds ? boundsSize.x : transform.localScale.x;

            case SlideDirection.Forward:
            case SlideDirection.Back:
                return hasBounds ? boundsSize.z : transform.localScale.z;

            default:
                return transform.localScale.y;
        }
    }

    void Update()
    {
        if (CurrentState != DoorState.Opening && CurrentState != DoorState.Closing)
        {
            return;
        }

        Vector3 targetPos = CurrentState == DoorState.Opening ? openLocalPos : closedLocalPos;

        // Gradually accelerate from 0 up to maxSpeed
        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPos,
            currentSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, targetPos) <= 0.001f)
        {
            transform.localPosition = targetPos;
            currentSpeed = 0f;

            if (CurrentState == DoorState.Opening)
            {
                CurrentState = DoorState.Open;
                if (disableColliderWhenOpen && doorCollider != null)
                {
                    doorCollider.enabled = false;
                }
            }
            else if (CurrentState == DoorState.Closing)
            {
                CurrentState = DoorState.Closed;
                if (doorCollider != null)
                {
                    doorCollider.enabled = true;
                }
            }
        }
    }

    public void ToggleDoor()
    {
        if (CurrentState == DoorState.Closed || CurrentState == DoorState.Closing)
        {
            OpenDoor();
        }
        else if (CurrentState == DoorState.Open || CurrentState == DoorState.Opening)
        {
            CloseDoor();
        }
    }

    public void OpenDoor()
    {
        if (CurrentState == DoorState.Open) return;

        // Re-enable collider while moving/closed so the door stays solid until fully open
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        if (CurrentState == DoorState.Closing || CurrentState == DoorState.Closed)
        {
            currentSpeed = 0f;
        }

        CurrentState = DoorState.Opening;
    }

    public void CloseDoor()
    {
        if (CurrentState == DoorState.Closed) return;

        // Make sure collider is active again as the door slides back
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        if (CurrentState == DoorState.Opening || CurrentState == DoorState.Open)
        {
            currentSpeed = 0f;
        }

        CurrentState = DoorState.Closing;
    }
}