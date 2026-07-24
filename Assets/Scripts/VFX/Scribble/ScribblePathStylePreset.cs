using UnityEngine;

[CreateAssetMenu(menuName = "Scribble/Path Style Preset", fileName = "ScribblePathStylePreset")]
public class ScribblePathStylePreset : ScriptableObject
{
    [SerializeField] private ScribblePathStyleSettings settings = ScribblePathStyleSettings.CreateDefault();

    public void CaptureFrom(ScribblePathPlane3D plane)
    {
        settings = plane.GetStyleSettings();
    }

    public void ApplyTo(ScribblePathPlane3D plane)
    {
        plane.ApplyStyleSettings(settings);
    }
}

[System.Serializable]
public struct ScribblePathStyleSettings
{
    [Range(1, 64)] public int strokeCountMin;
    [Range(1, 64)] public int strokeCountMax;
    [Range(2, 64)] public int samplesPerSegment;
    [Min(0.001f)] public float strokeWidth;
    [Min(0f)] public float segmentVariation;
    [Min(0f)] public float pathPointOffsetRange;
    public Color strokeColor;
    public ScribblePathPlane3D.DensityEdge densityEdge;
    public AnimationCurve densityOpacityOverDistance;
    public bool frameEnabled;
    [Min(0.001f)] public float frameWidth;
    public Color frameColor;
    public Material appearanceMaterial;
    public int seed;

    public static ScribblePathStyleSettings CreateDefault()
    {
        return new ScribblePathStyleSettings
        {
            strokeCountMin = 5,
            strokeCountMax = 9,
            samplesPerSegment = 12,
            strokeWidth = 0.035f,
            segmentVariation = 0.12f,
            pathPointOffsetRange = 0.1f,
            strokeColor = new Color(1f, 0.38f, 0.16f, 0.75f),
            densityEdge = ScribblePathPlane3D.DensityEdge.Left,
            densityOpacityOverDistance = AnimationCurve.Linear(0f, 1f, 1f, 0.3f),
            frameEnabled = true,
            frameWidth = 0.055f,
            frameColor = new Color(1f, 0.86f, 0.48f, 1f),
            seed = 17
        };
    }
}
