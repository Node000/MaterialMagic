using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Preview driver: a generated floor and an explicitly selected snapshot use the same exploration and views.</summary>
public sealed class SecondFloorFloorPreview : MonoBehaviour
{
    private enum MapSource { Snapshot, GenerateFromPatterns, PublishedPool }

    [Serializable]
    private struct RegionSlot
    {
        public string regionTypeId;
        [Range(3, 20)] public int stripCount;
        [Min(0)] public int maxNoLoop;
    }

    [Header("地图输入")]
    [SerializeField] private MapSource mapSource;
    [SerializeField] private TextAsset floorFile;
    [SerializeField] private TextAsset patternLibrary;
    [SerializeField] private TextAsset mapPool;
    [SerializeField] private string poolId;
    [SerializeField] private string entryId;
    [SerializeField] private int startingSeed = 1;
    [SerializeField] private RegionSlot[] regionSlots;
    [SerializeField, Min(1)] private int baseLevel = 1;
    [SerializeField, Min(0)] private float levelPerHop = .75f;
    [SerializeField, Min(1)] private int maxLevel = 20;
    [Header("场景引用")]
    [SerializeField] private Button nextSeedButton;
    [SerializeField] private TMP_Text seedLabel;
    [SerializeField] private SecondFloorFloorGeometry geometry;
    [SerializeField] private SecondFloorFloorMinimapView minimap;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField, Min(0.01f)] private float cellWorldSize = 3f;
    [SerializeField, Min(0.01f)] private float playerEyeHeight = 0.8f;

    private FirstPersonGridExplorationSession exploration;
    private readonly HashSet<Vector2Int> discovered = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> visible = new List<Vector2Int>();
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private PointerEventData pointer;
    private int currentSeed;

    private void Awake()
    {
        pointer = new PointerEventData(eventSystem);
        currentSeed = startingSeed;
        nextSeedButton.interactable = CanGenerateSeed;
        ReloadMap();
    }

    public void ReloadMap()
    {
        SecondFloorFloorMap map;
        switch (mapSource)
        {
            case MapSource.Snapshot:
                map = SecondFloorFloorMap.Read(floorFile);
                seedLabel.text = map.Id;
                break;
            case MapSource.GenerateFromPatterns:
                var result = SecondFloorPatternGenerator.Generate(SecondFloorPatternLibrary.Read(patternLibrary),
                    regionSlots.Select(slot => new SecondFloorPatternGenerator.SlotSettings(slot.regionTypeId, slot.stripCount, slot.maxNoLoop)).ToArray(),
                    currentSeed, baseLevel, levelPerHop, maxLevel);
                map = result.Map;
                seedLabel.text = $"Seed {currentSeed}  /  R {result.Resource:0.##}  /  D {result.Difficulty:0.##}";
                break;
            case MapSource.PublishedPool:
                if (!mapPool) throw new InvalidOperationException("Assign a published map pool TextAsset.");
                var generated = SecondFloorMapPools.Read(mapPool.text).Generate(poolId, entryId, currentSeed);
                map = generated.Map;
                string budgetStatus = generated.WithinBudget ? "" : "  /  Budget missed";
                seedLabel.text = $"Seed {currentSeed}  /  R {generated.Resource:0.##}  /  D {generated.Difficulty:0.##}{budgetStatus}";
                break;
            default: throw new InvalidOperationException($"Invalid second-floor map source: {mapSource}.");
        }
        var next = new FirstPersonGridExplorationSession(map.Spawn.x, map.Spawn.y,
            (x, y) => map.HasTile(new Vector2Int(x, y)),
            (from, to) => map.CanStep(new Vector2Int(from.X, from.Y), new Vector2Int(to.X, to.Y)));
        geometry.Rebuild(map, cellWorldSize);
        minimap.Rebuild(map);
        exploration = next;
        discovered.Clear();
        RefreshViews();
    }

    private bool CanGenerateSeed => mapSource == MapSource.GenerateFromPatterns || mapSource == MapSource.PublishedPool;

    public void GenerateNextSeed()
    {
        if (!CanGenerateSeed) throw new InvalidOperationException("Seed generation requires a generated map source.");
        currentSeed = checked(currentSeed + 1);
        ReloadMap();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Turn(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Turn(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveForward();
        else if (TryGetWorldPress(out var screenX))
        {
            if (screenX < Screen.width / 3f) Turn(-1);
            else if (screenX > Screen.width * 2f / 3f) Turn(1);
            else MoveForward();
        }
    }

    private void Turn(int quarterTurns)
    {
        exploration.Turn(quarterTurns);
        RefreshViews();
    }

    private bool MoveForward()
    {
        if (!exploration.MoveForward()) return false;
        RefreshViews();
        return true;
    }

    private void RefreshViews()
    {
        var cell = new Vector2Int(exploration.X, exploration.Y);
        var direction = new Vector2Int(exploration.FacingX, exploration.FacingY);
        playerRoot.SetPositionAndRotation(
            new Vector3(cell.x * cellWorldSize, playerEyeHeight, cell.y * cellWorldSize),
            Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y)));

        visible.Clear();
        var ray = exploration.VisiblePositions;
        for (int i = 0; i < ray.Count; i++)
        {
            var point = new Vector2Int(ray[i].X, ray[i].Y);
            visible.Add(point);
            discovered.Add(point);
        }
        minimap.Refresh(cell, direction, discovered, visible);
    }

    private bool TryGetWorldPress(out float screenX)
    {
        screenX = 0f;
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began || IsOverUi(touch.position, touch.fingerId)) return false;
            screenX = touch.position.x;
            return true;
        }
        if (!Input.GetMouseButtonDown(0) || IsOverUi(Input.mousePosition, -1)) return false;
        screenX = Input.mousePosition.x;
        return true;
    }

    private bool IsOverUi(Vector2 position, int pointerId)
    {
        // Query the actual press, independent of whether EventSystem.Update has run this frame.
        pointer.Reset();
        pointer.pointerId = pointerId;
        pointer.position = position;
        eventSystem.RaycastAll(pointer, pointerHits);
        foreach (var hit in pointerHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }
}
