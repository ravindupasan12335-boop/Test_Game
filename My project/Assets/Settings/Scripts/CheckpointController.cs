using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointController : MonoBehaviour
{
    [Header("Keys")]
    public KeyCode loadCheckpointKey = KeyCode.T;
    public KeyCode fullResetKey = KeyCode.R;

    [Header("Starting Fallback")]
    [Tooltip("Where the player respawns if T is pressed before activating any flag.")]
    public Transform initialSpawnPoint;

    private CheckpointFlag activeCheckpoint;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isWarping = false;

    void Awake()
    {
        if (initialSpawnPoint != null)
        {
            initialPosition = initialSpawnPoint.position;
            initialRotation = initialSpawnPoint.rotation;
        }
        else
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(loadCheckpointKey) && !isWarping)
        {
            StartCoroutine(TeleportToLastCheckpoint());
        }

        if (Input.GetKeyDown(fullResetKey))
        {
            ResetWholeGame();
        }
    }

    public void SaveCheckpoint(CheckpointFlag flag)
    {
        if (flag == null) return;

        if (activeCheckpoint != null && activeCheckpoint != flag)
        {
            activeCheckpoint.SetSavedVisual(false);
        }

        activeCheckpoint = flag;
        activeCheckpoint.SetSavedVisual(true);
        Debug.Log($"[Checkpoint] Saved at '{flag.name}'.");
    }

    public void ResetWholeGame()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private IEnumerator TeleportToLastCheckpoint()
    {
        isWarping = true;

        Vector3 targetPos = activeCheckpoint != null
            ? activeCheckpoint.GetRespawnPosition()
            : initialPosition;

        Quaternion targetRot = activeCheckpoint != null
            ? activeCheckpoint.GetRespawnRotation()
            : initialRotation;

        CharacterController cc = GetComponent<CharacterController>();
        Rigidbody rb = GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = targetPos;
            rb.rotation = targetRot;
        }

        transform.SetPositionAndRotation(targetPos, targetRot);

        if (cc != null) cc.enabled = true;

        yield return new WaitForFixedUpdate();

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        isWarping = false;
    }
}