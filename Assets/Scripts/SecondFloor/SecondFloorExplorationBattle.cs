using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns the preview floor's resolved encounters and the exploration-to-battle handoff.</summary>
[DisallowMultipleComponent]
public sealed class SecondFloorExplorationBattle : MonoBehaviour
{
    [SerializeField] private HandSystemUI battleUi;
    [SerializeField] private SecondFloorEncounterMarker markerPrefab;
    [SerializeField] private Transform markerRoot;
    [SerializeField] private Camera dungeonCamera;
    [SerializeField] private Transform battleCameraPose;
    [SerializeField] private GameObject battleStage;
    [SerializeField, Range(10f, 120f)] private float battleFieldOfView = 51f;
    [SerializeField, Min(.05f)] private float enterSeconds = .43f;
    [SerializeField, Min(.05f)] private float returnSeconds = .32f;
    [SerializeField] private AnimationCurve shotEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector3 arcOffset = new Vector3(.45f, .04f, -.64f);
    [SerializeField, Range(0f, 1f)] private float hudRevealAt = .26f;
    [SerializeField, Min(.05f)] private float hudEnterSeconds = .42f;
    [SerializeField] private SecondFloorFloorGeometry explorationGeometry;
    [SerializeField] private Transform explorationArt;
    [SerializeField] private SecondFloorBattleHudMotion hudMotion;
    [SerializeField] private GameObject[] explorationOnlyUi;
    [SerializeField] private GameObject minimapPanel;

    private sealed class Entry
    {
        public SecondFloorEncounter Encounter;
        public SecondFloorEncounterMarker Marker;
    }

    private readonly Dictionary<Vector2Int, Entry> encounters = new Dictionary<Vector2Int, Entry>();
    private SecondFloorEncounterCatalog catalog;
    private Vector2Int? pending;
    private bool defeated;
    private bool minimapVisible = true;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private float returnFieldOfView;
    private float eyeHeight;
    private Vector3 stagingHomePosition;
    private Quaternion stagingHomeRotation;
    private Coroutine shot;
    private bool battlePresentationReady;

    public bool IsBusy => pending.HasValue || defeated || shot != null;
    public bool IsExplorationBlocked => IsBusy || (battleUi != null && battleUi.IsSecondFloorExplorationBlocked);
    public int RemainingEncounters => encounters.Count;

    private void Awake()
    {
        if (!battleUi || !markerPrefab || !markerRoot || !dungeonCamera || !battleCameraPose || !battleStage || !explorationGeometry || !hudMotion)
            throw new InvalidOperationException("Assign the second-floor battle UI, marker, stage, exploration geometry and HUD motion.");
        stagingHomePosition = battleStage.transform.position;
        stagingHomeRotation = battleStage.transform.rotation;
        SetExplorationUi(true);
        hudMotion.SetExploring();
    }

    private void Start() => battleUi.SetSecondFloorMapAction(ToggleMinimap);

    private void Update() => hudMotion.SetExplorationHoverBlocked(IsExplorationBlocked);

    public void Rebuild(SecondFloorFloorMap map, int seed, float cellSize, float playerEyeHeight = .8f)
    {
        if (IsBusy) throw new InvalidOperationException("Cannot replace the floor while an encounter is pending or the run has ended.");
        eyeHeight = playerEyeHeight;
        catalog ??= SecondFloorEncounterCatalog.LoadDefault();
        // Resolve before publishing any markers. Invalid content must not create a partially playable floor.
        var resolved = new List<(Vector2Int Position, SecondFloorEncounter Encounter)>();
        foreach (var tile in map.Tiles)
        {
            if (!catalog.TryResolve(seed, tile, out var encounter)) continue;
            if (tile.Position == map.Spawn)
                throw new InvalidOperationException($"Encounter at {tile.Position} overlaps the floor spawn.");
            resolved.Add((tile.Position, encounter));
        }
        foreach (var entry in encounters.Values)
        {
            entry.Marker.gameObject.SetActive(false);
            Destroy(entry.Marker.gameObject);
        }
        encounters.Clear();
        foreach (var item in resolved)
        {
            var marker = Instantiate(markerPrefab, markerRoot);
            marker.transform.position = new Vector3(item.Position.x * cellSize, 0f, item.Position.y * cellSize);
            marker.Bind(item.Encounter.DisplayEnemyId);
            encounters.Add(item.Position, new Entry { Encounter = item.Encounter, Marker = marker });
        }
    }

    public bool HasEncounter(Vector2Int cell) => encounters.ContainsKey(cell);

    public void FaceMarkers(Vector3 from)
    {
        foreach (var entry in encounters.Values)
            entry.Marker.Face(from);
    }

