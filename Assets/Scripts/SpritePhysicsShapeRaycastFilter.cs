using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SpritePhysicsShapeRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField, Min(0f)] private float extraHitDistance = 0.08f;

    private readonly List<Vector2> points = new List<Vector2>(16);
    private Image image;
    private RectTransform rectTransform;

    private void Awake()
    {
        CacheReferences();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        CacheReferences();
        if (image == null || !image.raycastTarget)
            return true;

        return ContainsScreenPoint(screenPoint, eventCamera);
    }

    public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        CacheReferences();
        if (image == null || rectTransform == null || image.sprite == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            return false;

        return ContainsLocalPoint(localPoint);
    }

    public bool ContainsLocalPoint(Vector2 localPoint)
    {
        CacheReferences();
        Sprite sprite = image != null ? image.sprite : null;
        if (sprite == null)
            return false;

        Rect drawRect = GetDrawRect(rectTransform.rect, sprite);
        if (!drawRect.Contains(localPoint))
            return false;

        Vector2 spritePoint = ToSpritePoint(localPoint, drawRect, sprite);
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return true;

        for (int i = 0; i < shapeCount; i++)
        {
            points.Clear();
            sprite.GetPhysicsShape(i, points);
            if (ContainsPoint(points, spritePoint) || IsWithinExpandedEdge(points, spritePoint, extraHitDistance))
                return true;
        }

        return false;
    }

    private void CacheReferences()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
    }

    private Rect GetDrawRect(Rect rect, Sprite sprite)
    {
        if (image == null || !image.preserveAspect || sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f || rect.width <= 0f || rect.height <= 0f)
            return rect;

        float spriteRatio = sprite.rect.width / sprite.rect.height;
        float rectRatio = rect.width / rect.height;
        if (spriteRatio > rectRatio)
        {
            float height = rect.width / spriteRatio;
            float y = rect.y + (rect.height - height) * 0.5f;
            return new Rect(rect.x, y, rect.width, height);
        }

        float width = rect.height * spriteRatio;
        float x = rect.x + (rect.width - width) * 0.5f;
        return new Rect(x, rect.y, width, rect.height);
    }

    private static Vector2 ToSpritePoint(Vector2 localPoint, Rect drawRect, Sprite sprite)
    {
        Rect spriteRect = sprite.rect;
        Vector2 pivot = sprite.pivot;
        float pixelsPerUnit = sprite.pixelsPerUnit;
        float pixelX = Mathf.Lerp(0f, spriteRect.width, Mathf.InverseLerp(drawRect.xMin, drawRect.xMax, localPoint.x));
        float pixelY = Mathf.Lerp(0f, spriteRect.height, Mathf.InverseLerp(drawRect.yMin, drawRect.yMax, localPoint.y));
        return new Vector2((pixelX - pivot.x) / pixelsPerUnit, (pixelY - pivot.y) / pixelsPerUnit);
    }

    private static bool ContainsPoint(List<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if (((a.y > point.y) != (b.y > point.y)) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static bool IsWithinExpandedEdge(List<Vector2> polygon, Vector2 point, float extraDistance)
    {
        if (extraDistance <= 0f || polygon == null || polygon.Count == 0)
            return false;

        float extraDistanceSqr = extraDistance * extraDistance;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (DistanceToSegmentSqr(point, polygon[j], polygon[i]) <= extraDistanceSqr)
                return true;
        }

        return false;
    }

    private static float DistanceToSegmentSqr(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        if (lengthSqr <= Mathf.Epsilon)
            return (point - a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
        Vector2 closest = a + ab * t;
        return (point - closest).sqrMagnitude;
    }
}
