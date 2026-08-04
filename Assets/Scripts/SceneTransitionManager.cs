using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private Image transitionImage;
    [SerializeField] private Material transitionMaterial;
    [SerializeField] private string pcGameSceneName = "SampleScene_PC";
    [SerializeField] private string peGameSceneName = "SampleScene_PE";
    [SerializeField] private float coverDuration = 0.38f;
    [SerializeField] private float revealToFocusDuration = 0.34f;
    [SerializeField] private float focusExpandDuration = 0.28f;
    [SerializeField] private float startSceneFocusRadius = 0.2f;
    [SerializeField] private float sampleSceneFocusRadius = 0.1f;
    [SerializeField] private float fallbackFocusRadius = 0.1f;
    [SerializeField] private Vector2 startSceneFocusShapeScale = Vector2.one;
    [SerializeField] private Vector2 sampleSceneFocusShapeScale = Vector2.one;
    [SerializeField] private Vector2 fallbackFocusShapeScale = Vector2.one;
    [SerializeField] private float startSceneFocusHoldDuration = 0.2f;
    [SerializeField] private float focusHoldDuration = 0.2f;
    [SerializeField] private AnimationCurve coverCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.72f, 1.06f),
        new Keyframe(1f, 1f));
    [SerializeField] private AnimationCurve revealToFocusCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.72f, 1.06f),
        new Keyframe(1f, 1f));
    [SerializeField] private AnimationCurve focusExpandCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.72f, 1.06f),
        new Keyframe(1f, 1f));
    [SerializeField] private int revealDelayFramesAfterLoad = 2;
    [SerializeField] private Color transitionColor = Color.black;

    private const string ProgressProperty = "_Progress";
    private const string CenterProperty = "_Center";
    private const string ShapeScaleProperty = "_ShapeScale";
    private const string StartSceneName = "StartScene";
    private Material runtimeTransitionMaterial;
    private GameObject departureFocusTarget;
    private bool transitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureTransitionView();

        if (SceneManager.GetActiveScene().name == StartSceneName)
        {
            SetCenterFromCurrentScene();
            SetShapeScale(GetCurrentSceneFocusShapeScale());
            SetProgress(1f);
            transitionImage.raycastTarget = true;
            StartCoroutine(PlayStartSceneIntroRoutine());
        }
        else
        {
            SetProgress(0f);
            transitionImage.raycastTarget = false;
        }
    }

    public void LoadGameSceneWithTransition(GameObject focusTarget = null)
    {
#if UNITY_ANDROID || UNITY_IOS
        LoadSceneWithTransition(peGameSceneName, focusTarget);
#else
        LoadSceneWithTransition(pcGameSceneName, focusTarget);
#endif
    }

    public void LoadSecondFloorWithTransition(GameObject focusTarget = null)
    {
        LoadSceneWithTransition("SampleScene_PC_SecondFloor", focusTarget);
    }

    public void LoadSceneWithTransition(string sceneName, GameObject focusTarget = null)
    {
        if (!transitioning && !string.IsNullOrWhiteSpace(sceneName))
        {
            departureFocusTarget = focusTarget;
            StartCoroutine(LoadSceneRoutine(sceneName));
        }
    }

    private IEnumerator PlayStartSceneIntroRoutine()
    {
        transitioning = true;
        yield return null;
        yield return null;
        yield return PlayProgress(1f, 0f, coverDuration, coverCurve);
        transitionImage.raycastTarget = false;
        transitioning = false;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        transitioning = true;
        transitionImage.raycastTarget = true;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"Unable to load scene '{sceneName}'. Ensure it exists and is enabled in Build Settings.");
            transitionImage.raycastTarget = false;
            transitioning = false;
            yield break;
        }

        operation.allowSceneActivation = false;
        SetCenterFromCurrentScene(true);
        SetShapeScale(startSceneFocusShapeScale);
        yield return PlayDepartureCover();
        departureFocusTarget = null;

        while (operation.progress < 0.9f)
            yield return null;

        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        yield return WaitForLoadedSceneToSettle();
        SetCenterFromCurrentScene();
        SetShapeScale(GetCurrentSceneFocusShapeScale());
        yield return PlayFocusedReveal();
        transitionImage.raycastTarget = false;
        transitioning = false;
    }

    private IEnumerator WaitForLoadedSceneToSettle()
    {
        SetProgress(1f);

        int frameCount = Mathf.Max(0, revealDelayFramesAfterLoad);
        for (int i = 0; i < frameCount; i++)
            yield return null;

        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
    }

    private IEnumerator PlayDepartureCover()
    {
        float focusRadius = startSceneFocusRadius;
        yield return PlayProgress(0f, 1f - focusRadius, coverDuration, coverCurve);
        if (startSceneFocusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(startSceneFocusHoldDuration);
        yield return PlayProgress(1f - focusRadius, 1f, coverDuration, coverCurve);
    }

    private IEnumerator PlayFocusedReveal()
    {
        float focusRadius = GetCurrentSceneFocusRadius();
        yield return PlayProgress(1f, 1f - focusRadius, revealToFocusDuration, revealToFocusCurve);
        if (focusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(focusHoldDuration);
        yield return PlayProgress(1f - focusRadius, 0f, focusExpandDuration, focusExpandCurve);
    }

    private float GetCurrentSceneFocusRadius()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "SampleScene_PC" || sceneName == "SampleScene_PE"
            ? sampleSceneFocusRadius
            : fallbackFocusRadius;
    }

    private Vector2 GetCurrentSceneFocusShapeScale()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == StartSceneName)
            return startSceneFocusShapeScale;
        return sceneName == "SampleScene_PC" || sceneName == "SampleScene_PE"
            ? sampleSceneFocusShapeScale
            : fallbackFocusShapeScale;
    }

    private IEnumerator PlayProgress(float from, float to, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        SetProgress(from);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float curveValue = curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
            SetProgress(Mathf.LerpUnclamped(from, to, curveValue));
            yield return null;
        }

        SetProgress(to);
    }

    private void EnsureTransitionView()
    {
        if (transitionImage != null)
            return;

        Canvas canvas = new GameObject("TransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("TransitionImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvas.transform, false);
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        transitionImage = imageObject.GetComponent<Image>();
        transitionImage.color = transitionColor;
        runtimeTransitionMaterial = new Material(transitionMaterial);
        transitionImage.material = runtimeTransitionMaterial;
    }

    private void SetCenterFromCurrentScene(bool useDepartureFocus = false)
    {
        SceneTransitionFocusManager focusManager = FindObjectOfType<SceneTransitionFocusManager>();
        GameObject focusTarget = useDepartureFocus && departureFocusTarget != null
            ? departureFocusTarget
            : focusManager != null ? focusManager.FocusTarget : null;
        Vector2 center = new Vector2(0.5f, 0.5f);

        if (focusTarget != null)
        {
            RectTransform focusRect = focusTarget.GetComponent<RectTransform>();
            if (focusRect != null)
            {
                Canvas canvas = focusRect.GetComponentInParent<Canvas>();
                Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, focusRect.TransformPoint(focusRect.rect.center));
                center = new Vector2(screenPoint.x / Screen.width, screenPoint.y / Screen.height);
            }
            else
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Vector3 viewportPoint = camera.WorldToViewportPoint(focusTarget.transform.position);
                    if (viewportPoint.z > 0f)
                        center = new Vector2(viewportPoint.x, viewportPoint.y);
                }
            }
        }

        if (runtimeTransitionMaterial != null)
            runtimeTransitionMaterial.SetVector(CenterProperty, center);
    }

    private void SetShapeScale(Vector2 shapeScale)
    {
        if (runtimeTransitionMaterial != null)
            runtimeTransitionMaterial.SetVector(ShapeScaleProperty, new Vector4(Mathf.Max(0.001f, shapeScale.x), Mathf.Max(0.001f, shapeScale.y), 0f, 0f));
    }

    private void SetProgress(float progress)
    {
        if (runtimeTransitionMaterial != null)
            runtimeTransitionMaterial.SetFloat(ProgressProperty, Mathf.Clamp01(progress));

        if (transitionImage != null)
            transitionImage.enabled = progress > 0.001f;
    }

    private static float EaseOutBack(float value)
    {
        float t = Mathf.Clamp01(value) - 1f;
        return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
    }

    private static float EaseInOutQuad(float value)
    {
        value = Mathf.Clamp01(value);
        return value < 0.5f ? 2f * value * value : 1f - Mathf.Pow(-2f * value + 2f, 2f) * 0.5f;
    }
}
