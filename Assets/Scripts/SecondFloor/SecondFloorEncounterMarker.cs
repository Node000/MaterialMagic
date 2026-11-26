using System;
using UnityEngine;

/// <summary>The single enemy representative a formation shows in the dungeon.</summary>
[DisallowMultipleComponent]
public sealed class SecondFloorEncounterMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField, Min(.01f)] private float height = 1.15f;

    public void Bind(int displayEnemyId)
    {
        if (body == null)
            throw new InvalidOperationException("The second-floor encounter marker requires a SpriteRenderer.");
        if (!GameDataDatabase.TryGetEnemyData(displayEnemyId, out var data))
            throw new InvalidOperationException($"Unknown second-floor display enemy {displayEnemyId}.");

        var sprite = EnemyVisualLoader.LoadStaticSprite(data.iconName);
        if (sprite == null || sprite.bounds.size.y <= 0f)
            throw new InvalidOperationException($"No usable sprite for second-floor display enemy {displayEnemyId}.");

        body.sprite = sprite;
        float scale = height / sprite.bounds.size.y;
        body.transform.localScale = Vector3.one * scale;
        body.transform.localPosition = new Vector3(-sprite.bounds.center.x * scale, -sprite.bounds.min.y * scale, 0f);
    }

    public void Face(Vector3 viewer)
    {
        var direction = transform.position - viewer;
        direction.y = 0f;
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
