using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具瀑布：一个矩形区域里不断有道具从区域正上方落下（2D 物理），落到底部（区域下边缘即地面）后堆积起来；
/// 堆到数量上限或落地存活时间到期时，最早的/到期的道具直接回收（碎片走对象池复用，不反复创建销毁）；
/// 落地停稳的道具会逐渐变成纯白（需要道具碎片使用 Style/Sprite/FadeToWhite）。
/// 区域矩形的具体位置完全由 <see cref="area"/>（RectTransform）在场景里手摆，脚本不会改它；墙面与地面碰撞体按矩形尺寸自动贴合。
/// </summary>
[DisallowMultipleComponent]
public class ItemWaterfallEffect : MonoBehaviour
{
    [Header("区域（矩形）")]
    [SerializeField] private RectTransform area;
    [SerializeField] private Transform piecesRoot;
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private BoxCollider2D leftWallCollider;
    [SerializeField] private BoxCollider2D rightWallCollider;
    [Tooltip("勾选时墙面/地面碰撞体按矩形自动贴合；取消后碰撞体完全由手工摆放决定。")]
    [SerializeField] private bool autoFitBounds = true;

    [Header("投放")]
    [SerializeField] private ItemFallingPiece piecePrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.18f, 0.42f);
    [Tooltip("出生位置：0 = 矩形最左，1 = 矩形最右，两个值构成随机区间（均匀分布）。默认只在中间 10% 落，想薄满整条改成 0~1。")]
    [SerializeField] private Vector2 spawnXRange = new Vector2(0.45f, 0.55f);
    [SerializeField, Min(0f)] private float spawnHeightOffset = 80f;
    [Tooltip("道具碎片相对图标原始像素尺寸的比例：1 表示图标按原始像素大小绘制（区域 1 单位 = 1 像素）。")]
    [SerializeField, Range(0.05f, 2f)] private float pieceScale = 0.5f;
    [SerializeField] private Vector2 pieceScaleRange = new Vector2(0.85f, 1.1f);
    [SerializeField, Min(1)] private int maxPieceCount = 40;
    [Tooltip("落地（第一次碰到地面/其它道具）后存活多少秒后自动消失；0 = 关闭，只按数量上限回收。")]
    [SerializeField, Min(0f)] private float landedLifetime;
    [SerializeField, Min(0.01f)] private float gravityScale = 1.05f;
    [SerializeField, Min(0f)] private float spawnSpin = 120f;

    [Header("碰撞与停稳（统一下发给碎片）")]
    [Tooltip("碰撞箱相对图标非透明轮廓包围盒的比例：1 = 刚好贴合轮廓，越小堆得越紧（可能互相插入），越大越松。")]
    [SerializeField, Range(0.2f, 1.2f)] private float colliderFit = 0.92f;
    [Tooltip("勾选时用 Sprite 导入期生成的非透明轮廓包围盒做碰撞箱（比整张贴图框贴合很多）；未生成轮廓的图标自动回退到贴图框。")]
    [SerializeField] private bool useOutlineColliderBounds = true;
    [Tooltip("出生点还有道具没让开时，本次不生成、下次再试（避免新道具生成在上一个道具/堆积体内部被弹飞）。矩形够大时不会触发。")]
    [SerializeField] private bool skipSpawnWhenBlocked = true;
    [Tooltip("停稳判定线速度阈值（世界单位/秒）。")]
    [SerializeField, Min(0f)] private float restSpeed = 0.08f;
    [Tooltip("停稳判定角速度阈值（度/秒）。")]
    [SerializeField, Min(0f)] private float restSpin = 40f;
    [Tooltip("低于阈值持续多久算停稳，之后开始黑白渐变。")]
    [SerializeField, Min(0f)] private float restDuration = 0.2f;

    [Header("落地表现")]
    [Tooltip("落地停稳后延迟多少秒开始变白。")]
    [SerializeField, Min(0f)] private float whiteDelay = 0.3f;
    [Tooltip("变白（渐变到纯白）的时长，秒。")]
    [SerializeField, Min(0.01f)] private float whiteDuration = 1.8f;
    [Tooltip("变白的目标颜色，默认纯白；想偏冷/偏暖白可改。")]
    [SerializeField] private Color whiteColor = Color.white;

    [Header("碰撞体尺寸（区域单位，1 单位 = 区域 1 像素）")]
    [SerializeField, Min(1f)] private float wallThickness = 40f;
    [SerializeField, Min(1f)] private float floorThickness = 40f;
    [Tooltip("地面/墙面在矩形外的额外延伸，保证高速下落时道具不会从缝隙漏出。")]
    [SerializeField, Min(0f)] private float boundsPadding = 100f;

    private readonly List<ItemFallingPiece> pieces = new List<ItemFallingPiece>();
    private readonly Stack<ItemFallingPiece> piecePool = new Stack<ItemFallingPiece>();
    private static Sprite[] itemSpriteCache;
    private float spawnTimer;
    private bool spawning;
    private bool missingPrefabLogged;

    public int PieceCount => pieces.Count;
    public bool IsSpawning => spawning;

    private void Awake()
    {
        ResolveReferences();
        FitBounds();
        spawnTimer = GetSpawnInterval();
        spawning = spawnOnStart;
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        // 所有碎片由控制器统一驱动：碎片自身不再跑 Update。
        TickPieces(delta);

        if (!spawning)
            return;

        spawnTimer -= delta;
        if (spawnTimer > 0f)
            return;

        spawnTimer = GetSpawnInterval();
        SpawnPiece();
    }

    /// <summary>推进所有存活碎片，并回收本帧请求回收的碎片。</summary>
    private void TickPieces(float delta)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            ItemFallingPiece piece = pieces[i];
            if (piece == null)
            {
                pieces.RemoveAt(i);
                continue;
            }

            piece.Tick(delta);
            if (piece.IsRemoveRequested)
                RecyclePiece(piece);
        }
    }

    public void SetSpawning(bool value)
    {
        spawning = value;
        if (spawning)
            spawnTimer = GetSpawnInterval();
    }

    /// <summary>区域矩形变化后重新贴合碰撞体（改完 RectTransform 尺寸调用）。</summary>
    public void RefreshBounds()
    {
        ResolveReferences();
        FitBounds();
    }

    private void SpawnPiece()
    {
        if (piecePrefab == null)
        {
            if (!missingPrefabLogged)
            {
                missingPrefabLogged = true;
                Debug.LogWarning("[ItemWaterfallEffect] 未绑定道具碎片 Prefab，瀑布不会掉落道具。", this);
            }
            return;
        }

        if (area == null)
            return;

        Sprite sprite = PickItemSprite();
        if (sprite == null)
            return;

        Rect rect = area.rect;
        float ratioMin = Mathf.Clamp01(Mathf.Min(spawnXRange.x, spawnXRange.y));
        float ratioMax = Mathf.Clamp01(Mathf.Max(spawnXRange.x, spawnXRange.y));
        float localX = Mathf.Lerp(rect.xMin, rect.xMax, Random.Range(ratioMin, ratioMax));
        float localY = rect.yMax + spawnHeightOffset;
        Vector3 worldPosition = area.TransformPoint(new Vector3(localX, localY, 0f));
        float rotation = Random.Range(0f, 360f);
        float pieceWorldScale = GetPieceLocalScale(sprite);

        // 出生点还有道具没让开时本次先生成，下次再试（避免叠生在堆积体内部被物理弹开）。
        if (skipSpawnWhenBlocked && IsSpawnAreaBlocked(worldPosition, sprite, rotation, pieceWorldScale))
        {
            TrimOverflowPieces();
            return;
        }

        TrimOverflowPieces();

        Transform parent = piecesRoot != null ? piecesRoot : transform;
        ItemFallingPiece piece = TakeFromPool(parent);
        Transform pieceTransform = piece.transform;
        pieceTransform.position = worldPosition;
        pieceTransform.rotation = Quaternion.Euler(0f, 0f, rotation);

        float rootScale = Mathf.Max(0.000001f, Mathf.Abs(parent.lossyScale.x));
        piece.Prepare(sprite, CreatePieceSettings(pieceWorldScale / rootScale));

        // 池化复用：复位之后才下发初速，避免残留上一颗的运动。
        piece.ApplySpawnDynamics(gravityScale, spawnSpin > 0f ? Random.Range(-spawnSpin, spawnSpin) : 0f);

        pieces.Add(piece);
    }

    /// <summary>回收一个碎片：放回对象池复用（不销毁）。</summary>
    private void RecyclePiece(ItemFallingPiece piece)
    {
        pieces.Remove(piece);
        if (piece == null)
            return;

        ReleaseToPool(piece);
    }

    private ItemFallingPiece TakeFromPool(Transform parent)
    {
        while (piecePool.Count > 0)
        {
            ItemFallingPiece pooled = piecePool.Pop();
            if (pooled != null)
                return pooled;
        }

        return Instantiate(piecePrefab, parent);
    }

    private void ReleaseToPool(ItemFallingPiece piece)
    {
        piece.PrepareForPool();

        // 按单次最大存活数兜底，避免池无限增长。
        if (piecePool.Count >= Mathf.Max(1, maxPieceCount))
        {
            Destroy(piece.gameObject);
            return;
        }

        piecePool.Push(piece);
    }

    private ItemFallPieceSettings CreatePieceSettings(float localScale)
    {
        return new ItemFallPieceSettings
        {
            scale = localScale,
            colliderFit = colliderFit,
            useOutlineColliderBounds = useOutlineColliderBounds,
            restSpeed = restSpeed,
            restSpin = restSpin,
            restDuration = restDuration,
            landedLifetime = landedLifetime,
            whiteDelay = whiteDelay,
            whiteDuration = whiteDuration,
            whiteColor = whiteColor,
        };
    }

    private void OnDestroy()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
                Destroy(pieces[i].gameObject);
        }

        pieces.Clear();

        while (piecePool.Count > 0)
        {
            ItemFallingPiece pooled = piecePool.Pop();
            if (pooled != null)
                Destroy(pooled.gameObject);
        }
    }

    private bool IsSpawnAreaBlocked(Vector3 worldPosition, Sprite sprite, float rotation, float pieceWorldScale)
    {
        Vector2 size;
        Vector2 offset;
        if (!useOutlineColliderBounds || !SpriteOutlineBounds.TryGet(sprite, out size, out offset))
            size = sprite.bounds.size;

        size *= colliderFit * pieceWorldScale;
        if (size.x <= 0f || size.y <= 0f)
            return false;

        return Physics2D.OverlapBox(worldPosition, size, rotation, Physics2D.AllLayers) != null;
    }

    private void TrimOverflowPieces()
    {
        int limit = Mathf.Max(1, maxPieceCount);
        while (pieces.Count >= limit && pieces.Count > 0)
            RecyclePiece(pieces[0]);
    }

    /// <summary>碎片缩放：按图标像素尺寸换算 —— 区域 1 单位 = 1 像素，pieceScale = 1 时图标按原始像素大小绘制。</summary>
    private float GetPieceLocalScale(Sprite sprite)
    {
        float rangeMin = Mathf.Min(pieceScaleRange.x, pieceScaleRange.y);
        float rangeMax = Mathf.Max(pieceScaleRange.x, pieceScaleRange.y);
        float pixelsPerUnit = sprite != null ? Mathf.Max(1f, sprite.pixelsPerUnit) : 100f;
        return pieceScale * Random.Range(rangeMin, rangeMax) * area.lossyScale.x * pixelsPerUnit;
    }

    private float GetSpawnInterval()
    {
        float min = Mathf.Max(0.02f, Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y));
        float max = Mathf.Max(min, Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y));
        return Random.Range(min, max);
    }

    private void FitBounds()
    {
        if (!autoFitBounds || area == null)
            return;

        Rect rect = area.rect;

        ApplyBox(floorCollider,
            new Vector2(rect.center.x, rect.yMin - floorThickness * 0.5f),
            new Vector2(rect.width + boundsPadding * 2f, floorThickness));

        ApplyBox(leftWallCollider,
            new Vector2(rect.xMin - wallThickness * 0.5f, rect.center.y + boundsPadding * 0.5f),
            new Vector2(wallThickness, rect.height + boundsPadding));

        ApplyBox(rightWallCollider,
            new Vector2(rect.xMax + wallThickness * 0.5f, rect.center.y + boundsPadding * 0.5f),
            new Vector2(wallThickness, rect.height + boundsPadding));
    }

    private void ApplyBox(BoxCollider2D box, Vector2 areaLocalCenter, Vector2 areaLocalSize)
    {
        if (box == null || area == null)
            return;

        Transform boxTransform = box.transform;
        Transform parent = boxTransform.parent;

        Vector3 worldCenter = area.TransformPoint(areaLocalCenter);
        Vector3 localCenter = parent != null ? parent.InverseTransformPoint(worldCenter) : worldCenter;
        float scaleX = parent != null ? SafeDivide(area.lossyScale.x, parent.lossyScale.x) : 1f;
        float scaleY = parent != null ? SafeDivide(area.lossyScale.y, parent.lossyScale.y) : 1f;
        Vector2 localSize = new Vector2(areaLocalSize.x * scaleX, areaLocalSize.y * scaleY);

        if (boxTransform.localPosition != localCenter)
            boxTransform.localPosition = localCenter;
        if (box.offset != Vector2.zero)
            box.offset = Vector2.zero;
        if (box.size != localSize)
            box.size = localSize;
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.000001f ? value / divisor : value;
    }

    private void ResolveReferences()
    {
        if (area == null)
            area = GetComponentInChildren<RectTransform>(true);
        if (piecesRoot == null)
            piecesRoot = transform;
    }

    private static Sprite PickItemSprite()
    {
        if (itemSpriteCache == null)
            itemSpriteCache = LoadItemSprites();

        return itemSpriteCache.Length > 0 ? itemSpriteCache[Random.Range(0, itemSpriteCache.Length)] : null;
    }

    private static Sprite[] LoadItemSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        HashSet<string> visited = new HashSet<string>();

        IReadOnlyDictionary<int, MagicData> magicTable = GameDataDatabase.MagicData;
        if (magicTable != null)
        {
            foreach (KeyValuePair<int, MagicData> pair in magicTable)
            {
                MagicData magic = pair.Value;
                if (magic == null || string.IsNullOrEmpty(magic.iconName) || !visited.Add(magic.iconName))
                    continue;

                Sprite sprite = Resources.Load<Sprite>("Images/Magics/" + magic.iconName);
                if (sprite != null)
                    sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            Sprite[] fallback = Resources.LoadAll<Sprite>("Images/Magics");
            if (fallback != null)
                sprites.AddRange(fallback);
        }

        return sprites.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnHeightOffset = Mathf.Max(0f, spawnHeightOffset);
        spawnXRange.x = Mathf.Clamp01(spawnXRange.x);
        spawnXRange.y = Mathf.Clamp01(spawnXRange.y);
        wallThickness = Mathf.Max(1f, wallThickness);
        floorThickness = Mathf.Max(1f, floorThickness);
        boundsPadding = Mathf.Max(0f, boundsPadding);

        if (Application.isPlaying || !gameObject.scene.IsValid())
            return;

        ResolveReferences();
        FitBounds();
    }
#endif
}
