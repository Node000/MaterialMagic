using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartConfigSelectionUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private StartConfigBookmarkUI startConfigBookmark;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private PopupDragonBackgroundUI popupDragonBackground;
    [SerializeField] private StartConfigEnchantTransitionUI enchantTransition;
    [Header("弹窗龙接管")]
    [SerializeField] private bool replaceDragonFrontWindow;
    [SerializeField] private bool alignToDragonFrontWindow;
    [SerializeField] private Vector2 dragonFrontWindowOffset;
    [SerializeField] private bool inheritDragonFrontRotation;
    [Header("配置切换")]
    [SerializeField] private bool useSwitchTransition;

    private readonly List<StartConfigBookmarkUI> bookmarks = new List<StartConfigBookmarkUI>();
    private readonly List<PlayerStartConfigData> startConfigs = new List<PlayerStartConfigData>();
    private string visibleOnlyConfigId;
    private bool configsLoaded;
    private bool switchingConfig;
    private int currentConfigIndex = -1;

    public PlayerStartConfigData SelectedConfig { get; private set; }
    public bool IsShowing => root != null && root.gameObject.activeSelf;
    public bool IsSwitchingConfig => switchingConfig;
    public int VisibleConfigWindowCount => CountVisibleBookmarks();
    public int ExpectedConfigWindowCount
    {
        get
        {
            LoadStartConfigs();
            return GetVisibleConfigCount() > 0 ? 1 : 0;
        }
    }
    public bool HasExpectedConfigWindows => root != null && root.gameObject.activeSelf && CountVisibleBookmarks() == ExpectedConfigWindowCount;

    public event Action<PlayerStartConfigData> ConfigSelected;
    public event Action Closed;

    private void Awake()
    {
        ResolveReferences();
        HookNavigationButtons();
        LocalizationSystem.LanguageChanged += RefreshBookmarksForLanguage;
    }

    private void OnDestroy()
    {
        CancelSwitchTransition();
        LocalizationSystem.LanguageChanged -= RefreshBookmarksForLanguage;
        UnhookNavigationButtons();
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i]?.KillTweens();
    }

    public void Prewarm()
    {
        ResolveReferences();
        if (root == null || startConfigBookmark == null)
            return;

        LoadStartConfigs();
        bool rootWasActive = root.gameObject.activeSelf;
        if (!rootWasActive)
            root.gameObject.SetActive(true);

        EnsureBookmarkPool();
        HideBookmarksImmediate();
        UpdateNavigationButtons();

        if (!rootWasActive)
            root.gameObject.SetActive(false);
    }

    public void Show()
    {
        ShowInternal(null);
    }

    public void ShowOnly(string configId)
    {
        ShowInternal(configId);
    }

    private void ShowInternal(string onlyConfigId)
    {
        ResolveReferences();
        if (root == null || startConfigBookmark == null)
            return;

        visibleOnlyConfigId = onlyConfigId;
        LoadStartConfigs();
        currentConfigIndex = FindFirstVisibleConfigIndex();
        if (currentConfigIndex < 0)
            return;

        PlayerStartConfigData currentConfig = startConfigs[currentConfigIndex];
        SelectedConfig = IsConfigUnlocked(currentConfig) ? currentConfig : null;
        root.gameObject.SetActive(true);
        SetDragonFrontWindowReplacement(true);
        ApplyDragonFrontWindowPose();
        EnsureBookmarkPool();
        ShowCurrentBookmark(true);
        UpdateNavigationButtons();
        ConfigSelected?.Invoke(SelectedConfig);
    }

    public void Hide()
    {
        CancelSwitchTransition();
        SelectedConfig = null;
        visibleOnlyConfigId = null;
        currentConfigIndex = -1;
        UpdateNavigationButtons();
        if (CountVisibleBookmarks() == 0)
        {
            if (root != null)
                root.gameObject.SetActive(false);
            SetDragonFrontWindowReplacement(false);
            return;
        }

        StartConfigBookmarkUI bookmark = bookmarks.Count > 0 ? bookmarks[0] : null;
        if (bookmark == null || !bookmark.gameObject.activeSelf)
        {
            if (root != null)
                root.gameObject.SetActive(false);
            SetDragonFrontWindowReplacement(false);
            return;
        }

        bookmark.Hide(bookmark.RectTransform.anchoredPosition.x, 0f, HideBookmark);
    }

    public bool EnsureConfigWindows()
    {
        ResolveReferences();
        if (root == null || startConfigBookmark == null)
            return false;

        LoadStartConfigs();
        if (!root.gameObject.activeSelf || SelectedConfig == null)
            ShowInternal(visibleOnlyConfigId);
        else
        {
            ApplyDragonFrontWindowPose();
            EnsureBookmarkPool();
            ShowCurrentBookmark(false);
            UpdateNavigationButtons();
        }
        return SelectedConfig != null && CountVisibleBookmarks() == ExpectedConfigWindowCount;
    }

    public void ShowPreviousConfig()
    {
        ShowAdjacentConfig(-1);
    }

    public void ShowNextConfig()
    {
        ShowAdjacentConfig(1);
    }

    public bool Contains(Transform hit)
    {
        return root != null && hit != null && hit.IsChildOf(root);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = transform as RectTransform;
        if (previousButton == null)
            previousButton = transform.Find("PreviousButton")?.GetComponent<Button>() ?? transform.Find("StartConfigBookmark/PreviousButton")?.GetComponent<Button>();
        if (nextButton == null)
            nextButton = transform.Find("NextButton")?.GetComponent<Button>() ?? transform.Find("StartConfigBookmark/NextButton")?.GetComponent<Button>();
        if (startConfigBookmark == null)
            startConfigBookmark = GetComponent<StartConfigBookmarkUI>() ?? transform.Find("StartConfigBookmark")?.GetComponent<StartConfigBookmarkUI>();
        if (popupDragonBackground == null && transform.parent != null)
            popupDragonBackground = transform.parent.GetComponentInChildren<PopupDragonBackgroundUI>(true);
        if (enchantTransition == null)
            enchantTransition = GetComponent<StartConfigEnchantTransitionUI>();
    }

    private void LoadStartConfigs()
    {
        if (configsLoaded)
            return;

        configsLoaded = true;
        startConfigs.Clear();
        DataTable<PlayerStartConfigData> table = GameDataReader.LoadTable<PlayerStartConfigData>("StartConfig");
        if (table == null || table.items == null)
            return;

        for (int i = 0; i < table.items.Count; i++)
        {
            PlayerStartConfigData config = table.items[i];
            if (config != null && !string.IsNullOrEmpty(config.id))
                startConfigs.Add(config);
        }
    }

    private void EnsureBookmarkPool()
    {
        if (startConfigBookmark == null)
            ResolveReferences();
        if (startConfigBookmark == null)
            return;

        if (bookmarks.Count == 1 && bookmarks[0] == startConfigBookmark)
            return;

        ClearBookmarksImmediate();
        StartConfigBookmarkUI bookmark = startConfigBookmark;
        bookmark.SetSelectButtonVisible(false);
        bookmark.HideImmediate();
        bookmarks.Add(bookmark);
    }

    private void SelectConfig(PlayerStartConfigData config)
    {
        if (!IsConfigUnlocked(config))
            return;

        SelectedConfig = config;
        ShowCurrentBookmark(false);
        ConfigSelected?.Invoke(config);
    }

    private void RefreshBookmarksForLanguage()
    {
        ShowCurrentBookmark(false);
    }

    private void ApplySelectionVisuals()
    {
        ShowCurrentBookmark(false);
    }

    private void RequestClose(StartConfigBookmarkUI bookmark)
    {
        if (bookmark == null)
            return;

        Closed?.Invoke();
        if (IsShowing)
            Hide();
    }

    private void HookNavigationButtons()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(ShowPreviousConfig);
        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNextConfig);
    }

    private void UnhookNavigationButtons()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPreviousConfig);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNextConfig);
    }

    private void ShowAdjacentConfig(int direction)
    {
        if (!IsShowing || direction == 0 || switchingConfig)
            return;

        int nextIndex = FindAdjacentVisibleConfigIndex(direction);
        if (nextIndex < 0 || nextIndex == currentConfigIndex)
            return;

        if (!useSwitchTransition || enchantTransition == null || !enchantTransition.isActiveAndEnabled)
        {
            ApplyAdjacentConfig(nextIndex);
            return;
        }

        switchingConfig = true;
        SetNavigationInteractable(false);
        enchantTransition.PlaySwitch(() => ApplyAdjacentConfig(nextIndex), CompleteSwitchTransition);
    }

    private void ApplyAdjacentConfig(int nextIndex)
    {
        currentConfigIndex = nextIndex;
        PlayerStartConfigData currentConfig = startConfigs[currentConfigIndex];
        SelectedConfig = IsConfigUnlocked(currentConfig) ? currentConfig : null;
        ApplyDragonFrontWindowPose();
        ShowCurrentBookmark(false);
        UpdateNavigationButtons();
        ConfigSelected?.Invoke(SelectedConfig);
    }

    private void CompleteSwitchTransition()
    {
        switchingConfig = false;
        SetNavigationInteractable(true);
        UpdateNavigationButtons();
    }

    private void CancelSwitchTransition()
    {
        switchingConfig = false;
        enchantTransition?.Kill();
        SetNavigationInteractable(true);
    }

    private void ShowCurrentBookmark(bool animate)
    {
        if (bookmarks.Count == 0 || bookmarks[0] == null)
            return;

        PlayerStartConfigData currentConfig = currentConfigIndex >= 0 && currentConfigIndex < startConfigs.Count ? startConfigs[currentConfigIndex] : SelectedConfig;
        if (currentConfig == null)
            return;

        bool locked = !IsConfigUnlocked(currentConfig);
        StartConfigBookmarkUI bookmark = bookmarks[0];
        bookmark.gameObject.SetActive(true);
        bookmark.Bind(currentConfig, SelectConfig, RequestClose, locked);
        bookmark.SetSelectButtonVisible(locked);
        bookmark.SetSelectedImmediate(false);
        if (animate)
        {
            float readyX = bookmark.RectTransform.anchoredPosition.x;
            bookmark.Show(readyX, readyX, 0f);
        }
    }

    private int FindFirstVisibleConfigIndex()
    {
        for (int i = 0; i < startConfigs.Count; i++)
        {
            if (IsConfigVisible(startConfigs[i]))
                return i;
        }
        return -1;
    }

    private int FindAdjacentVisibleConfigIndex(int direction)
    {
        int visibleCount = GetVisibleConfigCount();
        if (visibleCount <= 1 || currentConfigIndex < 0)
            return currentConfigIndex;

        int step = direction > 0 ? 1 : -1;
        int index = currentConfigIndex;
        for (int i = 0; i < startConfigs.Count; i++)
        {
            index = (index + step + startConfigs.Count) % startConfigs.Count;
            if (IsConfigVisible(startConfigs[index]))
                return index;
        }
        return currentConfigIndex;
    }

    private void UpdateNavigationButtons()
    {
        bool hasMultipleConfigs = IsShowing && GetVisibleConfigCount() > 1;
        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(hasMultipleConfigs);
            previousButton.interactable = hasMultipleConfigs && !switchingConfig;
        }
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(hasMultipleConfigs);
            nextButton.interactable = hasMultipleConfigs && !switchingConfig;
        }
    }

    private void SetNavigationInteractable(bool interactable)
    {
        if (previousButton != null)
            previousButton.interactable = interactable;
        if (nextButton != null)
            nextButton.interactable = interactable;
    }

    private void SetDragonFrontWindowReplacement(bool replaced)
    {
        if (!replaceDragonFrontWindow || popupDragonBackground == null)
            return;

        popupDragonBackground.SetFrontWindowReplaced(replaced);
    }

    private void ApplyDragonFrontWindowPose()
    {
        if (!alignToDragonFrontWindow || root == null || popupDragonBackground == null)
            return;

        if (!popupDragonBackground.TryGetFrontWindowPose(out Vector2 anchoredPosition, out float rotation, out _))
            return;

        root.anchoredPosition = anchoredPosition + dragonFrontWindowOffset;
        if (inheritDragonFrontRotation)
            root.localEulerAngles = new Vector3(0f, 0f, rotation);
    }

    private PlayerStartConfigData FindConfigById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < startConfigs.Count; i++)
        {
            PlayerStartConfigData config = startConfigs[i];
            if (config != null && config.id == id)
                return config;
        }
        return null;
    }

    private int GetVisibleConfigCount()
    {
        LoadStartConfigs();
        int count = 0;
        for (int i = 0; i < startConfigs.Count; i++)
        {
            if (IsConfigVisible(startConfigs[i]))
                count++;
        }
        return count;
    }

    private bool IsConfigVisible(PlayerStartConfigData config)
    {
        return config != null && IsConfigUnlocked(config) && (string.IsNullOrEmpty(visibleOnlyConfigId) || config.id == visibleOnlyConfigId);
    }

    private bool IsConfigUnlocked(PlayerStartConfigData config)
    {
        return UnlockSystem.IsStartConfigUnlocked(config);
    }

    private int CountVisibleBookmarks()
    {
        if (root == null || !root.gameObject.activeSelf)
            return 0;

        int count = 0;
        for (int i = 0; i < bookmarks.Count; i++)
        {
            if (bookmarks[i] != null && bookmarks[i].gameObject.activeSelf)
                count++;
        }
        return count;
    }

    private void HideBookmarksImmediate()
    {
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i]?.HideImmediate();
        SelectedConfig = null;
    }

    private void HideBookmark(StartConfigBookmarkUI bookmark)
    {
        if (bookmark != null)
            bookmark.HideImmediate();
        if (CountVisibleBookmarks() == 0 && root != null)
        {
            root.gameObject.SetActive(false);
            SetDragonFrontWindowReplacement(false);
        }
    }

    private void ClearBookmarksImmediate()
    {
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i]?.HideImmediate();
        bookmarks.Clear();
    }
}
