using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScribbleTileGrid3D))]
public class ScribbleTileGrid3DEditor : Editor
{
    private ScribbleTileGrid3D tileGrid;

    private void OnEnable()
    {
        tileGrid = (ScribbleTileGrid3D)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            tileGrid.RebuildMesh();
            EditorUtility.SetDirty(tileGrid);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("All generated tiles are combined into one MeshRenderer. The layout is stable for a given Seed; changing it produces another arrangement. Tile Chance Over Rows maps from the layout's bottom row (0) to top row (1).", MessageType.Info);
        EditorGUILayout.LabelField("Generated Tiles", tileGrid.GeneratedTileCount.ToString());
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Randomize Seed"))
            {
                Undo.RecordObject(tileGrid, "Randomize Scribble Tile Grid");
                tileGrid.RandomizeSeed();
                EditorUtility.SetDirty(tileGrid);
            }

            if (GUILayout.Button("Rebuild Preview"))
                tileGrid.RebuildMesh();
        }
    }

    private void OnSceneGUI()
    {
        if (tileGrid == null)
            return;

        Rect area = tileGrid.LayoutArea;
        Transform transform = tileGrid.transform;
        Vector3[] corners =
        {
            transform.TransformPoint(new Vector3(area.xMin, 0f, area.yMin)),
            transform.TransformPoint(new Vector3(area.xMax, 0f, area.yMin)),
            transform.TransformPoint(new Vector3(area.xMax, 0f, area.yMax)),
            transform.TransformPoint(new Vector3(area.xMin, 0f, area.yMax))
        };

        Color previousColor = Handles.color;
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.color = new Color(0.32f, 0.95f, 0.7f, 0.95f);
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        for (int index = 0; index < corners.Length; index++)
            Handles.DrawLine(corners[index], corners[(index + 1) % corners.Length], 2f);
        Handles.Label(corners[0], "  Tile Layout");
        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }
}
