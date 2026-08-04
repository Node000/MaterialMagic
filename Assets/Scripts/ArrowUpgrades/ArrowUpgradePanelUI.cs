using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArrowUpgradePanelUI : MonoBehaviour
{
    [SerializeField] private HandSystemUI handSystem;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(1180f, 720f);
    [SerializeField] private Vector2 pageButtonSize = new Vector2(150f, 44f);
    [SerializeField] private Vector2 nodeButtonSize = new Vector2(330f, 92f);
    [SerializeField] private Vector2 magicButtonSize = new Vector2(204f, 76f);
    [SerializeField] private Vector2 magicButtonSpacing = new Vector2(12f, 12f);
    [SerializeField] private Color panelColor = new Color(0.055f, 0.075f, 0.115f, 0.97f);
    [SerializeField] private Color pageColor = new Color(0.18f, 0.24f, 0.34f, 1f);
    [SerializeField] private Color selectedPageColor = new Color(0.24f, 0.48f, 0.7f, 1f);
    [SerializeField] private Color nodeAvailableColor = new Color(0.18f, 0.34f, 0.29f, 1f);
    [SerializeField] private Color nodeSelectedColor = new Color(0.42f, 0.36f, 0.12f, 1f);
    [SerializeField] private Color nodeUnlockedColor = new Color(0.2f, 0.5f, 0.38f, 1f);
    [SerializeField] private Color nodeLockedColor = new Color(0.13f, 0.15f, 0.19f, 1f);
    [SerializeField] private Color magicSelectedColor = new Color(0.35f, 0.42f, 0.16f, 1f);
    [SerializeField] private Color magicColor = new Color(0.16f, 0.2f, 0.29f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.68f, 0.74f, 0.82f, 1f);

    private readonly List<MagicModel> selectedMagics = new List<MagicModel>();
    private readonly List<Button> pageButtons = new List<Button>();
    private RectTransform panel;
    private RectTransform pageRoot;
    private RectTransform nodeRoot;
    private RectTransform magicContent;
    private TMP_Text titleText;
    private TMP_Text deckText;
    private TMP_Text selectionText;
    private TMP_Text requirementText;
    private TMP_Text hintText;
    private Button confirmButton;
    private Button cancelButton;
    private ArrowUpgradeDirection? currentDirection = ArrowUpgradeDirection.Up;
    private ArrowUpgradeNodeDefinition selectedNode;
    private PlayerState player;

    private void Awake()
    {
        EnsureLayout();
    }

    private void OnEnable()
    {
        EnsureLayout();
        if (player == null)
            player = BattleManager.Instance != null ? BattleManager.Instance.PlayerState : handSystem != null ? handSystem.PlayerState : null;
        RefreshAll();
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= RefreshAll;
    }

    public void Show(PlayerState playerState = null)
    {
        EnsureLayout();
        player = playerState ?? (BattleManager.Instance != null ? BattleManager.Instance.PlayerState : handSystem != null ? handSystem.PlayerState : null);
        ClearSelection();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        ClearSelection();
        gameObject.SetActive(false);
    }

    private void EnsureLayout()
    {
        if (panel != null)
            return;

        panel = transform as RectTransform;
        if (panel == null)
            return;

        if (handSystem == null)
            handSystem = GetComponentInParent<HandSystemUI>(true);
        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = panelSize;
        Image background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = panelColor;

        titleText = CreateText("Title", panel, 28, FontStyles.Bold, TextAlignmentOptions.Left, Color.white, new Vector2(-500f, 318f), new Vector2(760f, 40f));
        Button closeButton = CreateButton("CloseButton", panel, "关闭", new Vector2(510f, 318f), new Vector2(100f, 40f), pageColor);
        closeButton.onClick.AddListener(Hide);

        pageRoot = CreateRect("PageRoot", panel, new Vector2(0f, 260f), new Vector2(1000f, 44f));
        deckText = CreateText("DeckText", panel, 17, FontStyles.Normal, mutedTextColor, new Vector2(0f, 214f), new Vector2(1040f, 34f));
        nodeRoot = CreateRect("NodeRoot", panel, new Vector2(-215f, 62f), new Vector2(740f, 246f));

        RectTransform detailRoot = CreateRect("DetailRoot", panel, new Vector2(385f, 55f), new Vector2(340f, 258f));
        selectionText = CreateText("SelectionText", detailRoot, 22, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, new Vector2(0f, 98f), new Vector2(330f, 56f));
        requirementText = CreateText("RequirementText", detailRoot, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor, new Vector2(0f, 45f), new Vector2(330f, 54f));
        hintText = CreateText("HintText", detailRoot, 15, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor, new Vector2(0f, -18f), new Vector2(330f, 78f));
        confirmButton = CreateButton("ConfirmButton", detailRoot, "确认投入", new Vector2(-84f, -108f), new Vector2(150f, 42f), nodeUnlockedColor);
        cancelButton = CreateButton("CancelButton", detailRoot, "取消选择", new Vector2(84f, -108f), new Vector2(150f, 42f), pageColor);
        confirmButton.onClick.AddListener(ConfirmSelection);
        cancelButton.onClick.AddListener(CancelSelection);

        CreateText("MagicTitle", panel, 20, FontStyles.Bold, TextAlignmentOptions.Left, Color.white, new Vector2(-500f, -114f), new Vector2(420f, 32f)).text = Text("arrow_upgrade.magic.title", "投入道具");
        RectTransform magicViewport = CreateRect("MagicViewport", panel, new Vector2(-215f, -255f), new Vector2(740f, 245f));
        Image viewportImage = magicViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
        Mask mask = magicViewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scrollRect = magicViewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        magicContent = CreateRect("Content", magicViewport, Vector2.zero, new Vector2(720f, 240f));
        magicContent.anchorMin = new Vector2(0.5f, 1f);
        magicContent.anchorMax = new Vector2(0.5f, 1f);
        magicContent.pivot = new Vector2(0.5f, 1f);
        scrollRect.viewport = magicViewport;
        scrollRect.content = magicContent;

        LocalizationSystem.LanguageChanged -= RefreshAll;
        LocalizationSystem.LanguageChanged += RefreshAll;
    }

    private void RefreshAll()
    {
        if (panel == null)
            return;

        titleText.text = Text("arrow_upgrade.panel.title", "箭头强化");
        TMP_Text closeText = panel.Find("CloseButton/Text")?.GetComponent<TMP_Text>();
        if (closeText != null)
            closeText.text = Text("arrow_upgrade.close", "关闭");
        TMP_Text magicTitle = panel.Find("MagicTitle")?.GetComponent<TMP_Text>();
        if (magicTitle != null)
            magicTitle.text = Text("arrow_upgrade.magic.title", "投入道具");
        TMP_Text confirmText = confirmButton != null ? confirmButton.GetComponentInChildren<TMP_Text>() : null;
        if (confirmText != null)
            confirmText.text = Text("arrow_upgrade.confirm", "确认投入");
        TMP_Text cancelText = cancelButton != null ? cancelButton.GetComponentInChildren<TMP_Text>() : null;
        if (cancelText != null)
            cancelText.text = Text("arrow_upgrade.cancel", "取消选择");
        RefreshPageButtons();
        RefreshDeckSummary();
        RefreshNodes();
        RefreshMagicButtons();
        RefreshSelectionDetail();
    }

    private void RefreshPageButtons()
    {
        ClearChildren(pageRoot);
        pageButtons.Clear();
        CreatePageButton(ArrowUpgradeDirection.Up, Text("arrow_upgrade.page.up", "上箭头"), -340f);
        CreatePageButton(ArrowUpgradeDirection.Down, Text("arrow_upgrade.page.down", "下箭头"), -170f);
        CreatePageButton(ArrowUpgradeDirection.Left, Text("arrow_upgrade.page.left", "左箭头"), 0f);
        CreatePageButton(ArrowUpgradeDirection.Right, Text("arrow_upgrade.page.right", "右箭头"), 170f);
        CreatePageButton(null, Text("arrow_upgrade.page.body", "玩家本体"), 340f);
    }

    private void CreatePageButton(ArrowUpgradeDirection? direction, string label, float x)
    {
        bool selected = currentDirection == direction;
        Button button = CreateButton("Page_" + label, pageRoot, label, new Vector2(x, 0f), pageButtonSize, selected ? selectedPageColor : pageColor);
        button.onClick.AddListener(() =>
        {
            currentDirection = direction;
            ClearSelection(false);
            RefreshAll();
        });
        pageButtons.Add(button);
    }

    private void RefreshDeckSummary()
    {
        if (player == null)
        {
            deckText.text = Text("arrow_upgrade.no_player", "当前没有可用玩家状态。");
            return;
        }

        int up = 0;
        int down = 0;
        int left = 0;
        int right = 0;
        int other = 0;
        for (int i = 0; i < player.Deck.Count; i++)
        {
            if (!ArrowUpgradeSystem.TryGetDirection(player.Deck[i].material, out ArrowUpgradeDirection direction))
            {
                other++;
                continue;
            }

            switch (direction)
            {
                case ArrowUpgradeDirection.Up: up++; break;
                case ArrowUpgradeDirection.Down: down++; break;
                case ArrowUpgradeDirection.Left: left++; break;
                case ArrowUpgradeDirection.Right: right++; break;
            }
        }

        deckText.text = string.Format(Text("arrow_upgrade.deck_summary", "牌组：上 {0}  下 {1}  左 {2}  右 {3}  其他 {4}  总计 {5}"), up, down, left, right, other, player.Deck.Count);
    }

    private void RefreshNodes()
    {
        ClearChildren(nodeRoot);
        if (player == null)
            return;

        if (currentDirection.HasValue)
        {
            ArrowUpgradeDirection direction = currentDirection.Value;
            string prefix = direction.ToString().ToLowerInvariant();
            CreateNodeButton(ArrowUpgradeSystem.GetNode(prefix + "_root"), new Vector2(-190f, 0f));
            CreateNodeButton(ArrowUpgradeSystem.GetNode(prefix + "_up"), new Vector2(180f, 92f));
            CreateNodeButton(ArrowUpgradeSystem.GetNode(prefix + "_down"), new Vector2(180f, 30f));
            CreateNodeButton(ArrowUpgradeSystem.GetNode(prefix + "_left"), new Vector2(180f, -32f));
            CreateNodeButton(ArrowUpgradeSystem.GetNode(prefix + "_right"), new Vector2(180f, -94f));
            return;
        }

        CreateNodeButton(ArrowUpgradeSystem.GetNode("body_draw_1"), new Vector2(-250f, 0f));
        CreateNodeButton(ArrowUpgradeSystem.GetNode("body_refresh"), new Vector2(0f, 0f));
        CreateNodeButton(ArrowUpgradeSystem.GetNode("body_draw_2"), new Vector2(250f, 0f));
    }

    private void CreateNodeButton(ArrowUpgradeNodeDefinition node, Vector2 position)
    {
        if (node == null)
            return;

        bool unlocked = player.ArrowUpgrades.IsUnlocked(node.Id);
        bool available = ArrowUpgradeSystem.IsNodeAvailable(player, node);
        bool selected = selectedNode == node;
        Color color = unlocked ? nodeUnlockedColor : selected ? nodeSelectedColor : available ? nodeAvailableColor : nodeLockedColor;
        string requirement = FormatRequirement(node);
        string state = unlocked ? Text("arrow_upgrade.state.unlocked", "已激活") : available ? Text("arrow_upgrade.state.available", "可激活") : Text("arrow_upgrade.state.locked", "未解锁");
        string label = state + "\n" + ArrowUpgradeSystem.GetNodeDescription(node) + "\n" + requirement;
        Button button = CreateButton(node.Id, nodeRoot, label, position, nodeButtonSize, color);
        button.interactable = unlocked || available;
        button.onClick.AddListener(() => SelectNode(node));
    }

    private void SelectNode(ArrowUpgradeNodeDefinition node)
    {
        if (player == null || node == null || player.ArrowUpgrades.IsUnlocked(node.Id) || !ArrowUpgradeSystem.IsNodeAvailable(player, node))
            return;

        if (selectedNode != node)
            selectedMagics.Clear();
        selectedNode = node;
        RefreshAll();
    }

    private void RefreshMagicButtons()
    {
        ClearChildren(magicContent);
        if (selectedNode == null || player == null)
        {
            SetContentHeight(1);
            return;
        }

        List<MagicModel> candidates = new List<MagicModel>();
        for (int i = 0; i < player.MagicBook.Count; i++)
        {
            MagicModel magic = player.MagicBook[i];
            if (CanContribute(magic, selectedNode))
                candidates.Add(magic);
        }

        if (candidates.Count == 0)
        {
            CreateText("Empty", magicContent, 18, FontStyles.Normal, TextAlignmentOptions.Center, mutedTextColor, Vector2.zero, new Vector2(680f, 50f)).text = Text("arrow_upgrade.no_matching_magic", "没有能满足当前需求的道具。");
            SetContentHeight(1);
            return;
        }

        const int columns = 3;
        for (int i = 0; i < candidates.Count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float width = magicButtonSize.x;
            float height = magicButtonSize.y;
            float x = (column - 1) * (width + magicButtonSpacing.x);
            float y = -row * (height + magicButtonSpacing.y) - height * 0.5f;
            MagicModel magic = candidates[i];
            bool isSelected = selectedMagics.Contains(magic);
            string label = magic.Name + "\n" + FormatMagicRecipe(magic) + "  " + Text("arrow_upgrade.magic.slot", "槽") + " " + (magic.SlotIndex + 1);
            Button button = CreateButton("Magic_" + magic.SlotIndex, magicContent, label, new Vector2(x, y), magicButtonSize, isSelected ? magicSelectedColor : magicColor);
            button.onClick.AddListener(() => ToggleMagic(magic));
        }
        SetContentHeight((candidates.Count + columns - 1) / columns);
    }

    private void ToggleMagic(MagicModel magic)
    {
        if (magic == null)
            return;

        if (selectedMagics.Contains(magic))
            selectedMagics.Remove(magic);
        else
            selectedMagics.Add(magic);
        RefreshAll();
    }

    private void RefreshSelectionDetail()
    {
        if (selectedNode == null)
        {
            selectionText.text = Text("arrow_upgrade.select_node", "选择一个可激活节点");
            requirementText.text = Text("arrow_upgrade.select_node_hint", "切换节点会清空已选道具。免费根节点不需要投入。" );
            hintText.text = string.Empty;
            confirmButton.interactable = false;
            cancelButton.interactable = false;
            return;
        }

        selectionText.text = ArrowUpgradeSystem.GetNodeDescription(selectedNode);
        requirementText.text = FormatRequirement(selectedNode);
        bool meetsRequirement = ArrowUpgradeSystem.MeetsRequirement(selectedNode, selectedMagics);
        bool canConsume = selectedNode.Requirement.Length == 0 || player.CanConsumeArrowUpgradeMagics(selectedMagics);
        hintText.text = meetsRequirement && canConsume
            ? Text("arrow_upgrade.requirement_met", "需求已满足，确认后会消耗所选道具。")
            : Text("arrow_upgrade.requirement_unmet", "选择道具，使其配方合计覆盖以上方向；多余方向不会浪费。") + "\n" + Text("arrow_upgrade.selected_count", "已选 {0} 件").Replace("{0}", selectedMagics.Count.ToString());
        confirmButton.interactable = meetsRequirement && canConsume && ArrowUpgradeSystem.IsNodeAvailable(player, selectedNode);
        cancelButton.interactable = true;
    }

    private void ConfirmSelection()
    {
        if (player == null || selectedNode == null)
            return;

        if (!ArrowUpgradeSystem.TryUnlock(player, selectedNode.Id, selectedMagics))
        {
            RefreshAll();
            return;
        }

        selectedMagics.Clear();
        selectedNode = null;
        handSystem?.RefreshArrowUpgradeVisuals();
        RefreshAll();
    }

    private void CancelSelection()
    {
        ClearSelection(true);
    }

    private void ClearSelection(bool refresh = true)
    {
        selectedNode = null;
        selectedMagics.Clear();
        if (refresh && panel != null && gameObject.activeInHierarchy)
            RefreshAll();
    }

    private static bool CanContribute(MagicModel magic, ArrowUpgradeNodeDefinition node)
    {
        if (magic == null || node == null || node.Requirement.Length == 0)
            return false;

        MaterialEnum[] recipe = magic.Data != null ? magic.Data.recipe : null;
        for (int i = 0; recipe != null && i < recipe.Length; i++)
        {
            if (!ArrowUpgradeSystem.TryGetDirection(recipe[i], out ArrowUpgradeDirection direction))
                continue;
            for (int requirementIndex = 0; requirementIndex < node.Requirement.Length; requirementIndex++)
            {
                if (direction == node.Requirement[requirementIndex])
                    return true;
            }
        }
        return false;
    }

    private static string FormatMagicRecipe(MagicModel magic)
    {
        MaterialEnum[] recipe = magic != null && magic.Data != null ? magic.Data.recipe : null;
        if (recipe == null || recipe.Length == 0)
            return "-";

        List<string> directions = new List<string>();
        for (int i = 0; i < recipe.Length; i++)
        {
            if (ArrowUpgradeSystem.TryGetDirection(recipe[i], out ArrowUpgradeDirection direction))
                directions.Add(ArrowUpgradeSystem.GetDirectionText(direction));
        }
        return directions.Count > 0 ? string.Join("+", directions) : "-";
    }

    private static string FormatRequirement(ArrowUpgradeNodeDefinition node)
    {
        if (node == null || node.Requirement.Length == 0)
            return Text("arrow_upgrade.requirement.free", "需求：免费激活");

        List<string> directions = new List<string>(node.Requirement.Length);
        for (int i = 0; i < node.Requirement.Length; i++)
            directions.Add(ArrowUpgradeSystem.GetDirectionText(node.Requirement[i]));
        return Text("arrow_upgrade.requirement.prefix", "需求：") + string.Join(" + ", directions);
    }

    private void SetContentHeight(int rows)
    {
        float height = Mathf.Max(240f, rows * (magicButtonSize.y + magicButtonSpacing.y) + magicButtonSpacing.y);
        magicContent.sizeDelta = new Vector2(magicContent.sizeDelta.x, height);
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        Button button = gameObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.disabledColor = new Color(0.36f, 0.36f, 0.4f, 0.7f);
        button.colors = colors;
        CreateText("Text", rect, 16, FontStyles.Normal, TextAlignmentOptions.Center, Color.white, Vector2.zero, size).text = label;
        return button;
    }

    private TMP_Text CreateText(string name, Transform parent, int fontSize, FontStyles style, Color color, Vector2 position, Vector2 size)
    {
        return CreateText(name, parent, fontSize, style, TextAlignmentOptions.Center, color, position, size);
    }

    private TMP_Text CreateText(string name, Transform parent, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private static string Text(string key, string fallback)
    {
        return LocalizationSystem.GetText(key, fallback);
    }
}
