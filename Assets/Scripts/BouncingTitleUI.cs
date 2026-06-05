using UnityEngine;

[DisallowMultipleComponent]
public class BouncingTitleUI : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 220f;
    [SerializeField] private Vector2 direction = new Vector2(1f, -1f);
    [SerializeField] private RectTransform boundary;
    [SerializeField] private bool useUnscaledTime = true;

    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform rectTransform;
    private Vector2 velocity;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        if (boundary == null)
            boundary = rectTransform.parent as RectTransform;
        ResetVelocity();
        KeepInsideBounds();
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
        if (boundary == null)
            boundary = rectTransform.parent as RectTransform;
        ResetVelocity();
    }

    private void Update()
    {
        if (boundary == null || speed <= 0f)
            return;

        Vector2 normalizedVelocity = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : NormalizedDirection();
        velocity = normalizedVelocity * speed;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        rectTransform.localPosition += (Vector3)(velocity * deltaTime);
        KeepInsideBounds();
    }

    private void ResetVelocity()
    {
        velocity = NormalizedDirection() * speed;
    }

    private Vector2 NormalizedDirection()
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return new Vector2(1f, -1f).normalized;
        return direction.normalized;
    }

    private void KeepInsideBounds()
    {
        Rect boundaryRect = boundary.rect;
        rectTransform.GetWorldCorners(corners);

        Vector3 firstCorner = boundary.InverseTransformPoint(corners[0]);
        float minX = firstCorner.x;
        float maxX = firstCorner.x;
        float minY = firstCorner.y;
        float maxY = firstCorner.y;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 corner = boundary.InverseTransformPoint(corners[i]);
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
        }

        float offsetX = 0f;
        float offsetY = 0f;

        if (minX < boundaryRect.xMin)
        {
            offsetX = boundaryRect.xMin - minX;
            velocity.x = Mathf.Abs(velocity.x);
        }
        else if (maxX > boundaryRect.xMax)
        {
            offsetX = boundaryRect.xMax - maxX;
            velocity.x = -Mathf.Abs(velocity.x);
        }

        if (minY < boundaryRect.yMin)
        {
            offsetY = boundaryRect.yMin - minY;
            velocity.y = Mathf.Abs(velocity.y);
        }
        else if (maxY > boundaryRect.yMax)
        {
            offsetY = boundaryRect.yMax - maxY;
            velocity.y = -Mathf.Abs(velocity.y);
        }

        if (offsetX != 0f || offsetY != 0f)
            rectTransform.position += boundary.TransformVector(new Vector3(offsetX, offsetY, 0f));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        if (direction.sqrMagnitude <= 0.0001f)
            direction = new Vector2(1f, -1f);
    }
#endif
}
