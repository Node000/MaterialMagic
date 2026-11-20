using System.Collections.Generic;
using TMPro;
using UnityEngine;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SecondFloorStripDungeonTestUI")]
#endif
public class SecondFloorStripDungeonTestUI : MonoBehaviour
{
    [SerializeField] private StripDungeonMapConfig config;
    [SerializeField] private int nextSeed = 1;
    [SerializeField] private SecondFloorMinimapGraphic minimap;
    [SerializeField] private TMP_Text partText;
    [SerializeField] private TMP_Text logText;
    [SerializeField, Min(1)] private int logLineLimit = 10;
    private readonly Queue<string> lines = new Queue<string>();
    private SecondFloorMinimapSession session;

    private void Start() => GenerateMap();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            unchecked { nextSeed++; }
            GenerateMap();
            return;
        }
        if (session == null) return;
        if (Input.GetKeyDown(KeyCode.W)) Apply(MinimapAction.Forward);
        else if (Input.GetKeyDown(KeyCode.S)) Apply(MinimapAction.Backward);
        else if (Input.GetKeyDown(KeyCode.A)) Apply(MinimapAction.TurnLeft);
        else if (Input.GetKeyDown(KeyCode.D)) Apply(MinimapAction.TurnRight);
    }

    private void GenerateMap()
    {
        if (!SecondFloorMinimapGenerator.TryGenerate(config, nextSeed, out var map, out var error))
        {
            AddLog($"Seed {nextSeed} 生成失败：{error}");
            return;
        }
        session = new SecondFloorMinimapSession(map);
        lines.Clear();
        Refresh();
        AddLog($"Seed {map.Seed} 已生成，从下部 A 区进入。");
    }

    private void Apply(MinimapAction action)
    {
        var step = session.Execute(action);
        string move = action == MinimapAction.Backward ? "后退" : "前进";
        switch (step.Outcome)
        {
            case MinimapOutcome.Blocked:
                AddLog($"{move}受阻，位置不变。");
                return;
            case MinimapOutcome.Turned:
                AddLog($"{(action == MinimapAction.TurnLeft ? "左转" : "右转")}，朝{FacingName()}。");
                break;
            case MinimapOutcome.Teleported:
                var destination = session.CurrentBoard.GetTile(session.Position);
                AddLog($"进入 {step.EnteredTile.Area} 区传送房 → {PartName()} {destination.Area} 区 ({session.Position.x}, {session.Position.y})。");
                break;
            case MinimapOutcome.Moved:
                var tile = step.EnteredTile;
                var location = tile.Kind == MinimapTileKind.Connector ? $"{tile.Area} 连接条带" : $"{tile.Area} 区";
                var room = tile.Kind == MinimapTileKind.BossRoom ? "Boss 房" : tile.RoomType.HasValue ? $"{LevelTypeText.GetName(tile.RoomType.Value)}房" : null;
                AddLog(room == null ? $"{move}到 {location} ({tile.Position.x}, {tile.Position.y})。" : $"{move}进入 {location} {room}，仅记录。");
                break;
        }
        Refresh();
    }

    private void Refresh()
    {
        minimap.Refresh(session);
        partText.text = $"二层 · {PartName()}    朝{FacingName()}    Seed {session.Map.Seed}";
    }

    private string PartName() => session.Part == SecondFloorPart.Lower ? "下部" : "上部";
    private string FacingName() => session.Facing switch
    {
        SecondFloorFacing.North => "北",
        SecondFloorFacing.East => "东",
        SecondFloorFacing.South => "南",
        _ => "西"
    };

    private void AddLog(string message)
    {
        lines.Enqueue(message);
        while (lines.Count > Mathf.Max(1, logLineLimit)) lines.Dequeue();
        logText.text = string.Join("\n", lines);
    }
}
