using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 为 UI/ProceduralCrayon_Edge 提供图片坐标和外扩网格。
/// 支持 Image(Simple, Use Sprite Mesh 关闭) 和 RawImage。
/// 放在同物体其他网格效果之前；不与 Outline/Shadow 等效果组合使用。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
[AddComponentMenu("UI/Effects/Crayon UI Edge")]
public sealed class CrayonUIEdge : BaseMeshEffect
{
    [SerializeField, Min(1f)]
    [Tooltip("向四周增加的绘制空间，单位与 RectTransform 相同。应大于材质 Edge Tips Length 至少 1；不足时 Shader 会限制线头长度。")]
    private float padding = 12f;

    private readonly UIVertex[] corners = new UIVertex[4];
    private bool reportedUnsupported;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureCanvasChannels();
        if (graphic != null) graphic.SetVerticesDirty();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnsureCanvasChannels();
        if (graphic != null) graphic.SetVerticesDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        padding = Mathf.Max(1f, padding);
        reportedUnsupported = false;
        base.OnValidate();
        EnsureCanvasChannels();
        if (graphic != null) graphic.SetVerticesDirty();
    }
#endif

    private void EnsureCanvasChannels()
    {
        if (!isActiveAndEnabled || graphic == null || graphic.canvas == null) return;
        graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                                                | AdditionalCanvasShaderChannels.TexCoord2
                                                | AdditionalCanvasShaderChannels.TexCoord3;
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var image = graphic as Image;
        var rawImage = graphic as RawImage;
        bool supported = image != null || rawImage != null;
        if (image != null)
        {
            var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
            supported &= image.type == Image.Type.Simple && !image.useSpriteMesh;
            if (sprite != null && sprite.packed)
                supported &= sprite.packingMode == SpritePackingMode.Rectangle
                          && sprite.packingRotation == SpritePackingRotation.None;
        }
        if (!supported || vh.currentVertCount != 4)
        {
            if (!reportedUnsupported)
            {
                Debug.LogWarning("CrayonUIEdge 需要 Simple Image（关闭 Use Sprite Mesh）或 RawImage 的四顶点网格；图集请关闭 Tight Packing 与 Allow Rotation。请移除同物体的其他网格效果。", this);
                reportedUnsupported = true;
            }
            return;
        }
        reportedUnsupported = false;
        EnsureCanvasChannels();

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        for (int n = 0; n < 4; n++)
        {
            vh.PopulateUIVertex(ref corners[n], n);
            var p = corners[n].position;
            minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
        }
        float width = maxX - minX, height = maxY - minY;
        if (width <= 0.001f || height <= 0.001f) return;

        Vector2 uvBottomLeft = Vector2.zero, uvTopRight = Vector2.one;
        for (int n = 0; n < 4; n++)
        {
            var v = corners[n];
            if (Mathf.Abs(v.position.x - minX) < 0.001f && Mathf.Abs(v.position.y - minY) < 0.001f)
                uvBottomLeft = new Vector2(v.uv0.x, v.uv0.y);
            if (Mathf.Abs(v.position.x - maxX) < 0.001f && Mathf.Abs(v.position.y - maxY) < 0.001f)
                uvTopRight = new Vector2(v.uv0.x, v.uv0.y);
        }
        float pad = Mathf.Max(1f, padding);
        for (int n = 0; n < 4; n++)
        {
            var v = corners[n];
            float sideX = (v.position.x - minX) / width;
            float sideY = (v.position.y - minY) / height;
            v.position.x += (sideX * 2f - 1f) * pad;
            v.position.y += (sideY * 2f - 1f) * pad;
            float u = (v.position.x - minX) / width;
            float t = (v.position.y - minY) / height;
            v.uv0 = new Vector4(Mathf.LerpUnclamped(uvBottomLeft.x, uvTopRight.x, u),
                               Mathf.LerpUnclamped(uvBottomLeft.y, uvTopRight.y, t), 0f, 0f);
            v.uv1 = new Vector4(u, t, width, height);
            v.uv2 = new Vector4(uvBottomLeft.x, uvBottomLeft.y, uvTopRight.x, uvTopRight.y);
            v.uv3 = new Vector4(pad, 0f, 0f, 0f);
            vh.SetUIVertex(v, n);
        }
    }
}
