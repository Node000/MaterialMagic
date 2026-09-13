using System;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class JpegGlitchSprite : MonoBehaviour
{
    public enum GlitchMode { Coefficients, RawByteDamage }
    [Tooltip("Copy the input as image.jpg.bytes or image.png.bytes; use bytes, never TextAsset.text.")]
    public TextAsset jpegBytes;
    public GlitchMode mode = GlitchMode.Coefficients;
    public JpegGlitch.Settings settings = new JpegGlitch.Settings();
    [Range(1, 12)] public int byteEdits = 2;
    [Range(0f, 0.99f)] public float byteStart = 0.3f;
    [Range(0.001f, 1f)] public float byteSpan = 0.08f;
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Header("PNG input")]
    [Range(1, 100)] public int pngJpegQuality = 80;
    [Tooltip("Keep the original PNG alpha separately and restore it after JPEG decoding. The JPEG file itself has no alpha.")]
    public bool preservePngTransparency = true;
    [Tooltip("Opaque background used when Preserve Png Transparency is off. This color's alpha is ignored.")]
    public Color pngBackground = Color.white;
    [Header("Animation")]
    [Tooltip("Generate a new JPEG glitch repeatedly while the game runs.")]
    public bool animate = true;
    [Range(1f, 30f)] public float updatesPerSecond = 6f;
    [Tooltip("Continue refreshing when Time.timeScale is zero. Editor Pause still pauses execution.")]
    public bool useUnscaledTime = true;
    [Header("Diagnostics")]
    [Tooltip("Show the decoded texture and status in Game view, independently of scene cameras.")]
    public bool showGamePreview = true;
    [SerializeField, TextArea(2, 5)] private string runtimeStatus = "Not started. Enter Play Mode and enable this object and component.";

    private SpriteRenderer target;
    private Texture2D ownedTexture;
    private Sprite ownedSprite;
    private Sprite initialSprite;
    private byte[] acceptedJpeg;
    private bool lastWasGlitch;
    private bool displayedRestoredAlpha;
    private GUIStyle statusStyle;
    private float frameCountdown;
    private float nextAutoWarningTime;

    // Cache immutable input and PNG conversion, not generated glitch frames.
    private TextAsset cachedAsset;
    private byte[] cachedSource;
    private bool cachedIsPng;
    private byte[] cachedPngJpeg;
    private byte[] cachedPngAlpha;
    private int cachedPngWidth, cachedPngHeight, cachedPngQuality;
    private bool cachedPreserveAlpha;
    private Color cachedBackground;

    private void Awake()
    {
        target = GetComponent<SpriteRenderer>();
        initialSprite = target.sprite;
        runtimeStatus = "Awake ran. Waiting for Start.";
    }
    private void Start() { Generate(); }

    private float FrameInterval()
    {
        float rate = updatesPerSecond;
        if (float.IsNaN(rate) || float.IsInfinity(rate)) rate = 6f;
        return 1f / Mathf.Clamp(rate, 1f, 30f);
    }

    private void Update()
    {
        if (!animate || jpegBytes == null)
        {
            frameCountdown = FrameInterval();
            return;
        }
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta <= 0f) return;
        frameCountdown -= delta;
        if (frameCountdown > 0f) return;
        // At most one attempt per Update. Never queue catch-up work after a slow frame.
        frameCountdown = FrameInterval();
        if (settings == null) settings = new JpegGlitch.Settings();
        unchecked { settings.seed++; }
        LoadAndDisplay(true, false);
    }

    [ContextMenu("Pause Animation")]
    public void PauseAnimation()
    {
        animate = false;
        frameCountdown = FrameInterval();
    }

    [ContextMenu("Resume Animation")]
    public void ResumeAnimation()
    {
        animate = true;
        frameCountdown = 0f;
    }

    [ContextMenu("Generate JPEG Glitch")]
    public void Generate()
    {
        frameCountdown = FrameInterval();
        LoadAndDisplay(true);
    }

    [ContextMenu("Show Original Image (diagnostic)")]
    public void ShowOriginal()
    {
        PauseAnimation();
        LoadAndDisplay(false);
    }

    private void LoadAndDisplay(bool applyGlitch, bool logSuccess = true)
    {
        if (!Application.isPlaying)
        {
            runtimeStatus = "Not running. Enter Play Mode first.";
            Debug.LogWarning("JPEG display: " + runtimeStatus, this); return;
        }
        if (jpegBytes == null)
        {
            runtimeStatus = "No input. Assign photo.jpg.bytes or photo.png.bytes to Jpeg Bytes, then Generate.";
            Debug.LogError("JPEG display: " + runtimeStatus, this); return;
        }
        Texture2D candidate = null;
        Sprite next = null;
        try
        {
            if (target == null)
            {
                target = GetComponent<SpriteRenderer>();
                if (target == null) throw new InvalidOperationException("SpriteRenderer is missing.");
                initialSprite = target.sprite;
            }
            if (settings == null) settings = new JpegGlitch.Settings();
            runtimeStatus = "Reading input bytes.";
            bool isPng;
            byte[] source = GetSource(out isPng);
            bool isJpeg = !isPng;
            byte[] pngAlpha = null;
            int pngWidth = 0, pngHeight = 0;
            byte[] modified = source;
            if (applyGlitch)
            {
                if (isPng)
                {
                    runtimeStatus = "Converting PNG to JPEG.";
                    modified = GetPreparedPng(source, out pngAlpha, out pngWidth, out pngHeight);
                }
                runtimeStatus = "Editing JPEG: " + mode + ".";
                modified = mode == GlitchMode.Coefficients
                    ? JpegGlitch.EditCoefficients(modified, settings)
                    : JpegGlitch.BendBytes(modified, settings.seed, byteEdits, byteStart, byteSpan);
            }
            runtimeStatus = "Decoding image with Unity LoadImage.";
            candidate = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!ImageConversion.LoadImage(candidate, modified, false))
                throw new InvalidDataException("Unity rejected this image. Test Show Original Image; for byte damage, try fewer edits.");
            if (pngAlpha != null)
            {
                runtimeStatus = "Restoring the original PNG transparency.";
                Texture2D rgba = RestoreAlpha(candidate, pngAlpha, pngWidth, pngHeight);
                Destroy(candidate);
                candidate = rgba;
            }
            candidate.filterMode = FilterMode.Bilinear;
            candidate.wrapMode = TextureWrapMode.Clamp;
            runtimeStatus = "Creating Sprite.";
            next = Sprite.Create(candidate,
                new Rect(0, 0, candidate.width, candidate.height),
                new Vector2(0.5f, 0.5f), Mathf.Max(1f, pixelsPerUnit), 0, SpriteMeshType.FullRect);
            if (next == null) throw new InvalidOperationException("Sprite.Create returned null.");
            next.name = (applyGlitch ? "JPEG Glitch " : (isPng ? "Original PNG " : "Original JPEG ")) + candidate.width + "x" + candidate.height;
            candidate.name = next.name + " Texture";
            target.sprite = next;
            if (ownedSprite != null) Destroy(ownedSprite);
            if (ownedTexture != null) Destroy(ownedTexture);
            ownedSprite = next; ownedTexture = candidate;
            // Original PNG preview is not a JPEG and must not be saved with a .jpg extension.
            acceptedJpeg = applyGlitch || isJpeg ? modified : null;
            lastWasGlitch = applyGlitch;
            displayedRestoredAlpha = pngAlpha != null;
            runtimeStatus = "OK: " + next.name + ". Sprite assigned. " +
                (applyGlitch ? (isPng ? "PNG -> modified JPEG." : "Modified JPEG displayed.") : "Original only; glitch is OFF.") +
                (displayedRestoredAlpha ? " Original PNG alpha restored." : "");
            if (logSuccess) Debug.Log("JPEG display: " + runtimeStatus, this);
        }
        catch (Exception e)
        {
            if (next != null) Destroy(next);
            if (candidate != null) Destroy(candidate);
            runtimeStatus = "FAILED at " + runtimeStatus + " " + e.Message;
            // Raw corruption can fail for some seeds. Retain the previous frame
            // and limit automatic warning logs; manual actions still report immediately.
            if (logSuccess || Time.unscaledTime >= nextAutoWarningTime)
            {
                nextAutoWarningTime = Time.unscaledTime + 3f;
                Debug.LogWarning("JPEG display: " + runtimeStatus + " Previous sprite retained.", this);
            }
        }
    }

    private byte[] GetSource(out bool isPng)
    {
        if (cachedAsset != jpegBytes || cachedSource == null)
        {
            byte[] source = jpegBytes.bytes;
            bool png = IsPng(source);
            bool jpeg = source.Length >= 4 && source[0] == 0xFF && source[1] == 0xD8;
            if (!png && !jpeg) throw new InvalidDataException("The input must contain JPG or PNG file bytes.");
            cachedAsset = jpegBytes; cachedSource = source; cachedIsPng = png;
            cachedPngJpeg = null; cachedPngAlpha = null;
        }
        isPng = cachedIsPng;
        return cachedSource;
    }

    private byte[] GetPreparedPng(byte[] source, out byte[] alpha, out int width, out int height)
    {
        int quality = Mathf.Clamp(pngJpegQuality, 1, 100);
        if (cachedPngJpeg == null || cachedPngQuality != quality ||
            cachedPreserveAlpha != preservePngTransparency || cachedBackground != pngBackground)
        {
            byte[] converted = PngToJpeg(source, out alpha, out width, out height);
            cachedPngJpeg = converted; cachedPngAlpha = alpha;
            cachedPngWidth = width; cachedPngHeight = height;
            cachedPngQuality = quality; cachedPreserveAlpha = preservePngTransparency;
            cachedBackground = pngBackground;
        }
        alpha = cachedPngAlpha; width = cachedPngWidth; height = cachedPngHeight;
        return cachedPngJpeg;
    }

    [ContextMenu("Reload Source Image")]
    public void ReloadSource()
    {
        cachedAsset = null; cachedSource = null;
        cachedPngJpeg = null; cachedPngAlpha = null;
        Generate();
    }

    private static bool IsPng(byte[] data)
    {
        return data.Length >= 8 && data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71 &&
            data[4] == 13 && data[5] == 10 && data[6] == 26 && data[7] == 10;
    }

    private byte[] PngToJpeg(byte[] bytes, out byte[] alpha, out int width, out int height)
    {
        alpha = null; width = height = 0;
        Texture2D png = null, rgb = null;
        try
        {
            png = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(png, bytes, false)) throw new InvalidDataException("Unity could not decode the PNG.");
            width = png.width; height = png.height;
            if ((long)width * height > 16000000) throw new InvalidDataException("PNG is above this example's 16-megapixel limit.");
            Color32[] pixels = png.GetPixels32();
            if (preservePngTransparency)
            {
                // Avoid allocating an alpha buffer for an entirely opaque PNG.
                bool transparent = false;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a != 255) { transparent = true; break; }
                if (transparent)
                {
                    alpha = new byte[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
                }
            }
            Color32 background = (Color32)pngBackground;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (!preservePngTransparency)
                {
                    int a = p.a, inverse = 255 - a;
                    p.r = (byte)((p.r * a + background.r * inverse + 127) / 255);
                    p.g = (byte)((p.g * a + background.g * inverse + 127) / 255);
                    p.b = (byte)((p.b * a + background.b * inverse + 127) / 255);
                }
                p.a = 255; pixels[i] = p;
            }
            rgb = new Texture2D(width, height, TextureFormat.RGB24, false);
            rgb.SetPixels32(pixels); rgb.Apply(false, false);
            byte[] jpeg = ImageConversion.EncodeToJPG(rgb, Mathf.Clamp(pngJpegQuality, 1, 100));
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
                throw new InvalidDataException("Unity could not encode the PNG as JPEG.");
            return jpeg;
        }
        finally
        {
            if (png != null) Destroy(png);
            if (rgb != null) Destroy(rgb);
        }
    }

    private static Texture2D RestoreAlpha(Texture2D rgb, byte[] alpha, int width, int height)
    {
        if (rgb.width != width || rgb.height != height || alpha.Length != width * height)
            throw new InvalidDataException("JPEG dimensions changed; the original PNG alpha no longer matches.");
        Color32[] pixels = rgb.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i]; p.a = alpha[i]; pixels[i] = p;
        }
        Texture2D rgba = null;
        try
        {
            rgba = new Texture2D(width, height, TextureFormat.RGBA32, false);
            rgba.SetPixels32(pixels); rgba.Apply(false, false);
            return rgba;
        }
        catch
        {
            if (rgba != null) Destroy(rgba);
            throw;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showGamePreview || Screen.width < 100 || Screen.height < 160) return;
        if (statusStyle == null) { statusStyle = new GUIStyle(GUI.skin.label); statusStyle.wordWrap = true; }
        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.color = Color.white;
        GUI.depth = -10000;
        float width = Mathf.Min(460f, Screen.width - 24f);
        float height = Mathf.Min(420f, Screen.height - 24f);
        GUI.Box(new Rect(12, 12, width, height), GUIContent.none);
        GUI.Label(new Rect(22, 20, width - 20, 22), "JPEG preview - independent of scene camera");
        GUI.Label(new Rect(22, 47, width - 20, 86), runtimeStatus, statusStyle);
        if (ownedTexture != null && height > 146)
            GUI.DrawTexture(new Rect(22, 138, width - 20, height - 136), ownedTexture, ScaleMode.ScaleToFit, true);
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    [ContextMenu("Next Seed")]
    public void NextSeed()
    {
        if (settings == null) settings = new JpegGlitch.Settings();
        unchecked { settings.seed++; }
        Generate();
    }

    [ContextMenu("Save Current Modified JPG")]
    public void SaveCurrentJpg()
    {
        if (acceptedJpeg == null) { Debug.LogWarning("Generate a JPEG effect first. For an original PNG preview, use Save Current Display As PNG.", this); return; }
        try
        {
            string path = Path.Combine(Application.persistentDataPath,
                (lastWasGlitch ? "jpeg-glitch-" : "jpeg-original-") + Guid.NewGuid().ToString("N") + ".jpg");
            // Save the edited compressed bytes directly. Do not EncodeToJPG again.
            File.WriteAllBytes(path, acceptedJpeg);
            Debug.Log("Saved JPG: " + path + (displayedRestoredAlpha ? " (JPEG has no alpha. Use Save Current Display As PNG to retain transparency.)" : ""), this);
        }
        catch (Exception e) { Debug.LogWarning("Could not save JPG: " + e.Message, this); }
    }

    [ContextMenu("Save Current Display As PNG")]
    public void SaveCurrentPng()
    {
        if (ownedTexture == null) { Debug.LogWarning("Generate an image first.", this); return; }
        try
        {
            byte[] png = ImageConversion.EncodeToPNG(ownedTexture);
            if (png == null || png.Length == 0) throw new InvalidDataException("PNG encoding failed.");
            string path = Path.Combine(Application.persistentDataPath, "jpeg-glitch-display-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, png);
            Debug.Log("Saved displayed image as PNG: " + path, this);
        }
        catch (Exception e) { Debug.LogWarning("Could not save PNG: " + e.Message, this); }
    }

    private void OnDestroy()
    {
        if (target != null && target.sprite == ownedSprite) target.sprite = initialSprite;
        if (ownedSprite != null) Destroy(ownedSprite);
        if (ownedTexture != null) Destroy(ownedTexture);
    }
}
