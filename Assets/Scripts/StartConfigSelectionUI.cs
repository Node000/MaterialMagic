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
    [SerializeField] private float bookmarkInitialX = -940f;
    [SerializeField] private float bookmarkReadyX = -720f;
    [SerializeField] private float bookmarkDisplayX = -40f;
    [SerializeField] private float bookmarkShowStagger = 0.045f;
    [SerializeField] private float bookmarkHideStagger = 0.025f;

    private readonly List<StartConfigBookmarkUI> bookmarks = new List<StartConfigBookmarkUI>();
    private readonly List<PlayerStartConfigData> startConfigs = new List<PlayerStartConfigData>();
    private bool configsLoaded;

    public PlayerStartConfigData SelectedConfig { get; private set; }
    public bool IsShowing => root != null && root.gameObject.activeSelf;

    public event Action<PlayerStartConfigData> ConfigSelected;

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;
        if (root != null)
        {
            root.anchoredPosition = new Vector2(0f, rootY);
            root.sizeDelta = rootSize;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i]?.KillTweens();
    }

    public void Show()
    {
        if (root == null || startConfigBookmarkPrefab == null)
            return;

        LoadStartConfigs();
        SelectedConfig = null;
        root.gameObject.SetActive(true);
        root.anchoredPosition = new Vector2(0f, rootY);
        root.sizeDelta = rootSize;
        ClearBookmarksImmediate();

        for (int i = 0; i < startConfigs.Count; i++)
        {
            StartConfigBookmarkUI bookmark = Instantiate(startConfigBookmarkPrefab, root);
            bookmark.RectTransform.anchoredPosition = new Vector2(bookmarkInitialX, -i * bookmarkVerticalSpacing);
            bookmark.Bind(startConfigs[i], SelectConfig);
            bookmark.Show(bookmarkInitialX, bookmarkReadyX, i * bookmarkShowStagger);
            bookmarks.Add(bookmark);
        }
    }

    public void Hide()
    {
        SelectedConfig = null;
        for (int i = bookmarks.Count - 1; i >= 0; i--)
            bookmarks[i]?.Hide(bookmarkInitialX, (bookmarks.Count - 1 - i) * bookmarkHideStagger, DestroyBookmark);
    }

    public bool Contains(Transform hit)
    {
        return root != null && hit != null && hit.IsChildOf(root);
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

    private void SelectConfig(PlayerStartConfigData config)
    {
        SelectedConfig = config;
        for (int i = 0; i < bookmarks.Count; i++)
            bookmarks[i].SetSelected(bookmarks[i].Config == SelectedConfig, bookmarkReadyX, bookmarkDisplayX);
        ConfigSelected?.Invoke(config);
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

    private void DestroyBookmark(StartConfigBookmarkUI bookmark)
    {
        if (bookmark != null)
            Destroy(bookmark.gameObject);
        bookmarks.Remove(bookmark);
        if (bookmarks.Count == 0 && root != null)
            root.gameObject.SetActive(false);
    }
}
