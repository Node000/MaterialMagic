using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ModelEdgeScribble))]
public class ModelEdgeScribbleEditor : Editor
{
    private ModelEdgeScribble edgeScribble;

    private void OnEnable()
    {
        edgeScribble = (ModelEdgeScribble)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            edgeScribble.RebuildEdges();
            EditorUtility.SetDirty(edgeScribble);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Only boundary edges and edges whose adjoining faces exceed Crease Angle are generated. Coplanar triangle split edges are omitted. The source MeshRenderer is hidden when Hide Source Faces is enabled.", MessageType.Info);
        if (GUILayout.Button("Rebuild Edge Preview"))
            edgeScribble.RebuildEdges();
    }
}

[InitializeOnLoad]
internal static class ModelEdgeScribbleScenePreview
{
    private const string ShaderName = "Style/Scribble/ModelEdgeScribble";
    private static readonly int PreviewAnimationTimeId = Shader.PropertyToID("_PreviewAnimationTime");
    private static readonly MaterialPropertyBlock PreviewProperties = new MaterialPropertyBlock();
    private static double nextRepaintTime;

    static ModelEdgeScribbleScenePreview()
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
        MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
        float framesPerSecond = 0f;
        for (int index = 0; index < renderers.Length; index++)
        {
            MeshRenderer renderer = renderers[index];
            Material material = renderer.sharedMaterial;
            if (material == null || material.shader == null || material.shader.name != ShaderName)
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
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        SetPreviewTimes(-1f);
    }
}
