using System.Collections.Generic;
using UnityEngine;

/// <summary>道具碎片的运行参数：统一由 <see cref="ItemWaterfallEffect"/> 下发，避免同一个参数散落在预制体和场景两处。</summary>
public struct ItemFallPieceSettings
{
    public float scale;
    public float colliderFit;
    public bool useOutlineColliderBounds;
    public float restSpeed;
    public float restSpin;
    public float restDuration;
    public float landedLifetime;
    public float grayDelay;
    public float grayDuration;
    public Color grayTint;
    public float removeDuration;
}

/// <summary>
/// 道具瀑布里掉落的单个道具：落地停稳后逐渐变成黑白，被回收时缩小消失。
/// 由 <see cref="ItemWaterfallEffect"/> 在运行时生成并驱动，不要手动放进场景。
/// </summary>
[DisallowMultipleComponent]
public class ItemFallingPiece : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private BoxCollider2D hitBox;

    private static readonly int GrayAmountId = Shader.PropertyToID("_GrayAmount");
    private static readonly int GrayTintId = Shader.PropertyToID("_GrayTint");

    private Material materialInstance;
    private float colliderFit = 0.92f;
    private bool useOutlineColliderBounds = true;
    private float restSpeed = 0.08f;
    private float restSpin = 40f;
    private float restDuration = 0.2f;
    private float landedLifetime;
    private float grayDelay;
    private float grayDuration;
    private float removeDuration;
    private float restingTime;
    private float lifetimeTimer = -1f;
    private float grayTimer = -1f;
    private float grayAmount;
    private float removeTimer = -1f;
    private Vector3 standingScale = Vector3.one;
    private bool touchedSomething;
    private bool dying;

    public bool IsDying => dying;
    public float GrayAmount => grayAmount;

    /// <summary>开始消失时回调（不论是被数量上限回收还是存活时间到期），控制器据此把自己从队列里摘掉。</summary>
    public event System.Action<ItemFallingPiece> RemovalStarted;

    private void Awake()
    {
        ResolveReferences();
        standingScale = transform.localScale;
    }

    /// <summary>生成时由瀑布控制器调用：下发参数、绑定图标、贴碰撞箱、重置状态。</summary>
    public void Prepare(Sprite sprite, ItemFallPieceSettings settings)
    {
        ResolveReferences();

        colliderFit = Mathf.Clamp(settings.colliderFit, 0.05f, 1.5f);
        useOutlineColliderBounds = settings.useOutlineColliderBounds;
        restSpeed = Mathf.Max(0f, settings.restSpeed);
        restSpin = Mathf.Max(0f, settings.restSpin);
        restDuration = Mathf.Max(0f, settings.restDuration);
        landedLifetime = Mathf.Max(0f, settings.landedLifetime);
        grayDelay = Mathf.Max(0f, settings.grayDelay);
        grayDuration = Mathf.Max(0.01f, settings.grayDuration);
        removeDuration = Mathf.Max(0.01f, settings.removeDuration);

        standingScale = Vector3.one * Mathf.Max(0.0001f, settings.scale);
        transform.localScale = standingScale;

        if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;

        FitCollider(sprite);
        ApplyGrayTint(settings.grayTint);

        restingTime = 0f;
        lifetimeTimer = -1f;
        grayTimer = -1f;
        grayAmount = 0f;
        removeTimer = -1f;
        touchedSomething = false;
        dying = false;

        SetGrayAmount(0f);

        if (body != null)
        {
            body.simulated = true;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    /// <summary>被瀑布控制器回收：停止物理并缩小消失。</summary>
    public void RequestRemove()
    {
        BeginRemoval();
    }

    private void BeginRemoval()
    {
        if (dying)
            return;

        dying = true;
        removeTimer = 0f;
        if (body != null)
            body.simulated = false;
        if (hitBox != null)
            hitBox.enabled = false;

        RemovalStarted?.Invoke(this);
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (dying)
        {
            UpdateRemove(delta);
            return;
        }

        UpdateLifetime(delta);
        UpdateResting(delta);
        UpdateGray(delta);
    }

    /// <summary>落地（第一次碰到地面/其它道具）后开始计时，到点自行消失。</summary>
    private void UpdateLifetime(float delta)
    {
        if (!touchedSomething || landedLifetime <= 0f)
            return;

        lifetimeTimer = lifetimeTimer < 0f ? 0f : lifetimeTimer + delta;
        if (lifetimeTimer >= landedLifetime)
            BeginRemoval();
    }

    private void UpdateRemove(float delta)
    {
        if (removeTimer < 0f)
            return;

        removeTimer += delta;
        float progress = Mathf.Clamp01(removeTimer / removeDuration);
        transform.localScale = standingScale * (1f - progress);
        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void UpdateResting(float delta)
    {
        if (grayTimer >= 0f || !touchedSomething || body == null)
            return;

        bool resting = body.velocity.sqrMagnitude <= restSpeed * restSpeed && Mathf.Abs(body.angularVelocity) <= restSpin;
        restingTime = resting ? restingTime + delta : 0f;
        if (restingTime >= restDuration)
            grayTimer = 0f;
    }

    private void UpdateGray(float delta)
    {
        if (grayTimer < 0f)
            return;

        grayTimer += delta;
        float progress = grayDelay <= 0f ? 1f : Mathf.Clamp01((grayTimer - grayDelay) / grayDuration);
        if (Mathf.Approximately(progress, grayAmount))
            return;

        grayAmount = progress;
        SetGrayAmount(grayAmount);
    }

    private void SetGrayAmount(float amount)
    {
        if (materialInstance == null)
            return;

        materialInstance.SetFloat(GrayAmountId, amount);
    }

    /// <summary>
    /// 每个碎片用一份材质实例驱动黑白（不要用 MaterialPropertyBlock：SpriteRenderer 会把
    /// Sprite 纹理也放在同一个 property block 里，外部覆盖会丢掉 _MainTex）。
    /// </summary>
    private void ApplyGrayTint(Color grayTint)
    {
        if (spriteRenderer == null || spriteRenderer.sharedMaterial == null)
            return;

        if (materialInstance == null)
        {
            materialInstance = new Material(spriteRenderer.sharedMaterial);
            spriteRenderer.sharedMaterial = materialInstance;
        }

        materialInstance.SetFloat(GrayAmountId, 0f);
        materialInstance.SetColor(GrayTintId, grayTint);
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
            Destroy(materialInstance);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        touchedSomething = true;
    }

    private void ResolveReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (hitBox == null)
            hitBox = GetComponent<BoxCollider2D>();
    }

    private void FitCollider(Sprite sprite)
    {
        if (hitBox == null || sprite == null)
            return;

        Vector2 size;
        Vector2 offset;
        if (!useOutlineColliderBounds || !SpriteOutlineBounds.TryGet(sprite, out size, out offset))
        {
            // 该图标没有导入期生成的物理轮廓时，退回整张贴图框（含透明边，堆积会偏松）。
            size = sprite.bounds.size;
            offset = sprite.bounds.center;
        }

        size *= colliderFit;
        if (size.x <= 0f || size.y <= 0f)
            return;

        hitBox.size = size;
        hitBox.offset = offset;
    }
}

/// <summary>
/// 读取 Sprite 导入期生成的非透明轮廓（Unity 在导入贴图时就生成好了，运行时不需要读像素），
/// 并缓存每个图标的轮廓包围盒，用于自动碰撞箱。坐标即 Sprite 本地坐标，可直接当 BoxCollider2D 的 size/offset。
/// </summary>
internal static class SpriteOutlineBounds
{
    private static readonly Dictionary<int, Rect> boundsCache = new Dictionary<int, Rect>();

    public static bool TryGet(Sprite sprite, out Vector2 size, out Vector2 offset)
    {
        size = Vector2.zero;
        offset = Vector2.zero;
        if (sprite == null)
            return false;

        int key = sprite.GetInstanceID();
        if (!boundsCache.TryGetValue(key, out Rect bounds))
        {
            bounds = BuildBounds(sprite);
            boundsCache[key] = bounds;
        }

        if (bounds.width <= 0f || bounds.height <= 0f)
            return false;

        size = new Vector2(bounds.width, bounds.height);
        offset = bounds.center;
        return true;
    }

    private static Rect BuildBounds(Sprite sprite)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return new Rect(0f, 0f, 0f, 0f);

        List<Vector2> points = new List<Vector2>();
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int shape = 0; shape < shapeCount; shape++)
        {
            points.Clear();
            sprite.GetPhysicsShape(shape, points);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                if (point.x < minX) minX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.x > maxX) maxX = point.x;
                if (point.y > maxY) maxY = point.y;
            }
        }

        if (maxX <= minX || maxY <= minY)
            return new Rect(0f, 0f, 0f, 0f);

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
}
