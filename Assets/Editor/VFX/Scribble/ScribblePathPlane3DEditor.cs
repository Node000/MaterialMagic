using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScribblePathPlane3D))]
public class ScribblePathPlane3DEditor : Editor
{
    private ScribblePathPlane3D pathPlane;
    private ScribblePathStylePreset stylePreset;
    private Material legacyMaterial;

    private void OnEnable()
    {
        pathPlane = (ScribblePathPlane3D)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            pathPlane.RebuildMesh();
            EditorUtility.SetDirty(pathPlane);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Points", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Direct children are traversed in Hierarchy order. Move, duplicate, delete, or reorder them with normal Unity tools. Path Point Offset Range controls each stroke's stable random offset around every child; Segment Variation bends only between those offset points. Animation FPS is set on the assigned path material.", MessageType.Info);
        if (GUILayout.Button("Add Path Point"))
        {
            Transform pathPoint = pathPlane.CreatePathPoint();
            Undo.RegisterCreatedObjectUndo(pathPoint.gameObject, "Add Scribble Path Point");
            EditorUtility.SetDirty(pathPlane);
            Selection.activeGameObject = pathPoint.gameObject;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Style Preset", EditorStyles.boldLabel);
        stylePreset = (ScribblePathStylePreset)EditorGUILayout.ObjectField("Preset", stylePreset, typeof(ScribblePathStylePreset), false);
        using (new EditorGUI.DisabledScope(stylePreset == null))
        {
            if (GUILayout.Button("Apply Preset"))
            {
                Undo.RecordObject(pathPlane, "Apply Scribble Path Style Preset");
                stylePreset.ApplyTo(pathPlane);
                EditorUtility.SetDirty(pathPlane);
            }
            if (GUILayout.Button("Capture To Preset"))
            {
                Undo.RecordObject(stylePreset, "Capture Scribble Path Style Preset");
                stylePreset.CaptureFrom(pathPlane);
                EditorUtility.SetDirty(stylePreset);
                AssetDatabase.SaveAssets();
            }
        }
        if (GUILayout.Button("Create Style Preset From Current"))
            CreateStylePreset();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Legacy Appearance Migration", EditorStyles.boldLabel);
        legacyMaterial = (Material)EditorGUILayout.ObjectField("Legacy Material", legacyMaterial, typeof(Material), false);
        using (new EditorGUI.DisabledScope(legacyMaterial == null))
        {
            if (GUILayout.Button("Create Path Material From Legacy"))
                CreatePathMaterialFromLegacy();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild Preview"))
            pathPlane.RebuildMesh();
    }

    private void OnSceneGUI()
    {
        if (pathPlane == null)
            return;

        Transform transform = pathPlane.transform;
        DrawPathPoints(transform);
        DrawFrame(transform);
    }

    private void DrawPathPoints(Transform transform)
    {
        Handles.color = new Color(0.28f, 0.94f, 1f, 0.95f);
        for (int index = 0; index < pathPlane.PathPointCount; index++)
        {
            Vector3 point = transform.TransformPoint(pathPlane.ToLocalPoint(pathPlane.GetPathPoint(index)));
            float handleSize = HandleUtility.GetHandleSize(point) * 0.06f;
            Handles.SphereHandleCap(0, point, Quaternion.identity, handleSize, EventType.Repaint);
            Handles.Label(point, "  Path " + (index + 1));
            if (index > 0)
            {
                Vector3 previousPoint = transform.TransformPoint(pathPlane.ToLocalPoint(pathPlane.GetPathPoint(index - 1)));
                Handles.DrawDottedLine(previousPoint, point, 3f);
            }
        }
    }

    private void DrawFrame(Transform transform)
    {
        Color previousColor = Handles.color;
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = pathPlane.FrameEnabled
            ? new Color(1f, 0.78f, 0.22f, 0.95f)
            : new Color(1f, 0.78f, 0.22f, 0.35f);

        Vector3[] worldCorners = new Vector3[4];
        for (int index = 0; index < worldCorners.Length; index++)
            worldCorners[index] = transform.TransformPoint(pathPlane.ToLocalPoint(pathPlane.GetFrameCorner(index)));

        for (int index = 0; index < worldCorners.Length; index++)
            Handles.DrawLine(worldCorners[index], worldCorners[(index + 1) % worldCorners.Length], 2f);
        Handles.Label(worldCorners[0], pathPlane.FrameEnabled ? "  Frame" : "  Frame (Hidden)");

        Vector3 planeNormal = transform.TransformDirection(pathPlane.GetLocalPlaneNormal()).normalized;
        Vector3 firstAxis = transform.right.normalized;
        Vector3 secondAxis = transform.TransformDirection(pathPlane.GetLocalPlaneSecondAxis()).normalized;
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

            Undo.RecordObject(pathPlane, "Move Scribble Path Frame Corner");
            pathPlane.SetFrameCorner(index, pathPlane.ToPlanePoint(transform.InverseTransformPoint(movedWorldPoint)));
            EditorUtility.SetDirty(pathPlane);
        }

        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }

    private void CreateStylePreset()
    {
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Create Scribble Path Style Preset",
            "ScribblePathStylePreset",
            "asset",
            "Choose where to save the reusable path style preset.");
        if (string.IsNullOrEmpty(assetPath))
            return;

