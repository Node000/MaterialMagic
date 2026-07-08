using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PopupDragonBackgroundUI : MonoBehaviour
{
    private const string WindowNamePrefix = "PopupDragonWindow_";

    [Header("弹窗生成")]
    [SerializeField] private RectTransform windowPrefab;
    [SerializeField, FormerlySerializedAs("visibleWindowCount"), Min(1)] private int windowCount = 10;

    [Header("基础布局")]
    [SerializeField] private Vector2 frontWindowAnchoredPosition = new Vector2(-350f, -40f);
    [SerializeField] private Vector2 segmentOffset = new Vector2(-34f, -24f);
    [SerializeField] private bool frontWindowStaysStill = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("龙身摆动")]
    [SerializeField] private Vector2 waveAmplitude = new Vector2(24f, 18f);
    [SerializeField] private float waveSpeed = 1.35f;
    [SerializeField] private float phaseStep = 0.72f;
    [SerializeField] private float tailAmplitudeGain = 0.12f;
    [SerializeField] private float verticalWaveRatio = 1.18f;

    [Header("旋转和景深")]
    [SerializeField] private float rotationAmplitude = 3.5f;
    [SerializeField] private float rotationSpeed = 1.1f;
    [SerializeField] private float rotationPhaseStep = 0.9f;
    [SerializeField] private float tailRotationGain = 0.08f;
    [SerializeField, Range(0f, 0.08f)] private float tailScaleStep = 0.018f;
    [SerializeField, Range(0.3f, 1f)] private float minTailScale = 0.78f;

    private readonly List<RectTransform> windows = new List<RectTransform>();
    private int builtWindowCount = -1;
    private RectTransform builtWindowPrefab;
    private bool frontWindowReplaced;

    public bool TryGetFrontWindowPose(out Vector2 anchoredPosition, out float rotation, out float scale)
    {
        return TryGetWindowPose(0, out anchoredPosition, out rotation, out scale);
    }

    public bool TryGetWindowPose(int index, out Vector2 anchoredPosition, out float rotation, out float scale)
    {
        if (index < 0 || index >= Mathf.Max(1, windowCount))
        {
            anchoredPosition = Vector2.zero;
            rotation = 0f;
            scale = 1f;
            return false;
        }

        EvaluateWindowPose(index, GetAnimationTime(), out anchoredPosition, out rotation, out scale);
        return true;
    }

    public void SetFrontWindowReplaced(bool replaced)
    {
        if (frontWindowReplaced == replaced)
            return;

        frontWindowReplaced = replaced;
        EnsureWindows();
        ApplyActiveAndOrder();
    }

    private void Awake()
    {
        EnsureWindows();
        ApplyActiveAndOrder();
        ApplyLayout(GetAnimationTime());
    }

    private void OnEnable()
    {
        EnsureWindows();
        ApplyActiveAndOrder();
        ApplyLayout(GetAnimationTime());
    }

    private void Update()
    {
        EnsureWindows();
        ApplyLayout(GetAnimationTime());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        windowCount = Mathf.Max(1, windowCount);
        minTailScale = Mathf.Clamp(minTailScale, 0.3f, 1f);
        tailScaleStep = Mathf.Max(0f, tailScaleStep);

        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            CollectExistingGeneratedWindows();
            ApplyActiveAndOrder();
            ApplyLayout(0f);
        }
    }
#endif

    private void EnsureWindows()
    {
        if (windowPrefab == null)
        {
            ClearGeneratedWindows();
            return;
        }

        if (builtWindowCount == windowCount && builtWindowPrefab == windowPrefab && AreWindowsValid())
            return;

        RebuildWindows();
    }

    private bool AreWindowsValid()
    {
        if (windows.Count != windowCount)
            return false;

        for (int i = 0; i < windows.Count; i++)
        {
            RectTransform window = windows[i];
            if (window == null || !window.IsChildOf(transform))
                return false;
        }
        return true;
    }

    private void RebuildWindows()
    {
        ClearGeneratedWindows();

        int count = Mathf.Max(1, windowCount);
        for (int i = 0; i < count; i++)
        {
            RectTransform window = Instantiate(windowPrefab, transform);
            window.name = WindowNamePrefix + (i + 1).ToString("00");
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.gameObject.SetActive(true);
            ConfigureGeneratedWindow(window, i);
            windows.Add(window);
        }

        builtWindowCount = count;
        builtWindowPrefab = windowPrefab;
        ApplyActiveAndOrder();
    }

    private void ClearGeneratedWindows()
    {
        windows.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(WindowNamePrefix, StringComparison.Ordinal))
                continue;

            if (Application.isPlaying)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        builtWindowCount = -1;
        builtWindowPrefab = null;
    }

#if UNITY_EDITOR
    private void CollectExistingGeneratedWindows()
    {
        windows.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(WindowNamePrefix, StringComparison.Ordinal) && child is RectTransform rect)
                windows.Add(rect);
        }
        windows.Sort(CompareWindowNames);
    }
#endif

    private void ConfigureGeneratedWindow(RectTransform window, int index)
    {
        Graphic[] graphics = window.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void ApplyActiveAndOrder()
    {
        for (int i = 0; i < windows.Count; i++)
        {
            RectTransform window = windows[i];
            if (window == null)
                continue;

            bool shouldBeActive = !(frontWindowReplaced && i == 0);
            if (window.gameObject.activeSelf != shouldBeActive)
                window.gameObject.SetActive(shouldBeActive);
        }

        for (int i = 0; i < windows.Count; i++)
        {
            RectTransform window = windows[i];
            if (window != null)
                window.SetSiblingIndex(windows.Count - 1 - i);
        }
    }

    private void ApplyLayout(float animationTime)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            RectTransform window = windows[i];
            if (window == null)
                continue;

            EvaluateWindowPose(i, animationTime, out Vector2 position, out float rotation, out float scale);
            window.anchoredPosition = position;
            window.localEulerAngles = new Vector3(0f, 0f, rotation);
            window.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void EvaluateWindowPose(int index, float animationTime, out Vector2 position, out float rotation, out float scale)
    {
        bool fixedFront = index == 0 && frontWindowStaysStill;
        position = frontWindowAnchoredPosition + segmentOffset * index;
        rotation = 0f;

        if (!fixedFront)
        {
            float depthScale = 1f + index * tailAmplitudeGain;
            float phase = animationTime * waveSpeed - index * phaseStep;
            position.x += Mathf.Sin(phase) * waveAmplitude.x * depthScale;
            position.y += Mathf.Cos(phase * verticalWaveRatio) * waveAmplitude.y * depthScale;

            float rotationPhase = animationTime * rotationSpeed - index * rotationPhaseStep;
            rotation = Mathf.Sin(rotationPhase) * rotationAmplitude * (1f + index * tailRotationGain);
        }

        scale = Mathf.Max(minTailScale, 1f - tailScaleStep * index);
    }

    private float GetAnimationTime()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private static int CompareWindowNames(RectTransform a, RectTransform b)
    {
        return ExtractFirstNumber(a.name).CompareTo(ExtractFirstNumber(b.name));
    }

    private static int ExtractFirstNumber(string value)
    {
        int result = 0;
        bool foundDigit = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c >= '0' && c <= '9')
            {
                foundDigit = true;
                result = result * 10 + c - '0';
            }
            else if (foundDigit)
            {
                break;
            }
        }

        return foundDigit ? result : int.MaxValue;
    }
}
