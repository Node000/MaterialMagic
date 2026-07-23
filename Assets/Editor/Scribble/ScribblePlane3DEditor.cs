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
            EditorGUILayout.HelpBox("Each direct child GameObject is a guide point. Their Hierarchy order defines the stroke route. Move, duplicate, delete, or reorder these children with normal Unity tools.", MessageType.Info);
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

        if (GUILayout.Button("Initialize Contour From Fill Area"))
        {
            Undo.RecordObject(scribblePlane, "Initialize Scribble Contour");
            scribblePlane.InitializeContourFromFillArea();
            EditorUtility.SetDirty(scribblePlane);
        }
    }

    private void OnSceneGUI()
    {
        if (scribblePlane == null || scribblePlane.ControlPointCount == 0)
            return;

        Transform transform = scribblePlane.transform;
        Vector3 planeNormal = transform.TransformDirection(scribblePlane.GetLocalPlaneNormal()).normalized;
        Vector3 firstAxis = transform.right.normalized;
        Vector3 secondAxis = transform.TransformDirection(scribblePlane.GetLocalPlaneSecondAxis()).normalized;

        DrawContour(transform);
        if (scribblePlane.CurrentFillMode == ScribblePlane3D.FillMode.EdgeGuidedStrokes)
            DrawGuidePoints(transform);

        for (int index = 0; index < scribblePlane.ControlPointCount; index++)
        {
            Vector3 worldPoint = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetControlPoint(index)));
            float handleSize = HandleUtility.GetHandleSize(worldPoint) * 0.075f;

            EditorGUI.BeginChangeCheck();
            Vector3 movedWorldPoint = Handles.Slider2D(
                worldPoint,
                planeNormal,
                firstAxis,
                secondAxis,
                handleSize,
                Handles.DotHandleCap,
                Vector2.zero);
            if (!EditorGUI.EndChangeCheck())
                continue;

            Undo.RecordObject(scribblePlane, "Move Scribble Control Point");
            Vector3 movedLocalPoint = transform.InverseTransformPoint(movedWorldPoint);
            scribblePlane.SetControlPoint(index, scribblePlane.ToPlanePoint(movedLocalPoint));
            EditorUtility.SetDirty(scribblePlane);
        }
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

    private void DrawContour(Transform transform)
    {
        int pointCount = scribblePlane.ControlPointCount;
        if (pointCount < 2)
            return;

        Handles.color = new Color(1f, 0.78f, 0.22f, 0.95f);
        for (int index = 0; index < pointCount - 1; index++)
        {
            Vector3 from = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetControlPoint(index)));
            Vector3 to = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetControlPoint(index + 1)));
            Handles.DrawLine(from, to, 2f);
        }

        if (scribblePlane.ClosedContour && pointCount > 2)
        {
            Vector3 from = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetControlPoint(pointCount - 1)));
            Vector3 to = transform.TransformPoint(scribblePlane.ToLocalPoint(scribblePlane.GetControlPoint(0)));
            Handles.DrawLine(from, to, 2f);
        }
    }
}
