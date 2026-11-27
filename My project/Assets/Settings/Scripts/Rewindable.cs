using System.Collections.Generic;
using UnityEngine;

public class Rewindable : MonoBehaviour
{
    private struct Sample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public bool wasKinematic;
        public bool wasUsingGravity;
        public bool rollerDoorWasOpenOrOpening;
        public bool trayDoorWasOpen;
    }

    [Header("Rewind History")]
    [Tooltip("Maximum seconds of history stored.")]
    [Min(1f)]
    public float maxRewindSeconds = 10f;

    [Tooltip("Samples recorded per second. Higher = smoother reverse curves.")]
    [Range(15f, 120f)]
    public float sampleRate = 60f;

    [Header("Optional Door & Button Sync")]
    [Tooltip("Syncs RollerDoor_Open open/closed state and button visuals when rewinding.")]
    public bool syncDoorAndButtonStates = true;

    public bool IsRewinding { get; private set; }

    private readonly List<Sample> samples = new List<Sample>();
    private Rigidbody rb;
    private CharacterController cc;
    private RollerDoor_Open rollerDoor;
    private BatteryTrayDoorSystem parentTrayDoorSystem;

    private float recordTimer = 0f;
    private float virtualPlaybackTime = 0f;
    private bool cachedPhysicsState = false;
    private bool prevKinematic;
    private bool prevGravity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
        rollerDoor = GetComponent<RollerDoor_Open>();
        FindParentTrayDoorSystem();
    }

    private void FindParentTrayDoorSystem()
    {
        if (parentTrayDoorSystem != null) return;

        BatteryTrayDoorSystem[] trays = FindObjectsByType<BatteryTrayDoorSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < trays.Length; i++)
        {
            if (trays[i] != null && trays[i].doorTransform == transform)
            {
                parentTrayDoorSystem = trays[i];
                break;
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsRewinding)
        {
            RecordSample(Time.fixedDeltaTime);
        }
    }

    private Sample CaptureCurrentSample()
    {
        if (parentTrayDoorSystem == null)
        {
            FindParentTrayDoorSystem();
        }

        return new Sample
        {
            time = Time.time,
            position = transform.position,
            rotation = transform.rotation,
            linearVelocity = (rb != null && !rb.isKinematic) ? rb.linearVelocity : Vector3.zero,
            angularVelocity = (rb != null && !rb.isKinematic) ? rb.angularVelocity : Vector3.zero,
            wasKinematic = rb != null ? rb.isKinematic : false,
            wasUsingGravity = rb != null ? rb.useGravity : false,
            rollerDoorWasOpenOrOpening = rollerDoor != null ? rollerDoor.IsOpenOrOpening : false,
            trayDoorWasOpen = parentTrayDoorSystem != null ? parentTrayDoorSystem.IsDoorOpen : false
        };
    }

    private void RecordSample(float dt)
    {
        recordTimer += dt;
        float interval = 1f / Mathf.Max(1f, sampleRate);

        if (recordTimer < interval && samples.Count > 0)
        {
            return;
        }

        recordTimer = 0f;

        Sample s = CaptureCurrentSample();
        samples.Add(s);
        PruneOldSamples(s.time - maxRewindSeconds);
    }

    private void PruneOldSamples(float cutoffTime)
    {
        while (samples.Count > 1 && samples[0].time < cutoffTime)
        {
            samples.RemoveAt(0);
        }
    }

    public void BeginRewind()
    {
        if (IsRewinding) return;

        Sample latest = CaptureCurrentSample();

        if (samples.Count == 0 || samples[samples.Count - 1].time < latest.time)
        {
            samples.Add(latest);
        }
        else
        {
            samples[samples.Count - 1] = latest;
        }

        IsRewinding = true;
        virtualPlaybackTime = samples[samples.Count - 1].time;

        if (!cachedPhysicsState)
        {
            if (rb != null)
            {
                prevKinematic = rb.isKinematic;
                prevGravity = rb.useGravity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (cc != null)
            {
                cc.enabled = false;
            }
            cachedPhysicsState = true;
        }
    }

    public bool StepReverseBySeconds(float deltaRewindSeconds)
    {
        if (!IsRewinding || samples.Count == 0) return false;

        float earliestTime = samples[0].time;
        virtualPlaybackTime = Mathf.Max(earliestTime, virtualPlaybackTime - Mathf.Max(0f, deltaRewindSeconds));

        while (samples.Count > 1 && samples[samples.Count - 1].time > virtualPlaybackTime)
        {
            samples.RemoveAt(samples.Count - 1);
        }

        Sample activeSample;
        if (samples.Count == 1)
        {
            activeSample = samples[0];
            ApplyPose(activeSample.position, activeSample.rotation);
            SyncDoorAndButtons(activeSample);
            return virtualPlaybackTime > earliestTime;
        }

        Sample older = samples[samples.Count - 2];
        Sample newer = samples[samples.Count - 1];
        float span = Mathf.Max(0.0001f, newer.time - older.time);
        float t = Mathf.Clamp01((virtualPlaybackTime - older.time) / span);

        Vector3 smoothPos = Vector3.Lerp(older.position, newer.position, t);
        Quaternion smoothRot = Quaternion.Slerp(older.rotation, newer.rotation, t);
        ApplyPose(smoothPos, smoothRot);

        activeSample = t < 0.5f ? older : newer;
        SyncDoorAndButtons(activeSample);

        return virtualPlaybackTime > earliestTime + 0.0001f;
    }

    public void EndRewind()
    {
        if (!IsRewinding) return;

        IsRewinding = false;

        Sample latest = samples.Count > 0 ? samples[samples.Count - 1] : default;

        if (cc != null)
        {
            cc.enabled = true;
        }

        if (rb != null && cachedPhysicsState)
        {
            bool isSlotted = IsCurrentlySlottedInTray();
            rb.isKinematic = isSlotted ? true : prevKinematic;
            rb.useGravity = prevGravity;

            if (!rb.isKinematic && samples.Count > 0)
            {
                rb.linearVelocity = latest.linearVelocity;
                rb.angularVelocity = latest.angularVelocity;
            }
        }

        if (samples.Count > 0)
        {
            SyncDoorAndButtons(latest);
        }

        recordTimer = 0f;
        PruneOldSamples(Time.time - maxRewindSeconds);
        cachedPhysicsState = false;
    }

    private void ApplyPose(Vector3 pos, Quaternion rot)
    {
        if (rb != null && rb.isKinematic)
        {
            rb.position = pos;
            rb.rotation = rot;
        }
        transform.SetPositionAndRotation(pos, rot);
    }

    private void SyncDoorAndButtons(Sample sample)
    {
        if (!syncDoorAndButtonStates) return;

        bool anyStateChanged = false;

        if (rollerDoor != null)
        {
            if (sample.rollerDoorWasOpenOrOpening && !rollerDoor.IsOpenOrOpening)
            {
                rollerDoor.OpenDoor();
                anyStateChanged = true;
            }
            else if (!sample.rollerDoorWasOpenOrOpening && rollerDoor.IsOpenOrOpening)
            {
                rollerDoor.CloseDoor();
                anyStateChanged = true;
            }
        }

        if (parentTrayDoorSystem != null && parentTrayDoorSystem.IsDoorOpen != sample.trayDoorWasOpen)
        {
            parentTrayDoorSystem.PressSideButton("RewindSync");
            anyStateChanged = true;
        }

        if (anyStateChanged)
        {
            TrayDoorButton[] trayButtons = FindObjectsByType<TrayDoorButton>(FindObjectsSortMode.None);
            for (int i = 0; i < trayButtons.Length; i++)
            {
                if (trayButtons[i] != null)
                {
                    trayButtons[i].RefreshVisualColor();
                }
            }
        }
    }

    private bool IsCurrentlySlottedInTray()
    {
        BatteryTrayDoorSystem tray = FindFirstObjectByType<BatteryTrayDoorSystem>();
        if (tray == null || tray.slots == null) return false;

        Battery bat = GetComponent<Battery>();
        if (bat == null) return false;

        for (int i = 0; i < tray.slots.Count; i++)
        {
            if (tray.slots[i].slottedBattery == bat)
            {
                return true;
            }
        }
        return false;
    }
}