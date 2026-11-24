using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrayDoorButton : MonoBehaviour, IInteractable
{
    [Header("Button Setup")]
    public string sideName = "Left Button";
    public BatteryTrayDoorSystem doorSystem;

    public void Interact(Cube_Player_Movement player)
    {
        Press();
    }

    public void Press()
    {
        if (doorSystem == null)
        {
            doorSystem = FindFirstObjectByType<BatteryTrayDoorSystem>();
        }

        if (doorSystem != null)
        {
            doorSystem.PressSideButton(sideName);
        }
        else
        {
            Debug.LogWarning($"[{sideName}] No BatteryTrayDoorSystem assigned.");
        }
    }
}