    public bool TryBegin(Vector2Int cell)
    {
        if (IsExplorationBlocked || !encounters.TryGetValue(cell, out var entry)) return false;
        var level = entry.Encounter.CreateBattleLevel();
        pending = cell;
        var cameraTransform = dungeonCamera.transform;
        returnPosition = cameraTransform.position;
        returnRotation = cameraTransform.rotation;
        returnFieldOfView = dungeonCamera.fieldOfView;
        battlePresentationReady = false;
        SetExplorationUi(false);
        hudMotion.PrepareBattle();
        if (battleUi.TryStartSecondFloorBattle(level, FinishEncounter, entry.Encounter.IsBoss, () => battlePresentationReady))
        {
            shot = StartCoroutine(PlayBattleShot(entering: true));
            return true;
        }
        pending = null;
        RestoreExploration();
        return false;
    }

    private void FinishEncounter(bool victory)
    {
        if (!pending.HasValue) return;
        var cell = pending.Value;
        pending = null;
        if (!victory)
        {
            defeated = true;
            return;
        }
        var marker = encounters[cell].Marker;
        encounters.Remove(cell);
        marker.gameObject.SetActive(false);
        Destroy(marker.gameObject);
        hudMotion.SetSuspended();
        shot = StartCoroutine(PlayBattleShot(entering: false));
    }

    private System.Collections.IEnumerator PlayBattleShot(bool entering)
    {
        if (entering)
        {
            // The prototype corridor is a cutaway set, not a path through the generated walls.
            // Match its origin to the approach cell so the camera travels a local arc, not to its storage position.
            var stage = battleStage.transform;
            stage.SetPositionAndRotation(returnPosition - Vector3.up * eyeHeight, returnRotation);
            SetDungeonVisible(false);
            battleStage.SetActive(true);
        }
        float duration = Mathf.Max(.05f, entering ? enterSeconds : returnSeconds);
        float elapsed = 0f;
        bool hudEntered = false;
        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float t = ApplyBattleShot(entering ? progress : 1f - progress);
            if (entering && !hudEntered && t >= hudRevealAt)
            {
                hudMotion.EnterBattle(hudEnterSeconds);
                hudEntered = true;
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        ApplyBattleShot(entering ? 1f : 0f);
        if (entering)
        {
            if (!hudEntered) hudMotion.EnterBattle(hudEnterSeconds);
            while (!hudMotion.IsBattleEntranceComplete) yield return null;
            battlePresentationReady = true;
        }
        else RestoreExploration();
        shot = null;
    }

    private float ApplyBattleShot(float time)
    {
        float t = time <= 0f ? 0f : time >= 1f ? 1f : Mathf.Clamp01(shotEase.Evaluate(time));
        if (t >= 1f)
        {
            dungeonCamera.transform.SetPositionAndRotation(battleCameraPose.position, battleCameraPose.rotation);
            dungeonCamera.fieldOfView = battleFieldOfView;
        }
        else if (t <= 0f)
        {
            ApplyExplorationCamera();
        }
        else
        {
            Vector3 position = Vector3.Lerp(returnPosition, battleCameraPose.position, t)
                + battleStage.transform.TransformVector(arcOffset) * (4f * t * (1f - t));
            dungeonCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(returnRotation, battleCameraPose.rotation, t));
            dungeonCamera.fieldOfView = Mathf.Lerp(returnFieldOfView, battleFieldOfView, t);
        }
        return t;
    }

    private void ApplyExplorationCamera()
    {
        dungeonCamera.transform.SetPositionAndRotation(returnPosition, returnRotation);
        dungeonCamera.fieldOfView = returnFieldOfView;
    }

    private void RestoreExploration()
    {
        ApplyExplorationCamera();
        battleStage.SetActive(false);
        battleStage.transform.SetPositionAndRotation(stagingHomePosition, stagingHomeRotation);
        SetDungeonVisible(true);
        SetExplorationUi(true);
        hudMotion.SetExploring();
    }

    private void SetDungeonVisible(bool visible)
    {
        explorationGeometry.SetExplorationVisible(visible);
        if (explorationArt) explorationArt.gameObject.SetActive(visible);
        markerRoot.gameObject.SetActive(visible);
    }

    private void SetExplorationUi(bool exploring)
    {
        foreach (var root in explorationOnlyUi)
            root.SetActive(exploring);
        if (minimapPanel) minimapPanel.SetActive(exploring && minimapVisible);
    }

    private void ToggleMinimap()
    {
        if (IsBusy || !minimapPanel) return;
        minimapVisible = !minimapVisible;
        minimapPanel.SetActive(minimapVisible);
    }
}
