using System.Collections.Generic;
using UnityEngine;

public class DoorButton : MonoBehaviour, IInteractable
{
    [Header("Target Doors (Assign Multiple)")]
    [Tooltip("Set how many roller doors this button controls, then drag each RollerDoor_Open into its slot.")]
    public List<RollerDoor_Open> targetDoors = new List<RollerDoor_Open>();

    [Header("Optional Visual Feedback (Button Mesh)")]
    public Renderer buttonRenderer;
    public Color closedStateColor = Color.red;
    public Color openStateColor = Color.green;

    private TrayDoorButton pairedTrayButton;

    void Awake()
    {
        pairedTrayButton = GetComponent<TrayDoorButton>();
    }

    void Start()
    {
        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }
        UpdateButtonVisual();
    }

    void Update()
    {
        if (pairedTrayButton == null)
        {
            pairedTrayButton = GetComponent<TrayDoorButton>();
        }
        UpdateButtonVisual();
    }

    public void Interact(Cube_Player_Movement player)
    {
        // If paired with a TrayDoorButton on this same object, require tray power first
        if (pairedTrayButton != null)
        {
            BatteryTrayDoorSystem tray = pairedTrayButton.doorSystem != null
                ? pairedTrayButton.doorSystem
                : FindFirstObjectByType<BatteryTrayDoorSystem>();

            if (tray == null || !tray.IsDoorPowered)
            {
                Debug.Log($"[{name}] Tray not powered yet. Fill and power all batteries first.");
                return;
            }

            bool willOpen = !tray.IsDoorOpen;
            pairedTrayButton.Press();
            SetAllRollerDoorsOpenState(willOpen);
            UpdateButtonVisual();
            return;
        }

        if (targetDoors == null || targetDoors.Count == 0) return;

        bool anyClosedOrClosing = false;
        for (int i = 0; i < targetDoors.Count; i++)
        {
            RollerDoor_Open door = targetDoors[i];
            if (door == null) continue;

            if (door.CurrentState == RollerDoor_Open.DoorState.Closed ||
                door.CurrentState == RollerDoor_Open.DoorState.Closing)
            {
                anyClosedOrClosing = true;
                break;
            }
        }

        SetAllRollerDoorsOpenState(anyClosedOrClosing);
        UpdateButtonVisual();
    }

    private void SetAllRollerDoorsOpenState(bool open)
    {
        if (targetDoors == null) return;

        for (int i = 0; i < targetDoors.Count; i++)
        {
            RollerDoor_Open door = targetDoors[i];
            if (door == null) continue;

            if (open)
            {
                door.OpenDoor();
            }
            else
            {
                door.CloseDoor();
            }
        }
    }

    void UpdateButtonVisual()
    {
        if (buttonRenderer == null) return;

        if (pairedTrayButton != null)
        {
            BatteryTrayDoorSystem tray = pairedTrayButton.doorSystem;
            bool isOpen = tray != null && tray.IsDoorOpen;
            Color desiredColor = isOpen ? openStateColor : closedStateColor;
            if (buttonRenderer.material.color != desiredColor)
            {
                buttonRenderer.material.color = desiredColor;
            }
            return;
        }

        if (targetDoors == null || targetDoors.Count == 0) return;

        bool anyOpenOrOpening = false;
        for (int i = 0; i < targetDoors.Count; i++)
        {
            RollerDoor_Open door = targetDoors[i];
            if (door != null && door.IsOpenOrOpening)
            {
                anyOpenOrOpening = true;
                break;
            }
        }

        Color fallbackDesiredColor = anyOpenOrOpening ? openStateColor : closedStateColor;
        if (buttonRenderer.material.color != fallbackDesiredColor)
        {
            buttonRenderer.material.color = fallbackDesiredColor;
        }
    }
}