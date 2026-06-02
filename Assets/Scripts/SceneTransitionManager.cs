using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private Image transitionImage;
    [SerializeField] private Material transitionMaterial;
    [SerializeField] private float transitionDuration = 0.6f;
    [SerializeField] private Color transitionColor = Color.black;

    private const string ProgressProperty = "_Progress";
    private const string StartSceneName = "StartScene";
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

    public void LoadSceneWithTransition(string sceneName)
    {
        if (!transitioning)
            StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator PlayStartSceneIntroRoutine()
    {
        transitioning = true;
        yield return null;
        yield return PlayTransition(1f, 0f);
        transitionImage.raycastTarget = false;
        transitioning = false;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        transitioning = true;
        transitionImage.raycastTarget = true;
        yield return PlayTransition(0f, 1f);
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
            yield return null;

        yield return PlayTransition(1f, 0f);
        transitionImage.raycastTarget = false;
        transitioning = false;
    }

    private IEnumerator PlayTransition(float from, float to)
    {
        float elapsed = 0f;
        SetProgress(from);

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProgress(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / transitionDuration)));
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
        transitionImage.material = transitionMaterial;
    }

    private void SetProgress(float progress)
    {
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(ProgressProperty, progress);

        if (transitionImage != null)
            transitionImage.enabled = progress > 0.001f;
    }
}
