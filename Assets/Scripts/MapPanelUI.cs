using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapPanelUI : MonoBehaviour
{
    [SerializeField] private float mapShowMove = 38f;
    [SerializeField] private float mapShowDuration = 0.28f;
    [SerializeField] private float mapHideDuration = 0.22f;
    [SerializeField] private float markerMoveDelay = 0.18f;
    [SerializeField] private float markerMoveDuration = 0.55f;
    [SerializeField] private Ease markerMoveEase = Ease.OutQuad;
    [SerializeField] private float autoHideDelay = 0.22f;
    [SerializeField] private float viewportMoveDuration = 0.28f;
    [SerializeField] private Ease viewportMoveEase = Ease.OutQuad;
    [SerializeField] private Vector2 connectionDashSize = new Vector2(18f, 4f);
    [SerializeField] private float connectionDashGap = 10f;
    [SerializeField] private Color connectionDashColor = new Color(1f, 1f, 1f, 0.34f);

    private const int RunNodeCount = 10;
    private readonly List<RectTransform> nodeViews = new List<RectTransform>();
    private HandSystemUI owner;
    private RectTransform rectTransform;
    private RectTransform content;
    private RectTransform playerMarker;
    private ScrollRect scrollRect;
    private Vector2 shownPosition;
    private int displayedNodeIndex;

    public float HideDuration => mapHideDuration;

    public float MapShowMove
    {
        get => mapShowMove;
        set => mapShowMove = value;
    }

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        rectTransform = (RectTransform)transform;
        shownPosition = rectTransform.anchoredPosition;
        displayedNodeIndex = owner != null ? owner.CurrentMapNodeIndex : 0;
        CacheReferences();
        CreateNodes(false);
        if (!gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void SetPlayerMarkerNodeIndex(int nodeIndex)
    {
        displayedNodeIndex = nodeIndex;
        CacheReferences();
        playerMarker = UIManager.FindChildRect(content, "PlayerMarker");
        if (playerMarker != null)
        {
            playerMarker.DOKill(false);
            playerMarker.anchoredPosition = GetNodePosition(displayedNodeIndex);
        }
    }

    public void SyncPlayerMarkerToCurrentNode()
    {
        SetPlayerMarkerNodeIndex(owner != null ? owner.CurrentMapNodeIndex : displayedNodeIndex);
        UpdateViewportToPlayer(true);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf)
            Hide();
        else
            Show(false, null, false, true);
    }

    public void Show(bool focusCurrentNode, TweenCallback onComplete, bool animateMarker)
    {
        Show(focusCurrentNode, onComplete, animateMarker, false);
    }

    private void Show(bool focusCurrentNode, TweenCallback onComplete, bool animateMarker, bool syncMarkerToCurrentNode)
    {
        CacheReferences();
        DOTween.Kill(this, false);
        if (syncMarkerToCurrentNode)
            SetPlayerMarkerNodeIndex(owner != null ? owner.CurrentMapNodeIndex : displayedNodeIndex);
        CreateNodes(animateMarker);
        if (animateMarker && playerMarker != null)
            playerMarker.anchoredPosition = GetNodePosition(displayedNodeIndex);

        gameObject.SetActive(true);
        rectTransform.DOKill(false);
        rectTransform.anchoredPosition = shownPosition - new Vector2(0f, mapShowMove);
        Tween showTween = rectTransform.DOAnchorPos(shownPosition, mapShowDuration).SetEase(Ease.OutQuad).SetTarget(this);
        UpdateViewportToPlayer(false);
        if (!focusCurrentNode)
        {
            onComplete?.Invoke();
            return;
        }

        if (animateMarker)
            showTween.OnComplete(() => MoveMarkerToCurrentNode(onComplete));
        else
            showTween.OnComplete(() => onComplete?.Invoke());
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        rectTransform.DOKill(false);
        DOTween.Kill(this, false);
        rectTransform.DOAnchorPos(shownPosition - new Vector2(0f, mapShowMove), mapHideDuration)
            .SetEase(Ease.InQuad)
            .SetTarget(this)
            .OnComplete(() => gameObject.SetActive(false));
    }

    public void RefreshNodeVisual(int index)
    {
        if (owner == null || index < 0 || index >= owner.MapNodes.Count || index >= nodeViews.Count)
            return;

        ApplyNodeVisual(nodeViews[index], owner.MapNodes[index]);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        scrollRect = GetComponent<ScrollRect>();
        RectTransform viewport = null;
        Transform viewportTransform = transform.Find("Viewport");
        if (viewportTransform != null)
        {
            viewport = viewportTransform as RectTransform;
            Transform contentTransform = viewportTransform.Find("Content");
            content = contentTransform as RectTransform;
        }

        if (scrollRect != null)
        {
            scrollRect.viewport = viewport;
            scrollRect.content = content;
        }

        if (content != null)
        {
            int nodeCount = owner != null && owner.MapNodes != null && owner.MapNodes.Count > 0 ? owner.MapNodes.Count : RunNodeCount;
            content.sizeDelta = new Vector2(Mathf.Max(1920f, 160f + (nodeCount - 1) * 180f + 160f), content.sizeDelta.y);
        }
    }

    private void CreateNodes(bool preserveMarkerPosition)
    {
        nodeViews.Clear();
        if (owner == null || content == null)
            return;

        IReadOnlyList<RunMapNodeModel> nodes = owner.MapNodes;
        CreateConnections(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            RectTransform node = UIManager.FindChildRect(content, "MapNode" + (i + 1));
            if (node == null)
                node = CreateRuntimeNode(i);

            node.anchoredPosition = GetNodePosition(i);
            node.gameObject.SetActive(true);
            ApplyNodeVisual(node, nodes[i]);
            nodeViews.Add(node);
        }

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (child.name.StartsWith("MapNode") && !IsActiveMapNode(child.name, nodes.Count))
                child.gameObject.SetActive(false);
            if (child.name.StartsWith("MapConnection") && !IsActiveConnection(child.name, nodes.Count - 1))
                child.gameObject.SetActive(false);
        }

        playerMarker = UIManager.FindChildRect(content, "PlayerMarker");
        if (playerMarker == null)
            return;

        bool shouldPreserveMarkerPosition = preserveMarkerPosition;

        TMP_Text markerText = playerMarker.GetComponent<TMP_Text>();
        if (markerText != null)
            markerText.enabled = false;

        Image markerImage = playerMarker.GetComponent<Image>();
        if (markerImage != null)
        {
            markerImage.color = new Color(1f, 0.86f, 0.18f, 0.95f);
            markerImage.raycastTarget = false;
        }

        if (!shouldPreserveMarkerPosition)
        {
            displayedNodeIndex = owner.CurrentMapNodeIndex;
            playerMarker.anchoredPosition = GetNodePosition(displayedNodeIndex);
        }
        playerMarker.gameObject.SetActive(true);
        playerMarker.sizeDelta = new Vector2(46f, 46f);
        if (playerMarker.GetComponent<JuicyMotion>() == null)
            playerMarker.gameObject.AddComponent<JuicyMotion>();
        playerMarker.SetAsLastSibling();
    }

    private void CreateConnections(int nodeCount)
    {
        for (int i = 0; i < nodeCount - 1; i++)
        {
            RectTransform connection = UIManager.FindChildRect(content, "MapConnection" + (i + 1));
            if (connection == null)
                connection = CreateRuntimeConnection(i);

            connection.gameObject.SetActive(true);
            connection.SetAsFirstSibling();
            ConfigureConnection(connection, i);
        }
    }

    private RectTransform CreateRuntimeConnection(int index)
    {
        RectTransform connection = new GameObject("MapConnection" + (index + 1), typeof(RectTransform)).GetComponent<RectTransform>();
        connection.SetParent(content, false);
        return connection;
    }

    private void ConfigureConnection(RectTransform connection, int index)
    {
        Vector2 start = GetNodePosition(index);
        Vector2 end = GetNodePosition(index + 1);
        float distance = end.x - start.x - 92f;
        int dashCount = Mathf.Max(1, Mathf.FloorToInt((distance + connectionDashGap) / (connectionDashSize.x + connectionDashGap)));
        float totalDashWidth = dashCount * connectionDashSize.x + (dashCount - 1) * connectionDashGap;
        float firstX = (distance - totalDashWidth) * 0.5f;

        connection.anchorMin = new Vector2(0f, 0.5f);
        connection.anchorMax = new Vector2(0f, 0.5f);
        connection.pivot = new Vector2(0f, 0.5f);
        connection.anchoredPosition = new Vector2(start.x + 46f, 0f);
        connection.sizeDelta = new Vector2(distance, connectionDashSize.y);

        for (int i = 0; i < dashCount; i++)
        {
            Image dash = GetOrCreateDash(connection, i);
            dash.color = connectionDashColor;
            dash.raycastTarget = false;
            RectTransform dashRect = dash.rectTransform;
            dashRect.anchorMin = new Vector2(0f, 0.5f);
            dashRect.anchorMax = new Vector2(0f, 0.5f);
            dashRect.pivot = new Vector2(0f, 0.5f);
            dashRect.anchoredPosition = new Vector2(firstX + i * (connectionDashSize.x + connectionDashGap), 0f);
            dashRect.sizeDelta = connectionDashSize;
            dash.gameObject.SetActive(true);
        }

        for (int i = dashCount; i < connection.childCount; i++)
            connection.GetChild(i).gameObject.SetActive(false);
    }

    private Image GetOrCreateDash(RectTransform connection, int index)
    {
        while (connection.childCount <= index)
        {
            Image dash = new GameObject("Dash" + (connection.childCount + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            dash.transform.SetParent(connection, false);
        }
        return connection.GetChild(index).GetComponent<Image>();
    }

    private void ApplyNodeVisual(RectTransform node, RunMapNodeModel nodeModel)
    {
        if (nodeModel.fixedSingleChoice)
        {
            SetSlashVisible(node, false);
            SetIconVisible(node, "LeftIcon", false);
            SetIconVisible(node, "RightIcon", false);
            Image centerIcon = GetOrCreateMapIcon(node, "CenterIcon", Vector2.zero);
            if (centerIcon != null)
            {
                RectTransform centerRect = centerIcon.GetComponent<RectTransform>();
                centerRect.anchoredPosition = Vector2.zero;
                centerRect.sizeDelta = new Vector2(42f, 42f);
            }
            SetNodeIcon(node, "CenterIcon", nodeModel.leftLevel.levelType, GetChoiceColor(nodeModel, nodeModel.leftLevel));
            return;
        }

        SetSlashVisible(node, true);
        SetIconVisible(node, "CenterIcon", false);
        Image left = GetOrCreateMapIcon(node, "LeftIcon", new Vector2(-18f, 18f));
        if (left != null)
        {
            left.gameObject.SetActive(true);
            RectTransform leftRect = left.GetComponent<RectTransform>();
            leftRect.anchoredPosition = new Vector2(-18f, 18f);
            leftRect.sizeDelta = new Vector2(32f, 32f);
        }

        Image right = GetOrCreateMapIcon(node, "RightIcon", new Vector2(18f, -18f));
        if (right != null)
        {
            right.gameObject.SetActive(true);
            RectTransform rightRect = right.GetComponent<RectTransform>();
            rightRect.anchoredPosition = new Vector2(18f, -18f);
            rightRect.sizeDelta = new Vector2(32f, 32f);
        }

        SetNodeIcon(node, "LeftIcon", nodeModel.leftLevel.levelType, GetChoiceColor(nodeModel, nodeModel.leftLevel));
        SetNodeIcon(node, "RightIcon", nodeModel.rightLevel.levelType, GetChoiceColor(nodeModel, nodeModel.rightLevel));
    }

    private RectTransform CreateRuntimeNode(int index)
    {
        Image image = new GameObject("MapNode" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(content, false);
        image.color = new Color(0.16f, 0.16f, 0.22f, 1f);
        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(92f, 92f);
        CreateSlash(rect);
        CreateMapIcon(rect, "LeftIcon", new Vector2(-18f, 18f));
        CreateMapIcon(rect, "RightIcon", new Vector2(18f, -18f));
        CreateMapIcon(rect, "CenterIcon", Vector2.zero).gameObject.SetActive(false);
        return rect;
    }

    private static void CreateSlash(RectTransform parent)
    {
        Image slash = new GameObject("Slash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        slash.transform.SetParent(parent, false);
        slash.color = new Color(0.85f, 0.85f, 0.9f, 0.78f);
        slash.raycastTarget = false;
        RectTransform rect = slash.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(4f, 118f);
        rect.localEulerAngles = new Vector3(0f, 0f, -45f);
    }

    private static void SetSlashVisible(RectTransform node, bool visible)
    {
        Transform slash = node != null ? node.Find("Slash") : null;
        if (slash == null)
        {
            if (!visible || node == null)
                return;
            CreateSlash(node);
            slash = node.Find("Slash");
        }

        RectTransform slashRect = slash as RectTransform;
        if (slashRect != null)
        {
            slashRect.SetAsFirstSibling();
            slashRect.anchorMin = new Vector2(0.5f, 0.5f);
            slashRect.anchorMax = new Vector2(0.5f, 0.5f);
            slashRect.pivot = new Vector2(0.5f, 0.5f);
            slashRect.anchoredPosition = Vector2.zero;
            slashRect.sizeDelta = new Vector2(4f, 118f);
            slashRect.localEulerAngles = new Vector3(0f, 0f, -45f);
        }
        Image slashImage = slash.GetComponent<Image>();
        if (slashImage != null)
        {
            slashImage.color = new Color(0.85f, 0.85f, 0.9f, 0.78f);
            slashImage.raycastTarget = false;
        }
        slash.gameObject.SetActive(visible);
    }

    private static Image CreateMapIcon(RectTransform parent, string name, Vector2 position)
    {
        Image icon = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(parent, false);
        ConfigureMapIcon(icon, position);
        return icon;
    }

    private static Image GetOrCreateMapIcon(RectTransform parent, string name, Vector2 position)
    {
        Image icon = UIManager.FindChildComponent<Image>(parent, name);
        if (icon == null && parent != null)
            icon = CreateMapIcon(parent, name, position);
        else if (icon != null)
            ConfigureMapIcon(icon, position);
        return icon;
    }

    private static void ConfigureMapIcon(Image icon, Vector2 position)
    {
        if (icon == null)
            return;

        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(32f, 32f);
    }

    private static void SetIconVisible(RectTransform node, string iconName, bool visible)
    {
        Image icon = UIManager.FindChildComponent<Image>(node, iconName);
        if (icon != null)
            icon.gameObject.SetActive(visible);
    }

    private Color GetChoiceColor(RunMapNodeModel nodeModel, LevelData level)
    {
        if (nodeModel.selectedLevel == null)
            return Color.white;

        return nodeModel.selectedLevel == level ? Color.white : new Color(0.58f, 0.58f, 0.58f, 0.55f);
    }

    private void SetNodeIcon(RectTransform node, string iconName, LevelType type, Color color)
    {
        Image icon = UIManager.FindChildComponent<Image>(node, iconName);
        if (icon == null)
            return;

        icon.gameObject.SetActive(true);
        icon.sprite = UIManager.LoadLevelTypeSprite(type);
        icon.color = icon.sprite != null ? color : new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.75f, color.a * 0.25f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private Vector2 GetNodePosition(int index)
    {
        int nodeCount = owner != null ? owner.MapNodes.Count : RunNodeCount;
        return new Vector2(80f + Mathf.Clamp(index, 0, Mathf.Max(0, nodeCount - 1)) * 180f, 0f);
    }

    private void MoveMarkerToCurrentNode(TweenCallback onComplete)
    {
        if (playerMarker == null)
        {
            onComplete?.Invoke();
            return;
        }

        playerMarker.DOKill(false);
        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(markerMoveDelay);
        sequence.Append(playerMarker.DOAnchorPos(GetNodePosition(owner.CurrentMapNodeIndex), markerMoveDuration).SetEase(markerMoveEase).OnUpdate(() => UpdateViewportToPlayer(false)));
        sequence.AppendCallback(() =>
        {
            displayedNodeIndex = owner.CurrentMapNodeIndex;
            playerMarker.anchoredPosition = GetNodePosition(displayedNodeIndex);
        });
        sequence.AppendInterval(autoHideDelay);
        sequence.SetTarget(this).OnComplete(onComplete);
    }

    private static bool IsActiveMapNode(string nodeName, int nodeCount)
    {
        if (!nodeName.StartsWith("MapNode"))
            return false;

        if (!int.TryParse(nodeName.Substring(7), out int nodeNumber))
            return false;

        return nodeNumber >= 1 && nodeNumber <= nodeCount;
    }

    private static bool IsActiveConnection(string connectionName, int connectionCount)
    {
        if (!connectionName.StartsWith("MapConnection"))
            return false;

        if (!int.TryParse(connectionName.Substring(13), out int connectionNumber))
            return false;

        return connectionNumber >= 1 && connectionNumber <= connectionCount;
    }

    private void UpdateViewportToPlayer(bool instant)
    {
        if (scrollRect == null || content == null || playerMarker == null || scrollRect.viewport == null)
            return;

        float scrollableWidth = Mathf.Max(0f, content.rect.width - scrollRect.viewport.rect.width);
        if (scrollableWidth <= 0f)
        {
            content.anchoredPosition = Vector2.zero;
            return;
        }

        float markerX = playerMarker.anchoredPosition.x;
        float targetX = Mathf.Clamp(markerX - scrollRect.viewport.rect.width * 0.5f, 0f, scrollableWidth);
        if (instant)
        {
            content.DOKill(false);
            content.anchoredPosition = new Vector2(-targetX, content.anchoredPosition.y);
            return;
        }

        content.DOAnchorPosX(-targetX, viewportMoveDuration).SetEase(viewportMoveEase).SetTarget(this);
    }
}
