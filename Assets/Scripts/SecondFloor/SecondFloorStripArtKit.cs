using System;
using UnityEngine;

/// <summary>Artist-authored assets in cell/world units. All references are explicit.</summary>
[CreateAssetMenu(menuName = "Config/Second Floor Strip Art Kit")]
public sealed class SecondFloorStripArtKit : ScriptableObject
{
    [SerializeField] private string visualTypeId = "default";
    [Header("地面 · 以单格中心为原点，+Y 朝上")]
    [SerializeField] private GameObject[] floorPatches;
    [Header("墙壁 · XY 平面，+Z 朝向走廊，笔触范围横向居中")]
    [SerializeField] private ScribblePlane3D[] wallBands;
    [SerializeField] private GameObject wallAccent;
    [SerializeField, Min(1)] private int accentMinWallCells = 2;
    [Header("装饰 · 以占地背沿中心为原点，+Z 朝向走廊")]
    [SerializeField] private Decoration[] decorations;

    public string VisualTypeId => visualTypeId;
    public GameObject[] FloorPatches => floorPatches;
    public ScribblePlane3D[] WallBands => wallBands;
    public GameObject WallAccent => wallAccent;
    public int AccentMinWallCells => accentMinWallCells;
    public Decoration[] Decorations => decorations;

    [Serializable]
    public sealed class Decoration
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 size;
        [SerializeField] private bool preferAtSpawn;
        public GameObject Prefab => prefab;
        public Vector3 Size => size;
        public bool PreferAtSpawn => preferAtSpawn;
    }
}
