using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the fixed floor and exploration state supplied by the scene controller.</summary>
[DisallowMultipleComponent]
public sealed class SecondFloorFloorMinimapView : MonoBehaviour
{
    [SerializeField] private SecondFloorMinimapGraphic graphic;
    [SerializeField, Tooltip("Upright player Image, a direct child of the map graphic with centered anchors and pivot.")]
    private Image playerMarker;
    [SerializeField] private bool showAll;

    private SecondFloorMinimapGraphic Graphic => graphic != null ? graphic :
        throw new InvalidOperationException($"{nameof(SecondFloorFloorMinimapView)} on {name} requires an authored minimap graphic.");

    public void Rebuild(SecondFloorFloorMap map)
    {
        var mapGraphic = Graphic;
        if (playerMarker == null || playerMarker.rectTransform.parent != mapGraphic.rectTransform)
            throw new InvalidOperationException($"{nameof(SecondFloorFloorMinimapView)} on {name} requires an authored player Image directly under the minimap graphic.");
        mapGraphic.RebuildFloor(map, showAll, playerMarker);
    }

    public void Refresh(Vector2Int playerCell, Vector2Int facingDirection,
        IReadOnlyCollection<Vector2Int> discovered, IReadOnlyCollection<Vector2Int> visible) =>
        Graphic.RefreshFloor(playerCell, facingDirection, discovered, visible);

    /// <summary>Rotates the map presentation without changing floor visibility or the player marker.</summary>
    public void SetVisualHeading(float clockwiseDegrees) => Graphic.SetFloorVisualHeading(clockwiseDegrees);

    public void SetShowAll(bool value)
    {
        Graphic.SetShowFloorAll(value);
        showAll = value;
    }
}
