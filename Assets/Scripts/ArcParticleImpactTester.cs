using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ArcParticleImpactTester : MonoBehaviour, ISpellCastMultiTargetImpactEffect
{
    [SerializeField] private RectTransform startPoint;
    [SerializeField] private RectTransform targetPoint;
    [SerializeField] private int projectileCount = 3;
    [SerializeField] private float launchInterval = 0.08f;
    [SerializeField] private float travelDuration = 0.43f;
    [SerializeField] private float arcHeight = 180f;
    [SerializeField] private Vector2 startSpread = new Vector2(32f, 12f);
    [SerializeField] private Vector2 targetSpread = new Vector2(26f, 14f);
    [SerializeField] private float projectileSize = 18f;
    [SerializeField] private float materialFillProjectileScale = 2.5f;
    [SerializeField] private float magicProjectileSize = 56f;
    [SerializeField] private float trailWidth = 10f;
    [SerializeField] private Color projectileColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color trailColor = new Color(1f, 0.36f, 0.08f, 1f);
    [SerializeField] private Color impactColor = new Color(1f, 0.86f, 0.18f, 1f);
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop;
    [SerializeField] private float loopDelay = 0.53f;

    private const float ImpactFadeDuration = 0.15f;

    private readonly List<GameObject> effectObjects = new List<GameObject>();
    private Coroutine routine;

    public float BurstDuration => travelDuration + Mathf.Max(0, projectileCount - 1) * launchInterval + ImpactFadeDuration;
    public float SingleProjectileImpactDelay => travelDuration;

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
    }

    [ContextMenu("Play Test Effect")]
    public void Play()
    {
        Stop();
        routine = StartCoroutine(PlayRoutine());
    }

    public void PlayBurst(RectTransform from, RectTransform to, int count, Color color)
    {
        PlayBurst(from, to, count, color, null);
    }

    private void PlayBurst(RectTransform from, RectTransform to, int count, Color color, Sprite projectileSprite)
    {
        PlayBurst(from, to, count, color, projectileSprite, projectileSize);
    }

    private void PlayBurst(RectTransform from, RectTransform to, int count, Color color, Sprite projectileSprite, float visualSize, Action onImpact = null)
    {
        if (from == null || to == null)
            return;

        projectileCount = Mathf.Max(1, count);
        projectileColor = color;
        trailColor = new Color(color.r, color.g, color.b, 1f);
        impactColor = new Color(Mathf.Min(1f, color.r + 0.18f), Mathf.Min(1f, color.g + 0.18f), Mathf.Min(1f, color.b + 0.18f), 1f);
        StartCoroutine(PlayBurstRoutine(GetLocalCenter(from), GetLocalCenter(to), projectileCount, projectileSprite, Mathf.Max(1f, visualSize), onImpact));
    }

    public void PlayMaterialFill(RectTransform from, RectTransform magicView, MaterialEnum material)
    {
        Sprite icon = MaterialCardView.GetMaterialIcon(material);
        PlayBurst(from, magicView, 1, Color.white, icon, projectileSize * materialFillProjectileScale);
    }


    public void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType)
    {
        PlayCast(magic, from, target, targetType, null);
    }

    public void PlayCast(MagicModel magic, RectTransform from, RectTransform target, SpellEffectTarget targetType, Action onImpact)
    {
        if (magic == null)
            return;

        GetCastVisual(magic, out Sprite icon, out Color color, out float visualSize);
        PlayBurst(from, target, 1, color, icon, visualSize, onImpact);
    }

    public void PlayCast(MagicModel magic, RectTransform from, IReadOnlyList<RectTransform> targets, SpellEffectTarget targetType, Action onImpact)
    {
        if (magic == null || targets == null)
            return;

        int lastTargetIndex = -1;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                lastTargetIndex = i;
        }

        if (lastTargetIndex < 0)
            return;

        GetCastVisual(magic, out Sprite icon, out Color color, out float visualSize);
        for (int i = 0; i < targets.Count; i++)
        {
            RectTransform target = targets[i];
            if (target != null)
                PlayBurst(from, target, 1, color, icon, visualSize, i == lastTargetIndex ? onImpact : null);
        }
    }

    private void GetCastVisual(MagicModel magic, out Sprite icon, out Color color, out float visualSize)
    {
        icon = LoadMagicIcon(magic.Data.iconName);
        color = magic.Data.recipe != null && magic.Data.recipe.Length > 0 ? MaterialCardView.GetMaterialColor(magic.Data.recipe[0]) : Color.white;
        visualSize = icon != null ? magicProjectileSize : projectileSize;
    }

    public void SetTestPlayback(bool playOnStart, bool loop)
    {
        this.playOnStart = playOnStart;
        this.loop = loop;
    }

    [ContextMenu("Stop Test Effect")]
    public void Stop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        DOTween.Kill(this);
        for (int i = 0; i < effectObjects.Count; i++)
        {
            if (effectObjects[i] != null)
                Destroy(effectObjects[i]);
        }

        effectObjects.Clear();
    }

    private IEnumerator PlayRoutine()
    {
        do
        {
            for (int i = 0; i < projectileCount; i++)
            {
                LaunchProjectile(i);
                yield return new WaitForSeconds(launchInterval);
            }

            yield return new WaitForSeconds(travelDuration + loopDelay);
        }
        while (loop);

        routine = null;
    }

    private IEnumerator PlayBurstRoutine(Vector2 start, Vector2 end, int count, Sprite projectileSprite, float visualSize, Action onImpact)
    {
        for (int i = 0; i < count; i++)
        {
            LaunchProjectile(start, end, i, count, projectileSprite, visualSize, i == count - 1 ? onImpact : null);
            yield return new WaitForSeconds(launchInterval);
        }
    }

    private void LaunchProjectile(int index)
    {
        if (startPoint == null || targetPoint == null)
            return;

        LaunchProjectile(startPoint.anchoredPosition, targetPoint.anchoredPosition, index, projectileCount, null, projectileSize, null);
    }

    private void LaunchProjectile(Vector2 startCenter, Vector2 endCenter, int index, int count, Sprite projectileSprite, float visualSize, Action onImpact)
    {
        RectTransform projectile = CreateImage("ArcProjectile", visualSize, projectileSprite != null ? Color.white : projectileColor, projectileSprite);
        RectTransform trail = CreateImage("ArcTrail", trailWidth, trailColor);
        trail.pivot = new Vector2(0f, 0.5f);
        trail.sizeDelta = new Vector2(1f, trailWidth);
        trail.SetAsFirstSibling();

        Vector2 start = GetOffsetPoint(startCenter, startSpread, index, count);
        Vector2 end = GetOffsetPoint(endCenter, targetSpread, index, count);
        Vector2 control = (start + end) * 0.5f + Vector2.up * arcHeight + Vector2.right * ((index - (count - 1) * 0.5f) * 38f);
        Vector2 lastPosition = start;

        projectile.anchoredPosition = start;
        trail.anchoredPosition = start;
        trail.gameObject.SetActive(false);

        float progress = 0f;
        DOTween.To(() => progress, value =>
        {
            progress = value;
            Vector2 position = EvaluateQuadraticBezier(start, control, end, progress);
            projectile.anchoredPosition = position;

            Vector2 delta = position - lastPosition;
            if (delta.sqrMagnitude > 0.01f)
            {
                trail.gameObject.SetActive(true);
                trail.anchoredPosition = lastPosition;
                trail.sizeDelta = new Vector2(delta.magnitude + visualSize * 0.4f, trailWidth);
                trail.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }

            lastPosition = position;
        }, 1f, travelDuration).SetEase(Ease.InOutSine).SetTarget(this).OnComplete(() =>
        {
            onImpact?.Invoke();
            SpawnImpact(end, projectileSprite, visualSize);
            effectObjects.Remove(projectile.gameObject);
            effectObjects.Remove(trail.gameObject);
            Destroy(projectile.gameObject);
            Destroy(trail.gameObject);
        });
    }

    private RectTransform CreateImage(string objectName, float size, Color color)
    {
        return CreateImage(objectName, size, color, null);
    }

    private RectTransform CreateImage(string objectName, float size, Color color, Sprite sprite)
    {
        GameObject instance = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        instance.transform.SetParent(transform, false);
        effectObjects.Add(instance);

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        Image image = instance.GetComponent<Image>();
        color.a = 1f;
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    private void SpawnImpact(Vector2 position, Sprite projectileSprite, float visualSize)
    {
        RectTransform impact = CreateImage("ArcImpact", visualSize * 1.15f, projectileSprite != null ? Color.white : impactColor, projectileSprite);
        impact.anchoredPosition = position;

        CanvasGroup canvasGroup = impact.gameObject.AddComponent<CanvasGroup>();
        float impactScale = projectileSprite != null ? 1.45f : 2.8f;
        Sequence sequence = DOTween.Sequence();
        sequence.Join(impact.DOScale(Vector3.one * impactScale, ImpactFadeDuration).SetEase(Ease.OutCubic));
        sequence.Join(canvasGroup.DOFade(0f, ImpactFadeDuration));
        sequence.SetTarget(this);
        sequence.OnComplete(() =>
        {
            effectObjects.Remove(impact.gameObject);
            Destroy(impact.gameObject);
        });
    }

    private static Sprite LoadMagicIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        return Resources.Load<Sprite>("Images/Magics/" + iconName);
    }

    private Vector2 GetLocalCenter(RectTransform rectTransform)
    {
        RectTransform root = (RectTransform)transform;
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(null, worldCenter), null, out Vector2 localPoint);
        return localPoint;
    }

    private static Vector2 GetOffsetPoint(Vector2 center, Vector2 spread, int index, int count)
    {
        if (count <= 1)
            return center;

        float normalized = index / (float)(count - 1) - 0.5f;
        float wave = index % 2 == 0 ? 1f : -1f;
        return center + new Vector2(spread.x * normalized, spread.y * wave);
    }

    private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
    }
}
