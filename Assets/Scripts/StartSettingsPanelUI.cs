using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class StartSettingsPanelUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button windowCloseButton;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private Vector2 shownPosition = new Vector2(320f, 0f);
    [SerializeField] private Vector2 hiddenPosition = new Vector2(-980f, 0f);
    [SerializeField] private float moveDuration = 0.46f;
    [SerializeField] private Ease showEase = Ease.OutQuart;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    private Tween moveTween;
    private bool dragging;
    private Vector2 dragOffset;

    public bool IsShowing => gameObject.activeSelf;
    public event Action Hidden;

    private void Awake()
    {
        ResolveReferences();
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        if (windowCloseButton != null)
            windowCloseButton.onClick.AddListener(Hide);
        InitializeControls();
        if (panelRect != null)
            panelRect.anchoredPosition = hiddenPosition;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
        if (windowCloseButton != null)
            windowCloseButton.onClick.RemoveListener(Hide);
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveListener(SetLanguage);
        moveTween?.Kill(false);
    }

    public void Show()
    {
        if (panelRect == null || gameObject.activeSelf)
            return;

        ResolveReferences();
        InitializeControls();
        gameObject.SetActive(true);
        moveTween?.Kill(false);
        panelRect.anchoredPosition = hiddenPosition;
        moveTween = panelRect.DOAnchorPos(shownPosition, moveDuration)
            .SetEase(showEase)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || panelRect == null)
            return;

        moveTween?.Kill(false);
        moveTween = panelRect.DOAnchorPos(hiddenPosition, moveDuration)
            .SetEase(hideEase)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                Hidden?.Invoke();
            });
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void ResolveReferences()
    {
        if (panelRect == null)
            panelRect = transform as RectTransform;
        if (closeButton == null)
            closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
        if (windowCloseButton == null)
            windowCloseButton = transform.Find("PopupDragonWindowBackground/Frame/TitleBar/Close")?.GetComponent<Button>();
        if (musicSlider == null)
            musicSlider = transform.Find("MusicSlider")?.GetComponent<Slider>();
        if (sfxSlider == null)
            sfxSlider = transform.Find("SfxSlider")?.GetComponent<Slider>();
        if (languageDropdown == null)
            languageDropdown = transform.Find("LanguageDropdown")?.GetComponent<TMP_Dropdown>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (panelRect == null || !CanDragFrom(eventData))
            return;

        RectTransform parent = panelRect.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, GetEventCamera(eventData), out Vector2 localPoint))
            return;

        moveTween?.Kill(false);
        dragging = true;
        dragOffset = panelRect.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || panelRect == null)
            return;

        RectTransform parent = panelRect.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, GetEventCamera(eventData), out Vector2 localPoint))
            return;

        panelRect.anchoredPosition = localPoint + dragOffset;
        shownPosition = panelRect.anchoredPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    private bool CanDragFrom(PointerEventData eventData)
    {
        Transform hit = eventData.pointerPressRaycast.gameObject != null
            ? eventData.pointerPressRaycast.gameObject.transform
            : eventData.pointerCurrentRaycast.gameObject != null ? eventData.pointerCurrentRaycast.gameObject.transform : null;
        if (hit == null || !hit.IsChildOf(transform))
            return false;
        if (closeButton != null && hit.IsChildOf(closeButton.transform))
            return false;
        if (windowCloseButton != null && hit.IsChildOf(windowCloseButton.transform))
            return false;
        if (hit.GetComponentInParent<Slider>() != null)
            return false;
        if (hit.GetComponentInParent<TMP_Dropdown>() != null)
            return false;
        if (hit.GetComponentInParent<Button>() != null)
            return false;
        return true;
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return eventData.pressEventCamera != null ? eventData.pressEventCamera : canvas != null ? canvas.worldCamera : null;
    }

    private void InitializeControls()
    {
        InitializeSliders();
        InitializeLanguageDropdown();
    }

    private void InitializeSliders()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.8f);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
    }

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null)
            return;

        languageDropdown.onValueChanged.RemoveListener(SetLanguage);
        languageDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(LocalizationSystem.LanguageCount);
        for (int i = 0; i < LocalizationSystem.LanguageCount; i++)
            options.Add(new TMP_Dropdown.OptionData(LocalizationSystem.GetLanguageDisplayName(i)));
        languageDropdown.AddOptions(options);
        languageDropdown.SetValueWithoutNotify(LocalizationSystem.GetCurrentLanguageIndex());
        languageDropdown.RefreshShownValue();
        languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    private void SetMusicVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    private void SetSfxVolume(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxVolume(value);
    }

    private void SetLanguage(int optionIndex)
    {
        LocalizationSystem.SetLanguage(LocalizationSystem.GetLanguageCode(optionIndex));
    }
}
