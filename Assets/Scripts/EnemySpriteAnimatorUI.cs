using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemySpriteAnimatorUI : MonoBehaviour
{
    private const string EnemyAnimatorRoot = "Animations/Enemies/";

    [SerializeField] private Image targetImage;
    [SerializeField] private EnemyViewUI owner;
    [SerializeField] private Animator animator;

    private EnemyData boundData;

    private void Awake()
    {
        CacheReferences();
    }

    public void Bind(EnemyData data)
    {
        CacheReferences();

        boundData = data;
        Sprite sprite = EnemyVisualLoader.LoadStaticSpriteOrSample(data);
        ApplySprite(sprite);
        ApplyAnimator(data);
    }

    public void RefreshLayoutFromAnimatedSprite()
    {
        if (targetImage == null || targetImage.sprite == null)
            return;

        ApplySprite(targetImage.sprite);
    }

    private void CacheReferences()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
        if (owner == null)
            owner = GetComponentInParent<EnemyViewUI>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void ApplyAnimator(EnemyData data)
    {
        RuntimeAnimatorController controller = EnemyVisualLoader.LoadAnimatorController(data);
        if (controller == null)
        {
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }
            return;
        }

        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        RefreshLayoutFromAnimatedSprite();
    }

    private void ApplySprite(Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.enabled = true;
        targetImage.sprite = sprite;
        targetImage.preserveAspect = true;
        if (sprite == null)
            return;

        targetImage.color = Color.white;
        targetImage.rectTransform.sizeDelta = sprite.rect.size * GetImageScale();
        if (owner == null)
            owner = GetComponentInParent<EnemyViewUI>();
        if (owner != null)
            owner.ApplyDataLayout(boundData);
    }

    private float GetImageScale()
    {
        return boundData != null && boundData.imageScale > 0f ? boundData.imageScale : 1f;
    }
}

public static class EnemyVisualLoader
{
    private const string EnemyImageRoot = "Images/Enemies/";
    private const string EnemyAnimatorRoot = "Animations/Enemies/";
    private const string SampleIconName = "Sample";

    private static readonly System.Collections.Generic.Dictionary<string, RuntimeAnimatorController> animatorCache = new System.Collections.Generic.Dictionary<string, RuntimeAnimatorController>();
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> spriteCache = new System.Collections.Generic.Dictionary<string, Sprite>();

    public static RuntimeAnimatorController LoadAnimatorController(EnemyData data)
    {
        if (data == null)
            return null;

        RuntimeAnimatorController controller = LoadAnimatorController(data.spriteAnimationPath);
        return controller != null ? controller : LoadAnimatorController(data.iconName);
    }

    public static RuntimeAnimatorController LoadAnimatorController(string pathOrName)
    {
        string path = NormalizeEnemyAnimatorPath(pathOrName);
        if (string.IsNullOrEmpty(path))
            return null;

        if (animatorCache.TryGetValue(path, out RuntimeAnimatorController controller))
            return controller;

        controller = Resources.Load<RuntimeAnimatorController>(path);
        animatorCache[path] = controller;
        return controller;
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

        if (spriteCache.TryGetValue(path, out Sprite sprite))
            return sprite;

        sprite = Resources.Load<Sprite>(path);
        spriteCache[path] = sprite;
        return sprite;
    }

    private static string NormalizeEnemyImagePath(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName))
            return null;

        return pathOrName.StartsWith(EnemyImageRoot, System.StringComparison.Ordinal) ? pathOrName : EnemyImageRoot + pathOrName;
    }

    private static string NormalizeEnemyAnimatorPath(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName))
            return null;

        return pathOrName.StartsWith(EnemyAnimatorRoot, System.StringComparison.Ordinal) ? pathOrName : EnemyAnimatorRoot + pathOrName;
    }
}
