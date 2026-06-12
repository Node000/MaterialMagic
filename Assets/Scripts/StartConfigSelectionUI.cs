using System;
using System.Collections.Generic;
using UnityEngine;

public class StartConfigSelectionUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private StartConfigBookmarkUI startConfigBookmarkPrefab;
    [SerializeField] private float rootY = 220f;
    [SerializeField] private Vector2 rootSize = new Vector2(760f, 620f);
    [SerializeField] private float bookmarkVerticalSpacing = 228f;
    [SerializeField] private float bookmarkInitialX = 340f;
    [SerializeField] private float bookmarkReadyX = 340f;
    [SerializeField] private float bookmarkDisplayX = 340f;
    [SerializeField] private float bookmarkShowStagger = 0.045f;
    [SerializeField] private float bookmarkHideStagger = 0.025f;

    private readonly List<StartConfigBookmarkUI> bookmarks = new List<StartConfigBookmarkUI>();
    private readonly List<PlayerStartConfigData> startConfigs = new List<PlayerStartConfigData>();
    private string visibleOnlyConfigId;
    private bool configsLoaded;

    public PlayerStartConfigData SelectedConfig { get; private set; }
    public bool IsShowing => root != null && root.gameObject.activeSelf;
    public int VisibleConfigWindowCount => CountVisibleBookmarks();
    public int ExpectedConfigWindowCount
    {
        get
        {
            LoadStartConfigs();
            return GetVisibleConfigCount();
        }
    }
    public bool HasExpectedConfigWindows => root != null && root.gameObject.activeSelf && CountVisibleBookmarks() == ExpectedConfigWindowCount;

    public event Action<PlayerStartConfigData> ConfigSelected;

    private void Awake()
    {
        ResolveReferences();
        ApplyRootLayout();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i]?.KillTweens();
    }

    public void Prewarm()
    {
        ResolveReferences();
        if (root == null || startConfigBookmarkPrefab == null)
            return;

        LoadStartConfigs();
        bool rootWasActive = root.gameObject.activeSelf;
        if (!rootWasActive)
            root.gameObject.SetActive(true);

        ApplyRootLayout();
        EnsureBookmarkPool();
        HideBookmarksImmediate();

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
        if (root == null || startConfigBookmarkPrefab == null)
            return;

        visibleOnlyConfigId = onlyConfigId;
        LoadStartConfigs();
        SelectedConfig = null;
        root.gameObject.SetActive(true);
        ApplyRootLayout();
        EnsureBookmarkPool();

        for (int i = 0; i < bookmarks.Count; i++)
        {
            StartConfigBookmarkUI bookmark = bookmarks[i];
            if (bookmark == null)
                continue;

            bool visible = IsConfigVisible(bookmark.Config);
            bookmark.gameObject.SetActive(visible);
            if (!visible)
                continue;

            bookmark.RectTransform.anchoredPosition = new Vector2(bookmarkReadyX, -i * bookmarkVerticalSpacing);
            bookmark.SetSelectedImmediate(false);
            bookmark.Show(bookmarkInitialX, bookmarkReadyX, i * bookmarkShowStagger);
        }
    }

    public void Hide()
    {
        SelectedConfig = null;
        visibleOnlyConfigId = null;
        if (CountVisibleBookmarks() == 0)
        {
            if (root != null)
                root.gameObject.SetActive(false);
            return;
        }

        int hideIndex = 0;
        for (int i = bookmarks.Count - 1; i >= 0; i--)
        {
            StartConfigBookmarkUI bookmark = bookmarks[i];
            if (bookmark == null || !bookmark.gameObject.activeSelf)
                continue;

            bookmark.Hide(bookmarkInitialX, hideIndex * bookmarkHideStagger, HideBookmark);
            hideIndex++;
        }
    }

    public bool EnsureConfigWindows()
    {
        ResolveReferences();
        if (root == null || startConfigBookmarkPrefab == null)
            return false;

        LoadStartConfigs();
        if (root.gameObject.activeSelf)
            EnsureBookmarkPool();
        int expectedVisibleCount = GetVisibleConfigCount();
        if (root.gameObject.activeSelf && CountVisibleBookmarks() == expectedVisibleCount)
        {
            ApplySelectionVisuals();
            return true;
        }

        string selectedId = SelectedConfig != null ? SelectedConfig.id : string.Empty;
        ShowInternal(visibleOnlyConfigId);
        PlayerStartConfigData restoredConfig = FindConfigById(selectedId);
        if (restoredConfig != null)
            SelectConfig(restoredConfig);
        return CountVisibleBookmarks() == expectedVisibleCount;
    }

    public bool Contains(Transform hit)
    {
        return root != null && hit != null && hit.IsChildOf(root);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = transform as RectTransform;
    }

    private void ApplyRootLayout()
    {
        if (root == null)
            return;

        root.anchoredPosition = new Vector2(0f, rootY);
        root.sizeDelta = rootSize;
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
        bool needsRebuild = bookmarks.Count != startConfigs.Count;
        if (!needsRebuild)
        {
            for (int i = 0; i < bookmarks.Count; i++)
            {
                if (bookmarks[i] == null)
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
            return;

        ClearBookmarksImmediate();
        for (int i = 0; i < startConfigs.Count; i++)
        {
            PlayerStartConfigData config = startConfigs[i];
            StartConfigBookmarkUI bookmark = Instantiate(startConfigBookmarkPrefab, root);
            bookmark.gameObject.SetActive(true);
            bookmark.name = "StartConfigBookmark_" + config.id;
            bookmark.RectTransform.anchoredPosition = new Vector2(bookmarkReadyX, -i * bookmarkVerticalSpacing);
            bookmark.Bind(config, SelectConfig, RequestClose);
            bookmark.SetSelectedImmediate(false);
            bookmark.HideImmediate();
            bookmarks.Add(bookmark);
        }
    }

    private void SelectConfig(PlayerStartConfigData config)
    {
        SelectedConfig = config;
        ApplySelectionVisuals();
        ConfigSelected?.Invoke(config);
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < bookmarks.Count; i++)
        {
            StartConfigBookmarkUI bookmark = bookmarks[i];
            if (bookmark == null)
                continue;

            bool isSelected = bookmark.Config == SelectedConfig;
            if (bookmark.gameObject.activeSelf)
                bookmark.SetSelected(isSelected, bookmarkReadyX, bookmarkDisplayX);
            else
                bookmark.SetSelectedImmediate(isSelected);
        }
    }

    private void RequestClose(StartConfigBookmarkUI bookmark)
    {
        if (bookmark == null)
            return;

        if (bookmark.Config == SelectedConfig)
            SelectConfig(null);
        bookmark.Hide(bookmarkInitialX, 0f, HideBookmark);
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
        return config != null && (string.IsNullOrEmpty(visibleOnlyConfigId) || config.id == visibleOnlyConfigId);
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
            root.gameObject.SetActive(false);
    }

    private void ClearBookmarksImmediate()
    {
        for (int i = 0; i < bookmarks.Count; i++)
        {
            if (bookmarks[i] != null)
                Destroy(bookmarks[i].gameObject);
        }
        bookmarks.Clear();
    }
}
