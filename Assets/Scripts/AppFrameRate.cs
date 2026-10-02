using UnityEngine;

/// <summary>
/// 全局帧率设定（2026-10-02：统一目标 60 帧）。
///
/// 用 <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 在首个场景加载前应用，
/// 因此不依赖任何场景对象：正常从 StartScene 启动、还是调试时直接 Play 战斗场景都生效。
///
/// 注意两点：
/// * **必须同时关掉垂直同步**——`Application.targetFrameRate` 在 vSync 开启时会被忽略；
/// * Android 默认画质（Medium）在 QualitySettings 里是 `vSyncCount = 1`，
///   运行期这里改成 0 会覆盖当前画质等级的该字段（若将来运行期切换画质，需要重新调用 <see cref="Apply"/>）。
/// </summary>
public static class AppFrameRate
{
    /// <summary>本项目统一目标帧率。</summary>
    public const int Target = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Target;
    }
}
