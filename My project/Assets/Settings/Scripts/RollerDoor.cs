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

    [Header("Lift Height")]
    [Tooltip("If checked, uses the door's own Y size as the upward lift distance.")]
    public bool useOwnYHeight = true;
    [Tooltip("Used if Use Own Y Height is unchecked.")]
    public float customOpenHeight = 3f;

    [Header("Acceleration Motion (0 -> Max Speed)")]
    public float maxSpeed = 5f;
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

        float liftHeight = useOwnYHeight
            ? GetEffectiveYHeight()
            : customOpenHeight;

        openLocalPos = closedLocalPos + new Vector3(0f, liftHeight, 0f);
        CurrentState = DoorState.Closed;
    }

    float GetEffectiveYHeight()
    {
        if (doorCollider != null)
        {
            return doorCollider.bounds.size.y;
        }

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            return rend.bounds.size.y;
        }

        return transform.localScale.y;
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

        // Reset speed cleanly when reversing direction from Closing
        if (CurrentState == DoorState.Closing)
        {
            currentSpeed = 0f;
        }
        else if (CurrentState == DoorState.Closed)
        {
            currentSpeed = 0f;
        }

        CurrentState = DoorState.Opening;
    }

    public void CloseDoor()
    {
        if (CurrentState == DoorState.Closed) return;

        // Make sure collider is active again as the door slides back down
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        if (CurrentState == DoorState.Opening)
        {
            currentSpeed = 0f;
        }
        else if (CurrentState == DoorState.Open)
        {
            currentSpeed = 0f;
        }

        CurrentState = DoorState.Closing;
    }
}