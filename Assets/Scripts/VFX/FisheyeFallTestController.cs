using UnityEngine;

[DisallowMultipleComponent]
public sealed class FisheyeFallTestController : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private Material effectMaterial;
    [SerializeField, Min(0.01f)] private float intensityResponse = 3f;
    [SerializeField, Min(0.01f)] private float intensityReturn = 2f;
    [SerializeField, Range(0f, 1f)] private float idleIntensity;

    [Header("Test Track")]
    [SerializeField] private bool createTestTrack = true;
    [SerializeField, Min(1)] private int trackRows = 12;
    [SerializeField, Min(1f)] private float rowSpacing = 4f;
    [SerializeField, Min(0.5f)] private float rowWidth = 4f;
    [SerializeField, Min(0.05f)] private float markerThickness = 0.12f;
    [SerializeField] private Color markerColor = new Color(0.08f, 0.75f, 1f, 1f);
    [SerializeField] private Color accentColor = new Color(1f, 0.2f, 0.55f, 1f);

    private Material runtimeMaterial;
    private Transform trackRoot;
    private float intensity;

    private void Awake()
    {
        PrepareMaterial();
        if (createTestTrack)
            CreateTestTrack();
    }

    private void Update()
    {
        float target = Input.GetKey(KeyCode.Space) ? 1f : idleIntensity;
        float speed = target > intensity ? intensityResponse : intensityReturn;
        intensity = Mathf.MoveTowards(intensity, target, speed * Time.unscaledDeltaTime);
        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat("_Intensity", intensity);
    }

    private void OnDestroy()
    {
        if (FisheyeFallRendererFeature.RuntimeMaterial == runtimeMaterial)
            FisheyeFallRendererFeature.RuntimeMaterial = null;
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void PrepareMaterial()
    {
        if (effectMaterial == null)
            return;

        runtimeMaterial = new Material(effectMaterial);
        runtimeMaterial.SetFloat("_Intensity", idleIntensity);
        FisheyeFallRendererFeature.RuntimeMaterial = runtimeMaterial;
    }

    private void CreateTestTrack()
    {
        trackRoot = new GameObject("Runtime Test Track").transform;
        trackRoot.SetParent(transform, false);

        Material markerMaterial = CreateUnlitMaterial(markerColor);
        Material accentMaterial = CreateUnlitMaterial(accentColor);
        for (int i = 0; i < trackRows; i++)
        {
            float depth = (i + 1) * rowSpacing;
            CreateMarker("Left Edge", new Vector3(-rowWidth, 0f, depth), new Vector3(markerThickness, markerThickness, rowSpacing * 0.72f), markerMaterial);
            CreateMarker("Right Edge", new Vector3(rowWidth, 0f, depth), new Vector3(markerThickness, markerThickness, rowSpacing * 0.72f), markerMaterial);
            CreateMarker("Center Dash", new Vector3(0f, 0f, depth), new Vector3(markerThickness, markerThickness, rowSpacing * 0.42f), accentMaterial);
        }
    }

    private void CreateMarker(string markerName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = markerName;
        marker.transform.SetParent(trackRoot, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = localScale;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(marker.GetComponent<Collider>());
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}
