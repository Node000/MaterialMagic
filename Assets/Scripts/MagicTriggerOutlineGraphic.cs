using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>A padded UGUI quad that samples just the source sprite's silhouette.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MagicTriggerOutlineGraphic : MaskableGraphic
{
    private Sprite sprite;
    private Rect spriteRect;
    private float thickness;

    public override Texture mainTexture => sprite != null ? sprite.texture : base.mainTexture;

    public bool Configure(Image source, float width)
    {
        sprite = source.overrideSprite;
        if (sprite == null || (sprite.packed && (sprite.packingMode != SpritePackingMode.Rectangle || sprite.packingRotation != SpritePackingRotation.None)))
            return false;

        // Match the drawing dimensions of the Simple Image, including transparent sprite padding.
        Vector2 spriteSize = sprite.rect.size;
        Rect rect = source.GetPixelAdjustedRect();
        if (spriteSize.x <= 0f || spriteSize.y <= 0f || rect.width <= 0f || rect.height <= 0f)
            return false;
        if (source.preserveAspect)
        {
            float aspect = spriteSize.x / spriteSize.y;
            if (aspect > rect.width / rect.height)
            {
                float height = rect.width / aspect;
                rect.y += (rect.height - height) * source.rectTransform.pivot.y;
                rect.height = height;
            }
            else
            {
                float fittedWidth = rect.height * aspect;
                rect.x += (rect.width - fittedWidth) * source.rectTransform.pivot.x;
                rect.width = fittedWidth;
            }
        }
        Vector4 padding = DataUtility.GetPadding(sprite);
        spriteRect = Rect.MinMaxRect(rect.xMin + padding.x / spriteSize.x * rect.width,
            rect.yMin + padding.y / spriteSize.y * rect.height,
            rect.xMax - padding.z / spriteSize.x * rect.width,
            rect.yMax - padding.w / spriteSize.y * rect.height);
        thickness = Mathf.Max(0.1f, width);
        EnsureCanvasChannel();
        SetAllDirty();
        return true;
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnsureCanvasChannel();
    }

    private void EnsureCanvasChannel()
    {
        if (canvas != null)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    protected override void UpdateMaterial()
    {
        base.UpdateMaterial();
        if (IsActive())
            canvasRenderer.SetAlphaTexture(sprite != null ? sprite.associatedAlphaSplitTexture : null);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (sprite == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            return;

        Vector4 bounds = DataUtility.GetOuterUV(sprite);
        var uvScale = new Vector2((bounds.z - bounds.x) / spriteRect.width, (bounds.w - bounds.y) / spriteRect.height);
        float pad = thickness + 1f;
        AddCorner(mesh, spriteRect.xMin - pad, spriteRect.yMin - pad, bounds, uvScale);
        AddCorner(mesh, spriteRect.xMin - pad, spriteRect.yMax + pad, bounds, uvScale);
        AddCorner(mesh, spriteRect.xMax + pad, spriteRect.yMax + pad, bounds, uvScale);
        AddCorner(mesh, spriteRect.xMax + pad, spriteRect.yMin - pad, bounds, uvScale);
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }

    private void AddCorner(VertexHelper mesh, float x, float y, Vector4 bounds, Vector2 uvScale)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector3(x, y, 0f);
        vertex.uv0 = new Vector4(bounds.x + (x - spriteRect.xMin) * uvScale.x,
            bounds.y + (y - spriteRect.yMin) * uvScale.y, thickness * uvScale.x, thickness * uvScale.y);
        vertex.uv1 = bounds;
        mesh.AddVert(vertex);
    }
}
