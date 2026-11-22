using UnityEngine;

public class DoorButton : MonoBehaviour, IInteractable
{
    [Header("Target Door")]
    public RollerDoor_Open targetDoor;

    [Header("Optional Visual Feedback (Button Mesh)")]
    public Renderer buttonRenderer;
    public Color closedStateColor = Color.red;
    public Color openStateColor = Color.green;

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
        UpdateButtonVisual();
    }

    public void Interact(Cube_Player_Movement player)
    {
        if (targetDoor == null) return;
        targetDoor.ToggleDoor();
        UpdateButtonVisual();
    }

    void UpdateButtonVisual()
    {
        if (buttonRenderer == null || targetDoor == null) return;

        Color desiredColor = targetDoor.IsOpenOrOpening ? openStateColor : closedStateColor;
        if (buttonRenderer.material.color != desiredColor)
        {
            buttonRenderer.material.color = desiredColor;
        }
    }
}