        ScribblePathStylePreset preset = CreateInstance<ScribblePathStylePreset>();
        preset.CaptureFrom(pathPlane);
        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();
        stylePreset = preset;
        Selection.activeObject = preset;
    }

    private void CreatePathMaterialFromLegacy()
    {
        Shader pathShader = Shader.Find("Style/Scribble/PathRibbon3D");
        if (pathShader == null)
        {
            EditorUtility.DisplayDialog("Path Shader Missing", "Style/Scribble/PathRibbon3D could not be found.", "OK");
            return;
        }

        string defaultName = legacyMaterial.name + " Path";
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Create Path Scribble Material",
            defaultName,
            "mat",
            "Choose where to save the new material. The source material is not modified.");
        if (string.IsNullOrEmpty(assetPath))
            return;

        Material pathMaterial = new Material(pathShader)
        {
            name = Path.GetFileNameWithoutExtension(assetPath)
        };
        CopySharedAppearance(legacyMaterial, pathMaterial);
        AssetDatabase.CreateAsset(pathMaterial, assetPath);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(pathPlane, "Assign Path Scribble Material");
        pathPlane.SetAppearanceMaterial(pathMaterial);
        EditorUtility.SetDirty(pathPlane);
        Selection.activeObject = pathMaterial;
    }

    private static void CopySharedAppearance(Material source, Material destination)
    {
        CopyColor(source, destination, "_Tint");
        CopyTexture(source, destination, "_BrushTex");
        CopyTexture(source, destination, "_BreakupTex");
        CopyVector(source, destination, "_BrushTiling");
        CopyFloat(source, destination, "_BreakupStrength");
        CopyFloat(source, destination, "_BreakupThreshold");
        CopyFloat(source, destination, "_WobbleAmplitude");
        CopyFloat(source, destination, "_WobbleFrequency");
        CopyFloat(source, destination, "_WobbleSpeed");
        CopyFloat(source, destination, "_SteppedAnimation");
        CopyFloat(source, destination, "_AnimationFramesPerSecond");
        CopyFloat(source, destination, "_SecondaryWobble");
        CopyFloat(source, destination, "_Seed");
        CopyFloat(source, destination, "_Opacity");
        CopyFloat(source, destination, "_DepthOffset");
    }

    private static void CopyColor(Material source, Material destination, string propertyName)
    {
        if (source.HasProperty(propertyName) && destination.HasProperty(propertyName))
            destination.SetColor(propertyName, source.GetColor(propertyName));
    }

    private static void CopyTexture(Material source, Material destination, string propertyName)
    {
        if (!source.HasProperty(propertyName) || !destination.HasProperty(propertyName))
            return;

        destination.SetTexture(propertyName, source.GetTexture(propertyName));
        destination.SetTextureScale(propertyName, source.GetTextureScale(propertyName));
        destination.SetTextureOffset(propertyName, source.GetTextureOffset(propertyName));
    }

    private static void CopyVector(Material source, Material destination, string propertyName)
    {
        if (source.HasProperty(propertyName) && destination.HasProperty(propertyName))
            destination.SetVector(propertyName, source.GetVector(propertyName));
    }

    private static void CopyFloat(Material source, Material destination, string propertyName)
    {
        if (source.HasProperty(propertyName) && destination.HasProperty(propertyName))
            destination.SetFloat(propertyName, source.GetFloat(propertyName));
    }
}

[InitializeOnLoad]
internal static class ScribblePathRibbonScenePreview
{
    private const string PathShaderName = "Style/Scribble/PathRibbon3D";
    private static readonly int PreviewAnimationTimeId = Shader.PropertyToID("_PreviewAnimationTime");
    private static readonly MaterialPropertyBlock PreviewProperties = new MaterialPropertyBlock();
    private static double nextRepaintTime;

    static ScribblePathRibbonScenePreview()
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

        float framesPerSecond = SetPreviewTimes((float)time);
        if (framesPerSecond <= 0f)
            return;

        nextRepaintTime = time + 1d / framesPerSecond;
        SceneView.RepaintAll();
    }

    private static float SetPreviewTimes(float previewTime)
    {
        ScribblePathPlane3D[] planes = Object.FindObjectsOfType<ScribblePathPlane3D>();
        float framesPerSecond = 0f;
        for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
        {
            MeshRenderer renderer = planes[planeIndex].GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            Material material = renderer.sharedMaterial;
            if (material == null || material.shader == null || material.shader.name != PathShaderName)
                continue;

            bool isAnimating = material.GetFloat("_WobbleAmplitude") > 0f && material.GetFloat("_WobbleSpeed") > 0f;
            renderer.GetPropertyBlock(PreviewProperties);
            PreviewProperties.SetFloat(PreviewAnimationTimeId, isAnimating ? previewTime : -1f);
            renderer.SetPropertyBlock(PreviewProperties);
            PreviewProperties.Clear();
            if (!isAnimating)
                continue;

            framesPerSecond = Mathf.Max(framesPerSecond, material.GetFloat("_SteppedAnimation") > 0.5f
                ? material.GetFloat("_AnimationFramesPerSecond")
                : 30f);
        }
        return framesPerSecond;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredPlayMode)
            return;

        ScribblePathPlane3D[] planes = Object.FindObjectsOfType<ScribblePathPlane3D>();
        for (int index = 0; index < planes.Length; index++)
        {
            MeshRenderer renderer = planes[index].GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(PreviewProperties);
            PreviewProperties.SetFloat(PreviewAnimationTimeId, -1f);
            renderer.SetPropertyBlock(PreviewProperties);
            PreviewProperties.Clear();
        }
    }
}
