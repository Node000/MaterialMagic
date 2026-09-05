using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 附魔 RT 链渲染器：挂在每张箭头卡的 icon Image 所在 GameObject 上（或用 <see cref="ApplyTo"/> 创建）。
/// 把 card.modifiers 里的每个附魔当作一个按顺序执行的离屏渲染阶段：
/// 阶段 i 以「原 Sprite 纹理 / 阶段 i-1 的输出 RT」作为其真实 visualMaterial 的 _MainTex，
/// 离屏绘制到该阶段的 RT；最终 RT 替换 Image 的显示纹理。
/// 这样后一个附魔能消费前一个的几何/裁剪结果，实现真正的链式叠加，且无需改动任何附魔 shader。
/// 刷新：默认 30fps（可配）；无附魔的卡不走此组件。
/// </summary>
[RequireComponent(typeof(Image))]
public class MaterialModifierRTChain : MonoBehaviour
{
    [SerializeField, Min(1)] private float refreshFps = 30f;
    [SerializeField, Min(8)] private int maxResolution = 512;

    private Image image;
    private RectTransform rectTransform;
    private MaterialModel card;

    private readonly List<RenderTexture> stageRTs = new List<RenderTexture>();
    private readonly List<Material> stageMaterials = new List<Material>();
    private RenderTexture resultRT;
    private Sprite originalSprite;
    private RawImage resultDisplay;
    private Color originalColor = Color.white;
    private Material originalMaterial;
    private bool hasOriginalState;
    private bool imageHidden;

    private float refreshInterval;
    private float timer;
    private bool initialized;
    private bool running;
    private bool dirty = true;

