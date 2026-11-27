using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeRewindManager : MonoBehaviour
{
    [Header("Input & Time Pool")]
    public KeyCode rewindKey = KeyCode.X;

    [Tooltip("Maximum seconds of rewind energy in the bar.")]
    [Min(0.5f)]
    public float maxRewindSeconds = 10f;

    [Tooltip("How many bar-seconds drain or recharge per real second. Drain speed always equals recharge speed.")]
    [Min(0.1f)]
    public float rateMultiplier = 1.0f;

    [Tooltip("Rewind playback speed relative to real time (1 = 1x reverse, 1.5 = slightly faster reverse).")]
    [Min(0.1f)]
    public float playbackSpeed = 1.25f;

    [Header("UI Bar References (Optional - Auto-Creates If Left Empty)")]
    [Tooltip("Optional existing UI Image (Type = Filled, Horizontal) for the bar fill.")]
    public Image customFillImage;

    [Tooltip("Optional existing UI Image for the bar background.")]
    public Image customBackgroundImage;

    [Tooltip("Optional existing UI Text for the watch icon/label.")]
    public Text customWatchText;

    [Header("Watch Icon & Bar Layout (Inspector Editable)")]
    public string watchIcon = "⌚";
    public int watchFontSize = 26;
    public Color watchIconColor = Color.cyan;

    [Tooltip("Anchored position on screen (pixels from bottom-center by default).")]
    public Vector2 barAnchoredPosition = new Vector2(0f, 70f);

    [Tooltip("Total width and height of the bar container.")]
    public Vector2 barSize = new Vector2(260f, 24f);

    [Tooltip("Width of the watch icon area in front of the bar.")]
    public float watchAreaWidth = 36f;

    public Color backgroundColor = new Color(0.1f, 0.12f, 0.16f, 0.85f);
    public Color fillNormalColor = new Color(0.25f, 0.85f, 1f, 1f);
    public Color fillRewindingColor = new Color(1f, 0.85f, 0.25f, 1f);
    public Color fillLowColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Range(0f, 0.5f)]
    public float lowEnergyThreshold = 0.2f;

    [Tooltip("If true, hides the bar when full and not rewinding (like a subtle meter); if false, always visible.")]
    public bool hideWhenFullAndIdle = false;

    public float CurrentChargeSeconds { get; private set; }
    public float ChargeNormalized => maxRewindSeconds > 0f ? Mathf.Clamp01(CurrentChargeSeconds / maxRewindSeconds) : 0f;
    public bool IsRewinding { get; private set; }

    private readonly List<Rewindable> rewindables = new List<Rewindable>();
    private Canvas runtimeCanvas;
    private RectTransform barRootRect;
    private RectTransform watchRect;
    private RectTransform trackRect;
    private Image runtimeBgImage;
    private Image runtimeFillImage;
    private Text runtimeWatchText;
    private Sprite fallbackSprite;

    void Awake()
    {
        CurrentChargeSeconds = maxRewindSeconds;
        RefreshRewindables();
        EnsureBarUI();
        ApplyBarLayoutAndVisuals();
    }

    void OnValidate()
    {
        maxRewindSeconds = Mathf.Max(0.5f, maxRewindSeconds);
        rateMultiplier = Mathf.Max(0.1f, rateMultiplier);
        playbackSpeed = Mathf.Max(0.1f, playbackSpeed);
        watchFontSize = Mathf.Max(8, watchFontSize);
        barSize.x = Mathf.Max(40f, barSize.x);
        barSize.y = Mathf.Max(8f, barSize.y);
        watchAreaWidth = Mathf.Max(16f, watchAreaWidth);
        lowEnergyThreshold = Mathf.Clamp01(lowEnergyThreshold);

        if (!Application.isPlaying)
        {
            CurrentChargeSeconds = maxRewindSeconds;
        }
        else
        {
            CurrentChargeSeconds = Mathf.Clamp(CurrentChargeSeconds, 0f, maxRewindSeconds);
        }

        if (runtimeCanvas != null || customFillImage != null)
        {
            ApplyBarLayoutAndVisuals();
        }
    }

    public void RefreshRewindables()
    {
        rewindables.Clear();
        rewindables.AddRange(FindObjectsByType<Rewindable>(FindObjectsSortMode.None));
    }

    void Update()
    {
        bool wantsRewind = Input.GetKey(rewindKey) && CurrentChargeSeconds > 0.001f;

        if (wantsRewind && !IsRewinding)
        {
            StartRewind();
        }
        else if (!wantsRewind && IsRewinding)
        {
            StopRewind();
        }

        // Equal drain and recharge rate (Prince of Persia style)
        float ratePerSecond = rateMultiplier;
        if (IsRewinding)
        {
            CurrentChargeSeconds = Mathf.Max(0f, CurrentChargeSeconds - ratePerSecond * Time.deltaTime);
            if (CurrentChargeSeconds <= 0.001f)
            {
                CurrentChargeSeconds = 0f;
                StopRewind();
            }
        }
        else
        {
            CurrentChargeSeconds = Mathf.Min(maxRewindSeconds, CurrentChargeSeconds + ratePerSecond * Time.deltaTime);
        }

        ApplyBarLayoutAndVisuals();
    }

    void FixedUpdate()
    {
        if (!IsRewinding) return;

        float reverseStep = Time.fixedDeltaTime * playbackSpeed;

        for (int i = 0; i < rewindables.Count; i++)
        {
            Rewindable r = rewindables[i];
            if (r != null)
            {
                r.StepReverseBySeconds(reverseStep);
            }
        }
    }

    private void StartRewind()
    {
        IsRewinding = true;
        RefreshRewindables();

        for (int i = 0; i < rewindables.Count; i++)
        {
            if (rewindables[i] != null)
            {
                rewindables[i].BeginRewind();
            }
        }
    }

    private void StopRewind()
    {
        if (!IsRewinding) return;
        IsRewinding = false;

        for (int i = 0; i < rewindables.Count; i++)
        {
            if (rewindables[i] != null)
            {
                rewindables[i].EndRewind();
            }
        }
    }

    private Sprite GetOrCreateFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return fallbackSprite;
    }

    private void EnsureBarUI()
    {
        if (customFillImage != null) return;

        GameObject canvasGo = new GameObject("RewindBar_Canvas");
        canvasGo.transform.SetParent(transform, false);

        runtimeCanvas = canvasGo.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject rootGo = new GameObject("RewindBar_Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        barRootRect = rootGo.AddComponent<RectTransform>();

        // Watch icon on the left side of the bar
        GameObject watchGo = new GameObject("WatchIcon");
        watchGo.transform.SetParent(rootGo.transform, false);
        watchRect = watchGo.AddComponent<RectTransform>();
        runtimeWatchText = watchGo.AddComponent<Text>();
        runtimeWatchText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        runtimeWatchText.alignment = TextAnchor.MiddleCenter;

        // Bar track background
        GameObject trackGo = new GameObject("BarBackground");
        trackGo.transform.SetParent(rootGo.transform, false);
        trackRect = trackGo.AddComponent<RectTransform>();
        runtimeBgImage = trackGo.AddComponent<Image>();
        runtimeBgImage.sprite = GetOrCreateFallbackSprite();

        // Bar fill
        GameObject fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(trackGo.transform, false);
        RectTransform fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        runtimeFillImage = fillGo.AddComponent<Image>();
        runtimeFillImage.sprite = GetOrCreateFallbackSprite();
        runtimeFillImage.type = Image.Type.Filled;
        runtimeFillImage.fillMethod = Image.FillMethod.Horizontal;
        runtimeFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void ApplyBarLayoutAndVisuals()
    {
        float ratio = ChargeNormalized;
        bool isFull = Mathf.Approximately(ratio, 1f);
        bool showBar = !(hideWhenFullAndIdle && isFull && !IsRewinding);

        if (runtimeCanvas != null)
        {
            runtimeCanvas.enabled = showBar;
        }
        else if (customFillImage != null)
        {
            customFillImage.transform.parent.gameObject.SetActive(showBar);
        }

        if (!showBar) return;

        Color activeFillColor = IsRewinding
            ? fillRewindingColor
            : (ratio <= lowEnergyThreshold ? fillLowColor : fillNormalColor);

        if (customFillImage != null)
        {
            customFillImage.fillAmount = ratio;
            customFillImage.color = activeFillColor;
            if (customBackgroundImage != null) customBackgroundImage.color = backgroundColor;
            if (customWatchText != null)
            {
                customWatchText.text = watchIcon;
                customWatchText.color = watchIconColor;
                customWatchText.fontSize = watchFontSize;
            }
            return;
        }

        if (barRootRect != null)
        {
            barRootRect.anchorMin = new Vector2(0.5f, 0f);
            barRootRect.anchorMax = new Vector2(0.5f, 0f);
            barRootRect.pivot = new Vector2(0.5f, 0.5f);
            barRootRect.anchoredPosition = barAnchoredPosition;
            barRootRect.sizeDelta = new Vector2(barSize.x + watchAreaWidth, barSize.y);
        }

        if (watchRect != null)
        {
            watchRect.anchorMin = new Vector2(0f, 0.5f);
            watchRect.anchorMax = new Vector2(0f, 0.5f);
            watchRect.pivot = new Vector2(0f, 0.5f);
            watchRect.anchoredPosition = Vector2.zero;
            watchRect.sizeDelta = new Vector2(watchAreaWidth, barSize.y + 8f);
        }

        if (runtimeWatchText != null)
        {
            runtimeWatchText.text = watchIcon;
            runtimeWatchText.color = watchIconColor;
            runtimeWatchText.fontSize = watchFontSize;
        }

        if (trackRect != null)
        {
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(0f, 0.5f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.anchoredPosition = new Vector2(watchAreaWidth, 0f);
            trackRect.sizeDelta = barSize;
        }

        if (runtimeBgImage != null)
        {
            runtimeBgImage.color = backgroundColor;
        }

        if (runtimeFillImage != null)
        {
            runtimeFillImage.fillAmount = ratio;
            runtimeFillImage.color = activeFillColor;
        }
    }
}