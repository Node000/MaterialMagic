using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEngine.UI;

public class MapPanelUI : MonoBehaviour
{
    [SerializeField] private float mapShowMove = 38f;
    [SerializeField] private float mapShowDuration = 0.28f;
    [SerializeField] private float mapHideDuration = 0.22f;
    [SerializeField] private float markerShakeDuration = 0.34f;
    [SerializeField] private Vector2 markerShakeStrength = new Vector2(12f, 6f);
    [SerializeField] private int markerShakeVibrato = 12;
    [SerializeField] private Ease markerShakeEase = Ease.OutQuad;
    [SerializeField] private float markerMoveDelay = 0.18f;
    [SerializeField] private float markerMoveDuration = 0.55f;
    [SerializeField] private Ease markerMoveEase = Ease.OutQuad;
    [SerializeField] private float autoHideDelay = 0.22f;
    [SerializeField] private float viewportMoveDuration = 0.28f;
    [SerializeField] private Ease viewportMoveEase = Ease.OutQuad;
    [SerializeField] private Vector2 connectionDashSize = new Vector2(18f, 4f);
    [SerializeField] private float connectionDashGap = 10f;
    [SerializeField] private Color connectionDashColor = new Color(1f, 1f, 1f, 0.34f);

    private const int RunNodeCount = 21;
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
            playerMarker.anchoredPosition = GetNodePosition(displayedNodeIndex);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf)
            Hide();
        else
            Show(false, null, false);
    }

    public void Show(bool focusCurrentNode, TweenCallback onComplete, bool animateMarker)
    {
        CacheReferences();
        DOTween.Kill(this, false);
        CreateNodes(animateMarker);
        gameObject.SetActive(true);
        rectTransform.DOKill(false);
        rectTransform.anchoredPosition = shownPosition - new Vector2(0f, mapShowMove);
        Tween showTween = rectTransform.DOAnchorPos(shownPosition, mapShowDuration).SetEase(Ease.OutQuad).SetTarget(this);
        UpdateViewportToPlayer();
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
            content.sizeDelta = new Vector2(Mathf.Max(1920f, 160f + (RunNodeCount - 1) * 180f + 160f), content.sizeDelta.y);
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

        Text markerText = playerMarker.GetComponent<Text>();
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
            SetNodeIcon(node, "LeftIcon", nodeModel.leftLevel.levelType, GetChoiceColor(nodeModel, nodeModel.leftLevel));
            Image leftIcon = UIManager.FindChildComponent<Image>(node, "LeftIcon");
            if (leftIcon != null)
            {
                RectTransform leftRect = leftIcon.GetComponent<RectTransform>();
                leftRect.anchoredPosition = Vector2.zero;
                leftRect.sizeDelta = new Vector2(42f, 42f);
            }

            Image rightIcon = UIManager.FindChildComponent<Image>(node, "RightIcon");
            if (rightIcon != null)
                rightIcon.gameObject.SetActive(false);
            return;
        }

        Image left = UIManager.FindChildComponent<Image>(node, "LeftIcon");
        if (left != null)
        {
            left.gameObject.SetActive(true);
            RectTransform leftRect = left.GetComponent<RectTransform>();
            leftRect.anchoredPosition = new Vector2(-18f, 18f);
            leftRect.sizeDelta = new Vector2(32f, 32f);
        }

        Image right = UIManager.FindChildComponent<Image>(node, "RightIcon");
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
        CreateMapIcon(rect, "LeftIcon", new Vector2(-18f, 18f));
        CreateMapIcon(rect, "RightIcon", new Vector2(18f, -18f));
        return rect;
    }

    private static void CreateMapIcon(RectTransform parent, string name, Vector2 position)
    {
        Image icon = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(parent, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(32f, 32f);
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
        sequence.Append(playerMarker.DOShakeAnchorPos(markerShakeDuration, markerShakeStrength, markerShakeVibrato, 90f, false, true).SetEase(markerShakeEase));
        sequence.AppendInterval(markerMoveDelay);
        sequence.Append(playerMarker.DOAnchorPos(GetNodePosition(owner.CurrentMapNodeIndex), markerMoveDuration).SetEase(markerMoveEase).OnUpdate(UpdateViewportToPlayer));
        sequence.AppendCallback(() => displayedNodeIndex = owner.CurrentMapNodeIndex);
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

    private void UpdateViewportToPlayer()
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
        content.DOAnchorPosX(-targetX, viewportMoveDuration).SetEase(viewportMoveEase).SetTarget(this);
    }
}
