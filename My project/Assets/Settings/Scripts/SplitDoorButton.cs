using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SplitDoorButton : MonoBehaviour, IInteractable
{
    [Header("Side Button Setup")]
    public string sideLabel = "Side Button";
    public SplitSideDoor targetDoor;

    public void Interact(Cube_Player_Movement player)
    {
        Press();
    }

    public void Press()
    {
        if (targetDoor == null)
        {
            targetDoor = FindFirstObjectByType<SplitSideDoor>();
        }

        if (targetDoor != null)
        {
            targetDoor.PressSideButton(sideLabel);
        }
        else
        {
            Debug.LogWarning($"[{sideLabel}] No SplitSideDoor assigned.");
        }
    }
}