    public MaterialModel Card => card;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = (RectTransform)transform;
        refreshInterval = 1f / Mathf.Max(1f, refreshFps);
    }

    private void OnEnable()
    {
        if (!initialized)
            return;
        if (card == null || card.modifiers == null || card.modifiers.Count == 0)
            return;
        if (running)
            return;

        CaptureOriginalStateIfNeeded();

        // OnDisable 释放过 RT，重新启用需重建。
        if (stageMaterials.Count == 0)
            RebuildStagesIfNeeded();
        if (stageMaterials.Count == 0)
            return;
        EnsureResultRT();
        image.material = null;
        image.color = Color.clear;
        imageHidden = true;
        EnsureDisplay();
        running = true;
        dirty = true;
    }

    private void OnDisable()
    {
        StopChain(true);
    }

    private void OnDestroy()
    {
        StopChain(false);
    }

    private void Update()
    {
        if (!running || card == null)
            return;

        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval && !dirty)
            return;

        timer = 0f;
        dirty = false;
        RenderChain();
    }

    /// <summary>从 Image + card 建立 RT 链（幂等）。</summary>
    public void Setup(Image targetImage, MaterialModel materialCard)
    {
        image = targetImage != null ? targetImage : GetComponent<Image>();
        rectTransform = (RectTransform)image.transform;

        if (!imageHidden || image.color.a > 0f)
        {
            originalColor = image.color;
            originalMaterial = image.material;
            hasOriginalState = true;
        }
        originalSprite = image.sprite;
        card = materialCard;

        if (!initialized)
        {
            refreshInterval = 1f / Mathf.Max(1f, refreshFps);
            initialized = true;
        }

        if (card == null || card.modifiers == null || card.modifiers.Count == 0)
        {
            StopChain(false);
            return;
        }

        // RT 链由 RawImage 直接显示，原始 Image 仅保留点击区域。
        CaptureOriginalStateIfNeeded();
        image.material = null;
        image.color = Color.clear;
        imageHidden = true;
        RebuildStagesIfNeeded();
        if (stageMaterials.Count == 0)
            return;
        EnsureResultRT();
        EnsureDisplay();
        running = true;
        dirty = true;
    }

    public void RefreshNow()
    {
        if (running && card != null)
        {
            dirty = true;
            timer = refreshInterval;
        }
    }

    private void RebuildStagesIfNeeded()
    {
        int effectCount = card.modifiers.Count;
        List<Material> wanted = new List<Material>(effectCount);
        for (int i = 0; i < effectCount; i++)
        {
            MaterialModifierModel modifier = card.modifiers[i];
            if (modifier == null)
                continue;
            Material mat = CreateStageMaterial(modifier);
            if (mat != null)
                wanted.Add(mat);
        }

        if (wanted.Count == 0)
        {
            StopChain(false);
            return;
        }

        // 复用已分配的 RT/材质，尽量少重建
        ResolveResolution(out int targetWidth, out int targetHeight);
        int intermediateCount = Mathf.Max(0, wanted.Count - 1);
        bool sameCount = wanted.Count == stageMaterials.Count && intermediateCount == stageRTs.Count;
        if (sameCount)
        {
            for (int i = 0; i < stageMaterials.Count; i++)
            {
                if (stageMaterials[i].shader != wanted[i].shader)
                {
                    sameCount = false;
                    break;
                }
            }
        }
        if (sameCount)
        {
            for (int i = 0; i < stageRTs.Count; i++)
            {
                if (stageRTs[i] == null
                    || stageRTs[i].width != targetWidth
                    || stageRTs[i].height != targetHeight)
                {
                    sameCount = false;
                    break;
                }
            }
        }

        if (sameCount)
        {
            // 仅更新材质属性（参数可能随时间/状态变），不重分配
            for (int i = 0; i < wanted.Count; i++)
                CopyMaterialProps(stageMaterials[i], wanted[i]);
            for (int i = 0; i < stageMaterials.Count; i++)
                DestroyObject(wanted[i]);
        }
        else
        {
            ReleaseStages();
            for (int i = 0; i < wanted.Count; i++)
                stageMaterials.Add(wanted[i]);
            for (int i = 0; i < intermediateCount; i++)
                stageRTs.Add(AllocateRT());
        }

        EnsureResultRT();
    }

    private static void DestroyObject(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private Material CreateStageMaterial(MaterialModifierModel modifier)
    {
        Material template = null;
        if (!MaterialModifierDisplayDatabase.TryGetVisualMaterial(modifier, out template))
            return null;
        if (template == null || template.shader == null)
            return null;

        Material mat = new Material(template)
        {
            name = "RTStage_" + modifier.GetType().Name,
            hideFlags = HideFlags.DontSave
        };
        CopyMaterialProps(mat, template);
        return mat;
    }

    private void CopyMaterialProps(Material target, Material source)
    {
        if (target == null || source == null)
            return;
        // 复制所有着色器属性（颜色/浮点/纹理）以继承 visualMaterial 的参数
        Shader shader = source.shader;
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            string name = shader.GetPropertyName(i);
            if (!target.HasProperty(name))
                continue;
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    target.SetColor(name, source.GetColor(name));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    target.SetFloat(name, source.GetFloat(name));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    target.SetVector(name, source.GetVector(name));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    // 避免把模板的 _MainTex 也带过来；链内会单独指定 _MainTex
                    if (name != "_MainTex")
                        target.SetTexture(name, source.GetTexture(name));
                    break;
            }
        }
    }

    private RenderTexture AllocateRT()
    {
        ResolveResolution(out int width, out int height);
        RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "ModifierRTStage",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        rt.Create();
        return rt;
    }

    private void EnsureResultRT()
    {
        ResolveResolution(out int width, out int height);
        if (resultRT != null && resultRT.width == width && resultRT.height == height)
            return;
        ReleaseResult();
        resultRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "ModifierRTResult",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        resultRT.Create();
    }

    private void ResolveResolution(out int width, out int height)
    {
        int sourceWidth = image != null && image.sprite != null && image.sprite.texture != null ? image.sprite.texture.width : 128;
        int sourceHeight = image != null && image.sprite != null && image.sprite.texture != null ? image.sprite.texture.height : 128;
        float scaleFactor = image != null && image.canvas != null ? image.canvas.scaleFactor : 1f;
        float displayWidth = rectTransform != null && rectTransform.rect.width > 0f ? rectTransform.rect.width * scaleFactor : sourceWidth;
        float displayHeight = rectTransform != null && rectTransform.rect.height > 0f ? rectTransform.rect.height * scaleFactor : sourceHeight;

        width = Mathf.Clamp(Mathf.RoundToInt(displayWidth), 8, maxResolution);
        height = Mathf.Clamp(Mathf.RoundToInt(displayHeight), 8, maxResolution);
    }

    private void RenderChain()
    {
        if (stageMaterials.Count == 0)
            return;

        Texture inputTex = originalSprite != null ? originalSprite.texture : null;
        if (inputTex == null)
            return;

        int lastStage = stageMaterials.Count - 1;
        for (int i = 0; i < stageMaterials.Count; i++)
        {
            RenderTexture targetRT = i == lastStage ? resultRT : stageRTs[i];
            RenderTexture prevRT = i > 0 ? stageRTs[i - 1] : null;
            Texture stageInput = i == 0 ? inputTex : (Texture)prevRT;

            Material mat = stageMaterials[i];
            // 每阶段将上级输出设为 _MainTex，并用 _MainTex 的 alpha/rect 语义离屏绘制
            mat.SetTexture("_MainTex", stageInput);
            ApplyDynamicPerStage(mat);
            ClearRenderTexture(targetRT);
            Graphics.Blit(stageInput, targetRT, mat);
        }

        UpdateDisplayTexture();
    }

    private void ApplyDynamicPerStage(Material mat)
    {
        // 各附魔 shader 依赖 _Time 自行动画，此处无需额外传参。
        // 需要把卡方向传给会读取 _ArrowDirection 的附魔（Half/Fragile 按方向选切线角度）。
        if (mat.HasProperty("_ArrowDirection"))
            mat.SetFloat("_ArrowDirection", GetArrowDirection(card));
    }

    private void ClearRenderTexture(RenderTexture target)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    private void EnsureDisplay()
    {
        if (resultDisplay != null)
            return;

        GameObject displayObject = new GameObject("ModifierRTDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        displayObject.transform.SetParent(image.transform, false);
        displayObject.transform.SetAsLastSibling();

        RectTransform rect = (RectTransform)displayObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        resultDisplay = displayObject.GetComponent<RawImage>();
        resultDisplay.raycastTarget = false;
        resultDisplay.maskable = image != null && image.maskable;
        resultDisplay.enabled = false;
    }

    private void UpdateDisplayTexture()
    {
        if (image == null || resultRT == null)
            return;

        EnsureDisplay();
        resultDisplay.texture = resultRT;
        resultDisplay.color = Color.white;
        resultDisplay.enabled = true;
    }

    private float GetArrowDirection(MaterialModel materialCard)
    {
        if (materialCard == null)
            return 0f;
        MaterialEnum m = materialCard.GetArrowDisplayMaterial();
        if (m == MaterialEnum.None)
            m = materialCard.material;
        switch (m)
        {
            case MaterialEnum.Fire: return 0f;
            case MaterialEnum.Water: return 1f;
            case MaterialEnum.Wind: return 2f;
            case MaterialEnum.Earth: return 3f;
            default: return 0f;
        }
    }

    private void CaptureOriginalStateIfNeeded()
    {
        if (hasOriginalState || image == null)
            return;
        originalColor = image.color;
        originalMaterial = image.material;
        hasOriginalState = true;
    }

    private void StopChain(bool preserveOriginalState)
    {
        running = false;
        ReleaseStages();
        ReleaseResult();
        if (resultDisplay != null)
        {
            resultDisplay.texture = null;
            resultDisplay.enabled = false;
        }
        if (imageHidden && hasOriginalState && image != null)
        {
            image.color = originalColor;
            image.material = originalMaterial;
        }
        imageHidden = false;
        if (!preserveOriginalState)
            hasOriginalState = false;
    }

    private void ReleaseStages()
    {
        for (int i = 0; i < stageMaterials.Count; i++)
            DestroyObject(stageMaterials[i]);
        for (int i = 0; i < stageRTs.Count; i++)
        {
            if (stageRTs[i] == null)
                continue;
            stageRTs[i].Release();
            DestroyObject(stageRTs[i]);
        }
        stageMaterials.Clear();
        stageRTs.Clear();
    }

    private void ReleaseResult()
    {
        if (resultRT == null)
            return;
        resultRT.Release();
        DestroyObject(resultRT);
        resultRT = null;
    }

    /// <summary>供外部使用：确保 targetImage 所在物体上有一个已 Setup 的链。</summary>
    public static void ApplyTo(Image targetImage, MaterialModel card)
    {
        if (targetImage == null)
            return;

        if (card == null || card.modifiers == null || card.modifiers.Count == 0)
        {
            MaterialModifierRTChain existing = targetImage.GetComponent<MaterialModifierRTChain>();
            if (existing != null)
                existing.Setup(targetImage, null);
            return;
        }

        MaterialModifierRTChain chain = targetImage.GetComponent<MaterialModifierRTChain>();
        if (chain == null)
            chain = targetImage.gameObject.AddComponent<MaterialModifierRTChain>();
        chain.Setup(targetImage, card);
    }
}
