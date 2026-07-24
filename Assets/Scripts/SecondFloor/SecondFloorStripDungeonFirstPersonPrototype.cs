using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SecondFloorStripDungeonFirstPersonPrototype : MonoBehaviour
{
    [Header("生成")]
    [SerializeField] private StripDungeonMapConfig config;
    [SerializeField] private int nextSeed = 1;

    [Header("视距")]
    [SerializeField, Min(1)] private int maxForwardSightCells = 2;

    [Header("小地图")]
    [SerializeField, Min(8f)] private float minimapCellSize = 22f;
    [SerializeField, Min(0f)] private float minimapCellSpacing = 2f;
    [SerializeField, Min(0f)] private float minimapMargin = 28f;

    [Header("白模尺寸")]
    [SerializeField, Min(0.5f)] private float cellWorldSize = 3f;
    [SerializeField, Min(0.05f)] private float floorThickness = 0.2f;
    [SerializeField, Min(0.1f)] private float wallThickness = 0.25f;
    [SerializeField, Min(0.5f)] private float wallHeight = 3.2f;
    [SerializeField, Min(0.1f)] private float playerEyeHeight = 1.55f;
    [SerializeField, Range(40f, 100f)] private float fieldOfView = 72f;

    [Header("白模颜色")]
    [SerializeField] private Color floorColor = new Color(0.74f, 0.74f, 0.71f, 1f);
    [SerializeField] private Color wallColor = new Color(0.88f, 0.88f, 0.84f, 1f);
    [SerializeField] private Color bossColor = new Color(0.68f, 0.12f, 0.14f, 1f);
    [SerializeField] private Color skyColor = new Color(0.08f, 0.1f, 0.14f, 1f);

    [Header("小地图颜色")]
    [SerializeField] private Color minimapPanelColor = new Color(0.015f, 0.02f, 0.03f, 0.9f);
    [SerializeField] private Color minimapFogColor = Color.black;
    [SerializeField] private Color minimapPathColor = new Color(0.25f, 0.28f, 0.27f, 1f);
    [SerializeField] private Color minimapBossColor = new Color(0.48f, 0.1f, 0.13f, 1f);
    [SerializeField] private Color minimapVisiblePathColor = new Color(0.72f, 0.75f, 0.71f, 1f);
    [SerializeField] private Color minimapVisibleBossColor = new Color(0.78f, 0.16f, 0.2f, 1f);
    [SerializeField] private Color minimapPlayerColor = new Color(0.25f, 0.78f, 1f, 1f);

    private readonly Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private readonly HashSet<Vector2Int> visiblePositions = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, Image> minimapCells = new Dictionary<Vector2Int, Image>();
    private Transform environmentRoot;
    private Transform playerRoot;
    private Camera playerCamera;
    private Material floorMaterial;
    private Material wallMaterial;
    private Material bossMaterial;
    private StripDungeonMap currentMap;
    private Vector2Int playerPosition;
    private Vector2Int facing;
    private TMP_Text statusText;
    private Button generateButton;
    private TMP_FontAsset fontAsset;
    private RectTransform minimapRoot;

    private void Awake()
    {
        CreateWorldRoot();
        CreatePlayerCamera();
        CreateOverlay();
        GenerateMap();
    }

    private void OnDestroy()
    {
        if (generateButton != null)
            generateButton.onClick.RemoveListener(GenerateMap);
        DestroyRuntimeObject(floorMaterial);
        DestroyRuntimeObject(wallMaterial);
        DestroyRuntimeObject(bossMaterial);
    }

    private void Update()
    {
        if (TryGetClickPosition(out Vector2 pointerPosition))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (pointerPosition.x < Screen.width / 3f)
                TurnLeft();
            else if (pointerPosition.x > Screen.width * 2f / 3f)
                TurnRight();
            else
                TryMoveForward();
        }
    }

    private void GenerateMap()
    {
        if (!StripDungeonMapGenerator.TryGenerate(config, nextSeed, out currentMap, out string error))
        {
            statusText.text = "地图生成失败：" + error;
            return;
        }

        nextSeed = currentMap.seed + 1;
        playerPosition = currentMap.startPosition;
        facing = FindInitialFacing();
        BuildMinimap();
        RefreshView();
        statusText.text = $"Seed {currentMap.seed} · 点击中间前进 · 点击左右转向 · 最大视距 {maxForwardSightCells} 格";
    }

    private void TryMoveForward()
    {
        if (currentMap == null)
            return;

        Vector2Int target = playerPosition + facing;
        if (!IsWalkable(target))
        {
            statusText.text = "前方是墙壁。";
            return;
        }

        playerPosition = target;
        bool wasBossVisible = currentMap.IsBossVisible;
        currentMap.RevealBossIfOnHostStrip(playerPosition);
        RefreshView();
        if (!wasBossVisible && currentMap.IsBossVisible)
        {
            statusText.text = "你进入了 Boss 所属条带，Boss 入口已揭示。";
        }
        else if (playerPosition == currentMap.bossPosition)
        {
            statusText.text = "已抵达 Boss 格。";
        }
        else
        {
            statusText.text = "前进。";
        }

        UpdatePlayerTransform();
    }

    private void TurnLeft()
    {
        facing = new Vector2Int(-facing.y, facing.x);
        RefreshView();
        statusText.text = "左转。";
    }

    private void TurnRight()
    {
        facing = new Vector2Int(facing.y, -facing.x);
        RefreshView();
        statusText.text = "右转。";
    }

    private void CreateWorldRoot()
    {
        environmentRoot = new GameObject("WhiteboxEnvironment").transform;
        environmentRoot.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        floorMaterial = new Material(shader) { color = floorColor };
        wallMaterial = new Material(shader) { color = wallColor };
        bossMaterial = new Material(shader) { color = bossColor };
    }

    private void CreatePlayerCamera()
    {
        playerRoot = new GameObject("DungeonPlayer").transform;
        playerRoot.SetParent(transform, false);
        GameObject cameraObject = new GameObject("DungeonFirstPersonCamera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(playerRoot, false);
        playerCamera = cameraObject.GetComponent<Camera>();
        playerCamera.clearFlags = CameraClearFlags.SolidColor;
        playerCamera.backgroundColor = skyColor;
        playerCamera.fieldOfView = fieldOfView;
        playerCamera.nearClipPlane = 0.03f;
        playerCamera.tag = "MainCamera";

        GameObject lightObject = new GameObject("DungeonDirectionalLight", typeof(Light));
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = Color.white;
    }

    private void CreateOverlay()
    {
        fontAsset = Resources.Load<TMP_FontAsset>("Fonts/FZG_CN SDF");
        if (fontAsset == null)
            fontAsset = TMP_Settings.defaultFontAsset;

        GameObject canvasObject = new GameObject("DungeonPrototypeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasObject.GetComponent<RectTransform>();

        statusText = CreateText("Status", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -100f), new Vector2(-28f, -148f), 24f, TextAlignmentOptions.Left, string.Empty);
        CreateText("Instructions", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -38f), new Vector2(-250f, -82f), 28f, TextAlignmentOptions.Left, "第 2 层 · 第一人称白模测试", out _);
        CreateText("ClickGuide", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-270f, 28f), new Vector2(270f, 72f), 21f, TextAlignmentOptions.Center, "左侧：左转    中间：前进    右侧：右转", out _);

        RectTransform buttonRect = CreateButton("GenerateMapButton", root, "地图生成");
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-28f, -32f);
        buttonRect.sizeDelta = new Vector2(190f, 56f);
        generateButton = buttonRect.GetComponent<Button>();
        generateButton.onClick.AddListener(GenerateMap);

        minimapRoot = CreateRect("Minimap", root);
        minimapRoot.anchorMin = Vector2.zero;
        minimapRoot.anchorMax = Vector2.zero;
        minimapRoot.pivot = Vector2.zero;
        Image minimapBackground = minimapRoot.gameObject.AddComponent<Image>();
        minimapBackground.color = minimapPanelColor;
        minimapBackground.raycastTarget = false;
    }

    private void RefreshView()
    {
        UpdatePlayerTransform();
        RefreshVisiblePositions();
        RebuildEnvironment();
        RefreshMinimap();
    }

    private void RefreshVisiblePositions()
    {
        visiblePositions.Clear();
        visiblePositions.Add(playerPosition);

        Vector2Int left = new Vector2Int(-facing.y, facing.x);
        AddVisibleIfWalkable(playerPosition + left);
        AddVisibleIfWalkable(playerPosition - left);

        for (int distance = 1; distance <= maxForwardSightCells; distance++)
        {
            Vector2Int position = playerPosition + facing * distance;
            if (!IsWalkable(position))
                break;

            visiblePositions.Add(position);
            if (distance == 1)
            {
                AddVisibleIfWalkable(position + left);
                AddVisibleIfWalkable(position - left);
            }
        }
    }

    private void AddVisibleIfWalkable(Vector2Int position)
    {
        if (IsWalkable(position))
            visiblePositions.Add(position);
    }

    private void BuildMinimap()
    {
        ClearChildren(minimapRoot);
        minimapCells.Clear();
        float step = minimapCellSize + minimapCellSpacing;
        minimapRoot.sizeDelta = new Vector2(
            minimapMargin * 2f + currentMap.width * minimapCellSize + (currentMap.width - 1) * minimapCellSpacing,
            minimapMargin * 2f + currentMap.height * minimapCellSize + (currentMap.height - 1) * minimapCellSpacing);
        minimapRoot.anchoredPosition = new Vector2(minimapMargin, minimapMargin);

        for (int y = 0; y < currentMap.height; y++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                RectTransform cellRect = CreateRect($"MinimapCell_{x}_{y}", minimapRoot);
                cellRect.anchorMin = Vector2.zero;
                cellRect.anchorMax = Vector2.zero;
                cellRect.pivot = Vector2.zero;
                cellRect.sizeDelta = new Vector2(minimapCellSize, minimapCellSize);
                cellRect.anchoredPosition = new Vector2(minimapMargin + x * step, minimapMargin + y * step);
                Image cellImage = cellRect.gameObject.AddComponent<Image>();
                cellImage.raycastTarget = false;
                minimapCells[position] = cellImage;
            }
        }
    }

    private void RefreshMinimap()
    {
        foreach (KeyValuePair<Vector2Int, Image> pair in minimapCells)
        {
            Vector2Int position = pair.Key;
            Image image = pair.Value;
            if (position == playerPosition)
            {
                image.color = minimapPlayerColor;
                continue;
            }

            bool isBoss = currentMap.IsBossVisible && position == currentMap.bossPosition;
            if (isBoss)
            {
                image.color = visiblePositions.Contains(position) ? minimapVisibleBossColor : minimapBossColor;
                continue;
            }

            image.color = currentMap.IsPathCell(position)
                ? visiblePositions.Contains(position) ? minimapVisiblePathColor : minimapPathColor
                : minimapFogColor;
        }
    }

    private void RebuildEnvironment()
    {
        ClearChildren(environmentRoot);
        foreach (Vector2Int position in visiblePositions)
        {
            Vector3 center = GetWorldPosition(position);
            CreateBlock("Floor", center + Vector3.down * floorThickness * 0.5f, new Vector3(cellWorldSize, floorThickness, cellWorldSize), floorMaterial);
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector2Int direction = directions[directionIndex];
                if (!visiblePositions.Contains(position + direction))
                    CreateBoundaryWall(center, direction);
            }
        }

        if (currentMap.IsBossVisible && visiblePositions.Contains(currentMap.bossPosition))
        {
            Vector3 bossCenter = GetWorldPosition(currentMap.bossPosition);
            CreateBlock("BossMarker", bossCenter + Vector3.up * (wallHeight * 0.35f), new Vector3(cellWorldSize * 0.46f, wallHeight * 0.7f, cellWorldSize * 0.46f), bossMaterial);
        }
    }

    private void CreateBoundaryWall(Vector3 cellCenter, Vector2Int direction)
    {
        bool horizontalWall = direction.y != 0;
        Vector3 offset = new Vector3(direction.x, 0f, direction.y) * (cellWorldSize - wallThickness) * 0.5f;
        Vector3 size = horizontalWall
            ? new Vector3(cellWorldSize, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, cellWorldSize);
        CreateBlock("Wall", cellCenter + offset + Vector3.up * wallHeight * 0.5f, size, wallMaterial);
    }

    private void CreateBlock(string objectName, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(environmentRoot, false);
        block.transform.position = position;
        block.transform.localScale = size;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private bool IsWalkable(Vector2Int position)
    {
        return currentMap != null && (currentMap.IsPathCell(position) || currentMap.IsBossVisible && position == currentMap.bossPosition);
    }

    private Vector2Int FindInitialFacing()
    {
        for (int i = 0; i < directions.Length; i++)
        {
            if (currentMap.IsPathCell(currentMap.startPosition + directions[i]))
                return directions[i];
        }
        return Vector2Int.up;
    }

    private void UpdatePlayerTransform()
    {
        if (playerRoot == null)
            return;

        playerRoot.position = GetWorldPosition(playerPosition) + Vector3.up * playerEyeHeight;
        playerRoot.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
    }

    private Vector3 GetWorldPosition(Vector2Int mapPosition)
    {
        return new Vector3(
            (mapPosition.x - (currentMap.width - 1) * 0.5f) * cellWorldSize,
            0f,
            (mapPosition.y - (currentMap.height - 1) * 0.5f) * cellWorldSize);
    }

    private static bool TryGetClickPosition(out Vector2 pointerPosition)
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            pointerPosition = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            pointerPosition = Input.mousePosition;
            return true;
        }

        pointerPosition = default;
        return false;
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment, string value)
    {
        return CreateText(objectName, parent, anchorMin, anchorMax, offsetMin, offsetMax, fontSize, alignment, value, out TMP_Text text) ? text : null;
    }

    private bool CreateText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment, string value, out TMP_Text text)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.font = fontAsset;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.text = value;
        tmp.raycastTarget = false;
        text = tmp;
        return true;
    }

    private RectTransform CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.47f, 0.85f, 0.96f);
        CreateText("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center, label, out _);
        return rect;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
