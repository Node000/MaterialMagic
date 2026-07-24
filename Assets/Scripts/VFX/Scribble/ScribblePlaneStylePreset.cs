using UnityEngine;

[CreateAssetMenu(menuName = "Scribble/Plane Style Preset", fileName = "ScribblePlaneStylePreset")]
public class ScribblePlaneStylePreset : ScriptableObject
{
    [SerializeField] private ScribblePlaneStyleSettings settings = ScribblePlaneStyleSettings.CreateDefault();

    public void CaptureFrom(ScribblePlane3D plane)
    {
        settings = plane.GetStyleSettings();
    }

    public void ApplyTo(ScribblePlane3D plane)
    {
        plane.ApplyStyleSettings(settings);
    }
}

[System.Serializable]
public struct ScribblePlaneStyleSettings
{
    public bool fillEnabled;
    public ScribblePlane3D.FillMode fillMode;
    [Range(1, 96)] public int fillLineCount;
    [Range(2, 128)] public int fillSamplesPerLine;
    [Min(0.001f)] public float fillLineWidth;
    [Min(0f)] public float fillInset;
    [Range(-90f, 90f)] public float fillAngleDegrees;
    [Min(0f)] public float fillWobbleAmplitude;
    [Min(0f)] public float fillWobbleFrequency;
    public Color fillVertexColor;
    public ScribblePlane3D.FillAreaEdge guidedEdge;
    [Min(0f)] public float guidedStartOffsetRange;
    [Min(0f)] public float guidedEndOffsetRange;
    [Min(0f)] public float guidedEdgeJitter;
    [Range(1, 16)] public int guidedStrokesPerPointMin;
    [Range(1, 16)] public int guidedStrokesPerPointMax;
    [Min(0.01f)] public float guidedBypassOffset;
    [Min(0f)] public float guidedBypassOffsetRange;
    [Range(0.1f, 2f)] public float guidedBypassAspect;
    public AnimationCurve guidedWobbleAmplitudeOverPath;
    public AnimationCurve guidedWobbleFrequencyOverPath;
    public ScribblePlane3D.GuideCurveDirection guidedCurveDirection;
    public Material appearanceMaterial;
    public int seed;

    public static ScribblePlaneStyleSettings CreateDefault()
    {
        return new ScribblePlaneStyleSettings
        {
            fillEnabled = true,
            fillMode = ScribblePlane3D.FillMode.ParallelScan,
            fillLineCount = 18,
            fillSamplesPerLine = 24,
            fillLineWidth = 0.035f,
            fillInset = 0.05f,
            fillAngleDegrees = 0f,
            fillWobbleAmplitude = 0.06f,
            fillWobbleFrequency = 3.5f,
            fillVertexColor = new Color(1f, 1f, 1f, 0.55f),
            guidedEdge = ScribblePlane3D.FillAreaEdge.Left,
            guidedStartOffsetRange = 0.35f,
            guidedEndOffsetRange = 0.35f,
            guidedEdgeJitter = 0.035f,
            guidedStrokesPerPointMin = 2,
            guidedStrokesPerPointMax = 5,
            guidedBypassOffset = 0.85f,
            guidedBypassOffsetRange = 0.18f,
            guidedBypassAspect = 0.7f,
            guidedWobbleAmplitudeOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f),
            guidedWobbleFrequencyOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f),
            guidedCurveDirection = ScribblePlane3D.GuideCurveDirection.StartToEnd,
            seed = 17
        };
    }
}
