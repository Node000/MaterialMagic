using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemySpriteAnimatorUI : MonoBehaviour
{
    private const float DefaultFrameRate = 8f;

    [SerializeField] private Image targetImage;
    [SerializeField] private EnemyViewUI owner;

    private Sprite[] frames = Array.Empty<Sprite>();
    private EnemyData boundData;
    private Vector2 currentNativeSize;
    private float imageScale = 1f;
    private float frameInterval;
    private float elapsed;
    private int frameIndex;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
        if (owner == null)
            owner = GetComponentInParent<EnemyViewUI>();
    }

    public void Bind(EnemyData data)
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
        if (owner == null)
            owner = GetComponentInParent<EnemyViewUI>();

        boundData = data;
        frames = EnemyVisualLoader.LoadAnimationFrames(data);
        float frameRate = data != null && data.animationFrameRate > 0f ? data.animationFrameRate : DefaultFrameRate;
        frameInterval = 1f / frameRate;
        elapsed = 0f;
        frameIndex = 0;
        currentNativeSize = Vector2.zero;
        imageScale = data != null && data.imageScale > 0f ? data.imageScale : 1f;

        Sprite sprite = frames.Length > 0 ? frames[0] : EnemyVisualLoader.LoadStaticSpriteOrSample(data);
        ApplySprite(sprite);

        enabled = targetImage != null && frames.Length > 1;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < frameInterval)
            return;

        int step = (int)(elapsed / frameInterval);
        elapsed -= step * frameInterval;
        frameIndex = (frameIndex + step) % frames.Length;
        ApplySprite(frames[frameIndex]);
    }

    private void ApplySprite(Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = sprite;
        targetImage.preserveAspect = true;
        if (sprite == null)
            return;

        targetImage.color = Color.white;
        Vector2 spriteSize = sprite.rect.size * imageScale;
        if (spriteSize != currentNativeSize)
        {
            targetImage.rectTransform.sizeDelta = spriteSize;
            currentNativeSize = spriteSize;
        }
        if (owner == null)
            owner = GetComponentInParent<EnemyViewUI>();
        if (owner != null)
            owner.ApplyDataLayout(boundData);
    }
}

public static class EnemyVisualLoader
{
    private const string EnemyImageRoot = "Images/Enemies/";
    private const string SampleIconName = "Sample";

    private static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, Sprite> StaticSpriteCache = new Dictionary<string, Sprite>();

    public static Sprite[] LoadAnimationFrames(EnemyData data)
    {
        if (data == null)
            return Array.Empty<Sprite>();

        Sprite[] frames = LoadFrames(data.spriteAnimationPath);
        if (frames.Length > 0)
            return frames;

        return LoadFrames(data.iconName);
    }

    public static Sprite LoadStaticSpriteOrSample(EnemyData data)
    {
        Sprite sprite = data != null ? LoadStaticSprite(data.iconName) : null;
        return sprite != null ? sprite : LoadStaticSprite(SampleIconName);
    }

    public static Sprite LoadStaticSprite(string iconName)
    {
        string path = NormalizeEnemyImagePath(iconName);
        if (string.IsNullOrEmpty(path))
            return null;

        if (StaticSpriteCache.TryGetValue(path, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(path);
        StaticSpriteCache[path] = sprite;
        return sprite;
    }

    private static Sprite[] LoadFrames(string pathOrName)
    {
        string path = NormalizeEnemyImagePath(pathOrName);
        if (string.IsNullOrEmpty(path))
            return Array.Empty<Sprite>();

        if (FrameCache.TryGetValue(path, out Sprite[] cachedFrames))
            return cachedFrames;

        Sprite[] loadedFrames = Resources.LoadAll<Sprite>(path);
        if (loadedFrames == null || loadedFrames.Length == 0)
            loadedFrames = Array.Empty<Sprite>();
        else
            Array.Sort(loadedFrames, CompareSpriteNames);

        FrameCache[path] = loadedFrames;
        return loadedFrames;
    }

    private static string NormalizeEnemyImagePath(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName))
            return null;

        return pathOrName.StartsWith(EnemyImageRoot, StringComparison.Ordinal) ? pathOrName : EnemyImageRoot + pathOrName;
    }

    private static int CompareSpriteNames(Sprite left, Sprite right)
    {
        return string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, StringComparison.Ordinal);
    }
}
