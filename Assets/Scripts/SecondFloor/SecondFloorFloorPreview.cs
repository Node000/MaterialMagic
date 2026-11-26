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
    [SerializeField] private SecondFloorExplorationBattle encounterFlow;
    [SerializeField, Min(0.01f)] private float cellWorldSize = 3f;
    [SerializeField, Min(0.01f)] private float playerEyeHeight = 0.8f;
    [Header("探索转向")]
    [SerializeField, Min(0f)] private float turnSeconds = 0.22f;

    private FirstPersonGridExplorationSession exploration;
    private SecondFloorFloorMap floor;
    private readonly HashSet<Vector2Int> discovered = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> visible = new List<Vector2Int>();
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private PointerEventData pointer;
    private int currentSeed;
    private int pendingQuarterTurn;
    private float turnElapsed;

    private void Awake()
    {
        pointer = new PointerEventData(eventSystem);
        currentSeed = startingSeed;
        if (nextSeedButton) nextSeedButton.interactable = CanGenerateSeed;
        ReloadMap();
    }

    public void ReloadMap()
    {
        if (pendingQuarterTurn != 0 || (encounterFlow != null && encounterFlow.IsBusy)) return;
        SecondFloorFloorMap map;
        switch (mapSource)
        {
            case MapSource.Snapshot:
                map = SecondFloorFloorMap.Read(floorFile);
                if (seedLabel) seedLabel.text = map.Id;
                break;
            case MapSource.GenerateFromPatterns:
                var result = SecondFloorPatternGenerator.Generate(SecondFloorPatternLibrary.Read(patternLibrary),
                    regionSlots.Select(slot => new SecondFloorPatternGenerator.SlotSettings(slot.regionTypeId, slot.stripCount, slot.maxNoLoop)).ToArray(),
                    currentSeed, baseLevel, levelPerHop, maxLevel);
                map = result.Map;
                if (seedLabel) seedLabel.text = $"Seed {currentSeed}  /  R {result.Resource:0.##}  /  D {result.Difficulty:0.##}";
                break;
            case MapSource.PublishedPool:
                if (!mapPool) throw new InvalidOperationException("Assign a published map pool TextAsset.");
                var generated = SecondFloorMapPools.Read(mapPool.text).Generate(poolId, entryId, currentSeed);
                map = generated.Map;
                string budgetStatus = generated.WithinBudget ? "" : "  /  Budget missed";
                if (seedLabel) seedLabel.text = $"Seed {currentSeed}  /  R {generated.Resource:0.##}  /  D {generated.Difficulty:0.##}{budgetStatus}";
                break;
            default: throw new InvalidOperationException($"Invalid second-floor map source: {mapSource}.");
        }
        var next = new FirstPersonGridExplorationSession(map.Spawn.x, map.Spawn.y,
            (x, y) => map.HasTile(new Vector2Int(x, y)),
            (from, to) => map.CanStep(new Vector2Int(from.X, from.Y), new Vector2Int(to.X, to.Y)));
        encounterFlow?.Rebuild(map, currentSeed, cellWorldSize, playerEyeHeight);
        floor = map;
        geometry.Rebuild(map, cellWorldSize);
        minimap.Rebuild(map);
        exploration = next;
        discovered.Clear();
        RefreshViews();
    }

    private bool CanGenerateSeed => mapSource == MapSource.GenerateFromPatterns || mapSource == MapSource.PublishedPool;

    public void GenerateNextSeed()
    {
        if (pendingQuarterTurn != 0 || (encounterFlow != null && encounterFlow.IsBusy)) return;
        if (!CanGenerateSeed) throw new InvalidOperationException("Seed generation requires a generated map source.");
        currentSeed = checked(currentSeed + 1);
        ReloadMap();
    }

    private void Update()
    {
        if (encounterFlow != null && encounterFlow.IsExplorationBlocked) return;
        if (pendingQuarterTurn != 0)
        {
            AdvanceTurn();
            return;
        }
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
        if (pendingQuarterTurn != 0 || (encounterFlow != null && encounterFlow.IsExplorationBlocked)) return;
        pendingQuarterTurn = quarterTurns;
        turnElapsed = 0f;
        if (turnSeconds <= 0f) CompleteTurn();
    }

    private void AdvanceTurn()
    {
        turnElapsed += Time.unscaledDeltaTime;
        if (turnElapsed >= turnSeconds)
        {
            CompleteTurn();
            return;
        }

        float remaining = 1f - turnElapsed / turnSeconds;
        float progress = 1f - remaining * remaining * remaining;
        float heading = (int)exploration.Facing * 90f + pendingQuarterTurn * 90f * progress;
        playerRoot.rotation = Quaternion.Euler(0f, heading, 0f);
        minimap.SetVisualHeading(heading);
    }

    private void CompleteTurn()
    {
        // Commit the rule-facing and reveal the next ray only after the visual turn arrives.
        exploration.Turn(pendingQuarterTurn);
        pendingQuarterTurn = 0;
        RefreshViews();
        TryEncounterAhead();
    }

    private bool MoveForward()
    {
        if (pendingQuarterTurn != 0 || (encounterFlow != null && (encounterFlow.IsExplorationBlocked || TryEncounterAhead()))) return false;
        if (!exploration.MoveForward()) return false;
        RefreshViews();
        TryEncounterAhead();
        return true;
    }

    // An encounter blocks its own tile. A turn or a move can bring the player into the approach pose.
    private bool TryEncounterAhead()
    {
        if (encounterFlow == null) return false;
        var from = new Vector2Int(exploration.X, exploration.Y);
        var to = from + new Vector2Int(exploration.FacingX, exploration.FacingY);
        if (!floor.CanStep(from, to) || !encounterFlow.HasEncounter(to)) return false;
        encounterFlow.TryBegin(to);
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
        encounterFlow?.FaceMarkers(playerRoot.position);
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
