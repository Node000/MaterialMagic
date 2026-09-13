using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class InfiniteUIWindow : MonoBehaviour
{
    private RawImage preview;
    private RenderTexture frameTexture;
    private Coroutine captureRoutine;

    private void OnEnable()
    {
        preview = GetComponent<RawImage>();
        preview.raycastTarget = true;
        preview.texture = Texture2D.blackTexture;

        captureRoutine = StartCoroutine(CaptureLoop());
    }

    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            // 等场景和 UI 全部绘制完成，包括这个小窗口。
            yield return endOfFrame;

            int width = Screen.width;
            int height = Screen.height;

            if (width <= 0 || height <= 0)
                continue;

            // 首次使用或游戏窗口尺寸变化时，创建贴图。
            if (frameTexture == null ||
                frameTexture.width != width ||
                frameTexture.height != height)
            {
                ReleaseTexture();

                frameTexture = new RenderTexture(
                    width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "UI Feedback",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                frameTexture.Create();
            }

            // 捕获本帧，下一帧由 RawImage 显示。
            ScreenCapture.CaptureScreenshotIntoRenderTexture(frameTexture);
            preview.texture = frameTexture;
        }
    }

    private void OnDisable()
    {
        if (captureRoutine != null)
        {
            StopCoroutine(captureRoutine);
            captureRoutine = null;
        }

        ReleaseTexture();
    }

    private void ReleaseTexture()
    {
        if (preview != null)
            preview.texture = null;

        if (frameTexture == null)
            return;

        frameTexture.Release();
        Destroy(frameTexture);
        frameTexture = null;
    }
}