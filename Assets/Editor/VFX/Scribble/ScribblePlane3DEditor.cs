using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScribblePlane3D))]
public class ScribblePlane3DEditor : Editor
{
    private ScribblePlane3D scribblePlane;

    private void OnEnable()
    {
        scribblePlane = (ScribblePlane3D)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            scribblePlane.RebuildMesh();
            EditorUtility.SetDirty(scribblePlane);
        }

        if (scribblePlane.CurrentFillMode == ScribblePlane3D.FillMode.EdgeGuidedStrokes)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Each direct child GameObject is an independent bypass center. Its strokes leave and return to the selected edge, passing around that point once. Move, duplicate, or delete children with normal Unity tools; sibling order does not matter.", MessageType.Info);
            if (GUILayout.Button("Add Guide Point"))
            {
                Transform guidePoint = scribblePlane.CreateGuidePoint();
                Undo.RegisterCreatedObjectUndo(guidePoint.gameObject, "Add Scribble Guide Point");
                EditorUtility.SetDirty(scribblePlane);
                Selection.activeGameObject = guidePoint.gameObject;
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild Preview"))
            scribblePlane.RebuildMesh();

    }

    private void OnSceneGUI()
    {
        if (scribblePlane == null)
            return;

        Transform transform = scribblePlane.transform;
        DrawFillArea(transform);
        if (scribblePlane.CurrentFillMode == ScribblePlane3D.FillMode.EdgeGuidedStrokes)
            DrawGuidePoints(transform);
    }

    private void DrawGuidePoints(Transform transform)
    {
        int pointCount = scribblePlane.GuidePointCount;
        if (pointCount == 0)
            return;

        Handles.color = new Color(0.28f, 0.94f, 1f, 0.95f);
        for (int index = 0; index < pointCount; index++)
        {
            Vector3 point = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetGuidePoint(index)));
            float handleSize = HandleUtility.GetHandleSize(point) * 0.065f;
            Handles.SphereHandleCap(0, point, Quaternion.identity, handleSize, EventType.Repaint);
            Handles.Label(point, "  Bypass " + (index + 1));
        }
    }

    private void DrawFillArea(Transform transform)
    {
        Rect area = scribblePlane.FillArea;
        Vector2[] corners =
        {
            new Vector2(area.xMin, area.yMin),
            new Vector2(area.xMax, area.yMin),
            new Vector2(area.xMax, area.yMax),
            new Vector2(area.xMin, area.yMax)
        };
        Vector3[] worldCorners = new Vector3[corners.Length];
        for (int index = 0; index < corners.Length; index++)
            worldCorners[index] = transform.TransformPoint(scribblePlane.ToLocalPoint(corners[index]));

        Color previousColor = Handles.color;
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.color = new Color(0.28f, 0.94f, 1f, 0.95f);
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        for (int index = 0; index < worldCorners.Length; index++)
            Handles.DrawLine(worldCorners[index], worldCorners[(index + 1) % worldCorners.Length], 2f);
        Handles.Label(worldCorners[0], "  Fill Area");

        Vector3 planeNormal = transform.TransformDirection(scribblePlane.GetLocalPlaneNormal()).normalized;
        Vector3 firstAxis = transform.right.normalized;
        Vector3 secondAxis = transform.TransformDirection(scribblePlane.GetLocalPlaneSecondAxis()).normalized;
        for (int index = 0; index < worldCorners.Length; index++)
        {
            float handleSize = HandleUtility.GetHandleSize(worldCorners[index]) * 0.075f;
            EditorGUI.BeginChangeCheck();
            Vector3 movedWorldPoint = Handles.Slider2D(
                worldCorners[index],
                planeNormal,
                firstAxis,
                secondAxis,
                handleSize,
                Handles.RectangleHandleCap,
                Vector2.zero);
            if (!EditorGUI.EndChangeCheck())
                continue;

            Vector2 movedPoint = scribblePlane.ToPlanePoint(transform.InverseTransformPoint(movedWorldPoint));
            float xMin = area.xMin;
            float xMax = area.xMax;
            float yMin = area.yMin;
            float yMax = area.yMax;
            switch (index)
            {
                case 0:
                    xMin = movedPoint.x;
                    yMin = movedPoint.y;
                    break;
                case 1:
                    xMax = movedPoint.x;
                    yMin = movedPoint.y;
                    break;
                case 2:
                    xMax = movedPoint.x;
                    yMax = movedPoint.y;
                    break;
                default:
                    xMin = movedPoint.x;
                    yMax = movedPoint.y;
                    break;
            }

            Undo.RecordObject(scribblePlane, "Resize Scribble Fill Area");
            scribblePlane.SetFillArea(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
            EditorUtility.SetDirty(scribblePlane);
        }

        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }
}

[InitializeOnLoad]
internal static class ScribbleRibbonScenePreview
{
    private const string RibbonShaderName = "Style/Scribble/Ribbon3D";
    private static readonly int PreviewAnimationTimeId = Shader.PropertyToID("_PreviewAnimationTime");
    private static readonly MaterialPropertyBlock PreviewProperties = new MaterialPropertyBlock();
    private static double nextRepaintTime;

    static ScribbleRibbonScenePreview()
    {
        EditorApplication.update += RepaintSceneViewWhenNeeded;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void RepaintSceneViewWhenNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        double time = EditorApplication.timeSinceStartup;
        if (time < nextRepaintTime)
            return;

        float framesPerSecond = GetPreviewFramesPerSecond(time);
        if (framesPerSecond <= 0f)
            return;

        nextRepaintTime = time + 1d / framesPerSecond;
        SceneView.RepaintAll();
    }

    private static float GetPreviewFramesPerSecond(double previewTime)
    {
        ScribblePlane3D[] planes = Object.FindObjectsOfType<ScribblePlane3D>();
        float framesPerSecond = 0f;
        for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
        {
            MeshRenderer renderer = planes[planeIndex].GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || material.shader == null || material.shader.name != RibbonShaderName)
                    continue;

                bool isAnimating = material.GetFloat("_WobbleAmplitude") > 0f && material.GetFloat("_WobbleSpeed") > 0f;
                SetPreviewTime(renderer, materialIndex, isAnimating ? (float)previewTime : -1f);
                if (!isAnimating)
                    continue;

                float materialFramesPerSecond = material.GetFloat("_SteppedAnimation") > 0.5f
                    ? material.GetFloat("_AnimationFramesPerSecond")
                    : 30f;
                framesPerSecond = Mathf.Max(framesPerSecond, materialFramesPerSecond);
            }
        }

        return framesPerSecond;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            ClearPreviewTimes();
    }

    private static void ClearPreviewTimes()
    {
        ScribblePlane3D[] planes = Object.FindObjectsOfType<ScribblePlane3D>();
        for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
        {
            MeshRenderer renderer = planes[planeIndex].GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.shader != null && material.shader.name == RibbonShaderName)
                    SetPreviewTime(renderer, materialIndex, -1f);
            }
        }
    }

    private static void SetPreviewTime(MeshRenderer renderer, int materialIndex, float previewTime)
    {
        renderer.GetPropertyBlock(PreviewProperties, materialIndex);
        PreviewProperties.SetFloat(PreviewAnimationTimeId, previewTime);
        renderer.SetPropertyBlock(PreviewProperties, materialIndex);
        PreviewProperties.Clear();
    }
}
