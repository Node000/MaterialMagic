using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartMenuButtonGroupUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private RectTransform buttonGroup;
    [SerializeField] private string confirmStartText = "确认开始";
    [SerializeField] private string confirmExitText = "确认退出";
    [SerializeField] private Color startButtonNormalColor = new Color(0.28f, 0.19f, 0.45f, 1f);
    [SerializeField] private Color startButtonConfirmColor = new Color(0.85f, 0.54f, 0.18f, 1f);
    [SerializeField] private Color optionSelectedColor = new Color(0.85f, 0.54f, 0.18f, 1f);
    [SerializeField] private Vector2 buttonGroupPosition = new Vector2(520f, -20f);
    [SerializeField] private float buttonGroupMoveDuration = 0.36f;
    [SerializeField] private Ease buttonGroupMoveEase = Ease.OutCubic;
    [SerializeField] private float pointerRepelMaxDistance = 520f;
    [SerializeField] private float pointerRepelMaxOffset = 28f;
    [SerializeField] private float pointerRepelLerpSpeed = 12f;
    [SerializeField] private float selectedOptionScale = 1.12f;
    [SerializeField] private float selectedOptionRepelDistance = 34f;
    [SerializeField] private float selectedOptionLerpSpeed = 14f;

    private readonly List<RectTransform> optionRects = new List<RectTransform>();
    private readonly List<Image> optionImages = new List<Image>();
    private readonly List<Color> optionBaseColors = new List<Color>();
    private readonly List<Vector2> optionBasePositions = new List<Vector2>();
    private readonly List<Vector2> optionCurrentOffsets = new List<Vector2>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private Canvas rootCanvas;
    private Text startButtonText;
    private Text exitButtonText;
    private Image startButtonImage;
    private string originalStartText;
    private string originalExitText;
    private int selectedOptionIndex = -1;
    private int activeOptionIndex = -1;
    private Tween buttonGroupTween;

    public event Action StartClicked;
    public event Action ContinueClicked;
    public event Action SettingsClicked;
    public event Action ExitClicked;

    private void Awake()
    {
        ResolveReferences();
        startButton.onClick.AddListener(HandleStartClicked);
        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);
        settingsButton.onClick.AddListener(HandleSettingsClicked);
        exitButton.onClick.AddListener(HandleExitClicked);

        startButtonText = startButton.GetComponentInChildren<Text>(true);
        exitButtonText = exitButton.GetComponentInChildren<Text>(true);
        startButtonImage = startButton.GetComponent<Image>();
        originalStartText = startButtonText != null ? startButtonText.text : "开始游戏";
        originalExitText = exitButtonText != null ? exitButtonText.text : "退出游戏";

        if (startButtonText != null && string.IsNullOrEmpty(startButtonText.text))
            startButtonText.text = originalStartText;
        if (startButtonImage != null)
            startButtonImage.color = startButtonNormalColor;

        PositionButtonGroupRight();
        CacheOptionRects();
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStartClicked);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinueClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(HandleExitClicked);
        buttonGroupTween?.Kill(false);
    }

    private void Update()
    {
        int hoverOptionIndex = GetPointerOptionIndex();
        selectedOptionIndex = hoverOptionIndex >= 0 ? hoverOptionIndex : activeOptionIndex;
        UpdatePointerRepel();
    }

    public bool Contains(Transform hit)
    {
        return buttonGroup != null && hit != null && hit.IsChildOf(buttonGroup);
    }

    public void SetStartConfigMode(bool selecting)
    {
        int startIndex = GetOptionIndex(startButton);
        if (startIndex >= 0 && startIndex < optionBaseColors.Count)
            optionBaseColors[startIndex] = selecting ? startButtonConfirmColor : startButtonNormalColor;
        if (startButtonText != null)
            startButtonText.text = selecting ? confirmStartText : originalStartText;
        if (startButtonImage != null)
            startButtonImage.color = selecting ? startButtonConfirmColor : startButtonNormalColor;
        activeOptionIndex = selecting ? startIndex : activeOptionIndex == startIndex ? -1 : activeOptionIndex;
        MoveButtonGroup();
    }

    public void SetSettingsMode(bool showing)
    {
        int settingsIndex = GetOptionIndex(settingsButton);
        activeOptionIndex = showing ? settingsIndex : activeOptionIndex == settingsIndex ? -1 : activeOptionIndex;
    }

    public void SetExitConfirmMode(bool confirming)
    {
        int exitIndex = GetOptionIndex(exitButton);
        if (exitButtonText != null)
            exitButtonText.text = confirming ? confirmExitText : originalExitText;
        activeOptionIndex = confirming ? exitIndex : activeOptionIndex == exitIndex ? -1 : activeOptionIndex;
    }

    public void ClearActiveOption()
    {
        activeOptionIndex = -1;
    }

    private void ResolveReferences()
    {
        if (buttonGroup == null)
            buttonGroup = transform as RectTransform;
        if (startButton == null)
            startButton = transform.Find("StartButton")?.GetComponent<Button>();
        if (continueButton == null)
            continueButton = transform.Find("ContinueButton")?.GetComponent<Button>();
        if (settingsButton == null)
            settingsButton = transform.Find("SettingsButton")?.GetComponent<Button>();
        if (exitButton == null)
            exitButton = transform.Find("ExitButton")?.GetComponent<Button>();
    }

    private void HandleStartClicked()
    {
        StartClicked?.Invoke();
    }

    private void HandleContinueClicked()
    {
        ContinueClicked?.Invoke();
    }

    private void HandleSettingsClicked()
    {
        SettingsClicked?.Invoke();
    }

    private void HandleExitClicked()
    {
        ExitClicked?.Invoke();
    }

    private void PositionButtonGroupRight()
    {
        if (buttonGroup == null)
            return;

        buttonGroup.anchorMin = new Vector2(0.5f, 0.5f);
        buttonGroup.anchorMax = new Vector2(0.5f, 0.5f);
        buttonGroup.pivot = new Vector2(0.5f, 0.5f);
        buttonGroup.anchoredPosition = buttonGroupPosition;
        buttonGroup.sizeDelta = new Vector2(420f, 460f);
    }

    private void MoveButtonGroup()
    {
        if (buttonGroup == null)
            return;

        buttonGroupTween?.Kill(false);
        buttonGroupTween = buttonGroup.DOAnchorPos(buttonGroupPosition, buttonGroupMoveDuration).SetEase(buttonGroupMoveEase).SetUpdate(true).SetTarget(this);
    }

    private void CacheOptionRects()
    {
        optionRects.Clear();
        optionImages.Clear();
        optionBaseColors.Clear();
        optionBasePositions.Clear();
        optionCurrentOffsets.Clear();
        if (buttonGroup == null)
            return;

        for (int i = 0; i < buttonGroup.childCount; i++)
        {
            RectTransform rect = buttonGroup.GetChild(i) as RectTransform;
            if (rect == null)
                continue;

            optionRects.Add(rect);
            Image image = rect.GetComponent<Image>();
            optionImages.Add(image);
            optionBaseColors.Add(image != null ? image.color : Color.white);
            optionBasePositions.Add(rect.anchoredPosition);
            optionCurrentOffsets.Add(Vector2.zero);
        }
    }

    private void UpdatePointerRepel()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null || optionRects.Count == 0)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, camera, out Vector2 pointerLocal);

        for (int i = 0; i < optionRects.Count; i++)
        {
            RectTransform rect = optionRects[i];
            if (rect == null)
                continue;

            Vector2 rectLocal = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect).center;
            Vector2 away = rectLocal - pointerLocal;
            float distance = away.magnitude;
            Vector2 offset = Vector2.zero;
            if (distance > 0.01f)
            {
                float strength = Mathf.Clamp01(distance / pointerRepelMaxDistance) * pointerRepelMaxOffset;
                offset = away.normalized * strength;
            }

            Vector2 targetOffset = offset + GetSelectedOptionOffset(i);
            optionCurrentOffsets[i] = Vector2.Lerp(optionCurrentOffsets[i], targetOffset, Time.unscaledDeltaTime * pointerRepelLerpSpeed);
            rect.anchoredPosition = optionBasePositions[i] + optionCurrentOffsets[i];
            Vector3 targetScale = i == selectedOptionIndex ? Vector3.one * selectedOptionScale : Vector3.one;
            rect.localScale = Vector3.Lerp(rect.localScale, targetScale, Time.unscaledDeltaTime * selectedOptionLerpSpeed);
            if (i < optionImages.Count && optionImages[i] != null)
            {
                Color targetColor = i == activeOptionIndex ? optionSelectedColor : optionBaseColors[i];
                optionImages[i].color = Color.Lerp(optionImages[i].color, targetColor, Time.unscaledDeltaTime * selectedOptionLerpSpeed);
            }
        }
    }

    private Vector2 GetSelectedOptionOffset(int index)
    {
        if (selectedOptionIndex < 0 || index == selectedOptionIndex)
            return Vector2.zero;

        int delta = index - selectedOptionIndex;
        float distance = Mathf.Abs(delta);
        if (distance < 0.01f)
            return Vector2.zero;

        float strength = selectedOptionRepelDistance / distance;
        return delta < 0 ? Vector2.up * strength : Vector2.down * strength;
    }

    private int GetPointerOptionIndex()
    {
        if (EventSystem.current == null)
            return -1;

        if (pointerEventData == null)
            pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);
        for (int i = 0; i < raycastResults.Count; i++)
        {
            Transform hit = raycastResults[i].gameObject.transform;
            for (int optionIndex = 0; optionIndex < optionRects.Count; optionIndex++)
            {
                RectTransform rect = optionRects[optionIndex];
                if (rect != null && hit.IsChildOf(rect))
                    return optionIndex;
            }
        }
        return -1;
    }

    public void RefreshContinueButton(bool hasRun)
    {
        if (continueButton != null)
            continueButton.interactable = hasRun;
    }

    private int GetOptionIndex(Button button)
    {
        if (button == null)
            return -1;

        RectTransform rect = button.transform as RectTransform;
        for (int i = 0; i < optionRects.Count; i++)
        {
            if (optionRects[i] == rect)
                return i;
        }
        return -1;
    }
}
