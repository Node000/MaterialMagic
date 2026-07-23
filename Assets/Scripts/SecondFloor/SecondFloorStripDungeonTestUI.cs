using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SecondFloorStripDungeonTestUI : MonoBehaviour
{
    [Header("生成")]
    [SerializeField] private StripDungeonMapConfig config;
    [SerializeField] private int nextSeed = 1;

    [Header("布局")]
    [SerializeField] private float headerHeight = 130f;
    [SerializeField] private float cellSize = 42f;
    [SerializeField] private float cellSpacing = 6f;
    [SerializeField] private Vector2 generateButtonSize = new Vector2(180f, 56f);

    [Header("颜色")]
    [SerializeField] private Color backgroundColor = new Color(0.035f, 0.045f, 0.075f, 1f);
    [SerializeField] private Color emptyCellColor = new Color(0.09f, 0.11f, 0.16f, 1f);
    [SerializeField] private Color pathCellColor = new Color(0.18f, 0.27f, 0.35f, 1f);
    [SerializeField] private Color startCellColor = new Color(0.22f, 0.62f, 0.43f, 1f);
    [SerializeField] private Color contentCellColor = new Color(0.29f, 0.45f, 0.72f, 1f);
    [SerializeField] private Color bossEntranceColor = new Color(0.75f, 0.38f, 0.14f, 1f);
    [SerializeField] private Color bossCellColor = new Color(0.72f, 0.18f, 0.24f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.22f, 0.52f, 0.92f, 1f);
    [SerializeField] private Color textColor = Color.white;

    private RectTransform mapRoot;
    private Button generateButton;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_FontAsset fontAsset;
    private StripDungeonMap currentMap;

    private void Awake()
    {
        BuildInterface();
        generateButton.onClick.AddListener(GenerateMap);
    }

    private void OnDestroy()
    {
        if (generateButton != null)
            generateButton.onClick.RemoveListener(GenerateMap);
    }

    private void GenerateMap()
    {
        if (!StripDungeonMapGenerator.TryGenerate(config, nextSeed, out currentMap, out string error))
        {
            statusText.text = "生成失败：" + error;
            ClearMap();
            return;
        }

        nextSeed = currentMap.seed + 1;
        statusText.text = $"Seed {currentMap.seed} · {currentMap.strips.Count} 条带 · Boss 未揭示。点击任意可走格，模拟进入其所属条带。";
        RefreshMap();
    }

    public void HandleCellClicked(Vector2Int position)
    {
        if (currentMap == null || currentMap.IsBossVisible)
            return;

        currentMap.RevealBossIfOnHostStrip(position);
        if (currentMap.IsBossVisible)
        {
            statusText.text = $"Seed {currentMap.seed} · 已进入 Boss 所属条带，Boss 入口与 Boss 格已揭示。";
            RefreshMap();
        }
    }

    private void BuildInterface()
    {
        fontAsset = Resources.Load<TMP_FontAsset>("Fonts/FZG_CN SDF");
        if (fontAsset == null)
            fontAsset = TMP_Settings.defaultFontAsset;

        Image background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = backgroundColor;

        RectTransform root = transform as RectTransform;
        CreateText("Title", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(-230f, -72f), 30f, TextAlignmentOptions.Left, "第 2 层 · 条带地牢地图生成测试", out titleText);
        CreateText("Status", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(-230f, -124f), 18f, TextAlignmentOptions.Left, "点击“地图生成”创建一张独立条带地图。", out statusText);

        RectTransform buttonTransform = CreateButton("GenerateMapButton", root, "地图生成");
        buttonTransform.anchorMin = new Vector2(1f, 1f);
        buttonTransform.anchorMax = new Vector2(1f, 1f);
        buttonTransform.pivot = new Vector2(1f, 1f);
        buttonTransform.anchoredPosition = new Vector2(-24f, -32f);
        buttonTransform.sizeDelta = generateButtonSize;
        generateButton = buttonTransform.GetComponent<Button>();

        mapRoot = CreateRect("MapRoot", root);
        mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        mapRoot.pivot = new Vector2(0.5f, 0.5f);
        mapRoot.anchoredPosition = new Vector2(0f, -headerHeight * 0.25f);
    }

    private void RefreshMap()
    {
        ClearMap();
        float step = cellSize + cellSpacing;
        mapRoot.sizeDelta = new Vector2(
            currentMap.width * cellSize + (currentMap.width - 1) * cellSpacing,
            currentMap.height * cellSize + (currentMap.height - 1) * cellSpacing);

        for (int y = 0; y < currentMap.height; y++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                StripDungeonCell cell = currentMap.GetCell(position);
                RectTransform cellTransform = CreateButton($"Cell_{x}_{y}", mapRoot, GetCellLabel(cell));
                cellTransform.anchorMin = new Vector2(0.5f, 0.5f);
                cellTransform.anchorMax = new Vector2(0.5f, 0.5f);
                cellTransform.pivot = new Vector2(0.5f, 0.5f);
                cellTransform.sizeDelta = new Vector2(cellSize, cellSize);
                cellTransform.anchoredPosition = new Vector2(
                    (x - (currentMap.width - 1) * 0.5f) * step,
                    (y - (currentMap.height - 1) * 0.5f) * step);

                Button button = cellTransform.GetComponent<Button>();
                Image image = cellTransform.GetComponent<Image>();
                image.color = GetCellColor(cell);
                bool canEnter = cell != null && cell.kind == StripDungeonCellKind.Path;
                button.interactable = canEnter;
                StripDungeonMapCellTestButton cellButton = cellTransform.gameObject.AddComponent<StripDungeonMapCellTestButton>();
                cellButton.Initialize(this, position);
            }
        }
    }

    private void ClearMap()
    {
        if (mapRoot == null)
            return;

        for (int i = mapRoot.childCount - 1; i >= 0; i--)
            Destroy(mapRoot.GetChild(i).gameObject);
    }

    private Color GetCellColor(StripDungeonCell cell)
    {
        if (cell == null || cell.kind == StripDungeonCellKind.Boss && !currentMap.IsBossVisible)
            return emptyCellColor;
        if (cell.kind == StripDungeonCellKind.Boss)
            return bossCellColor;
        if (cell.isStart)
            return startCellColor;
        if (cell.isBossEntrance && currentMap.IsBossVisible)
            return bossEntranceColor;
        return cell.isContent ? contentCellColor : pathCellColor;
    }

    private string GetCellLabel(StripDungeonCell cell)
    {
        if (cell == null || cell.kind == StripDungeonCellKind.Boss && !currentMap.IsBossVisible)
            return string.Empty;
        if (cell.kind == StripDungeonCellKind.Boss)
            return "BOSS";
        if (cell.isStart)
            return "起";
        if (cell.isBossEntrance && currentMap.IsBossVisible)
            return "入口";
        return cell.isContent ? GetLevelTypeLabel(cell.levelType) : string.Empty;
    }

    private static string GetLevelTypeLabel(LevelType levelType)
    {
        switch (levelType)
        {
            case LevelType.Battle: return "战";
            case LevelType.Event: return "事";
            case LevelType.Shop: return "商";
            case LevelType.Rest: return "休";
            case LevelType.Reward: return "奖";
            case LevelType.Elite: return "精";
            case LevelType.RemoveMaterial: return "删";
            case LevelType.AddMaterial: return "加";
            default: return string.Empty;
        }
    }

    private RectTransform CreateButton(string objectName, Transform parent, string label)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = buttonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        CreateText("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20f, TextAlignmentOptions.Center, label, out _);
        return rect;
    }

    private void CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        TextAlignmentOptions alignment,
        string value,
        out TMP_Text text)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = fontAsset;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = textColor;
        tmp.text = value;
        text = tmp;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
}

public class StripDungeonMapCellTestButton : MonoBehaviour
{
    private SecondFloorStripDungeonTestUI owner;
    private Vector2Int position;
    private Button button;

    public void Initialize(SecondFloorStripDungeonTestUI owner, Vector2Int position)
    {
        this.owner = owner;
        this.position = position;
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        owner.HandleCellClicked(position);
    }
}
