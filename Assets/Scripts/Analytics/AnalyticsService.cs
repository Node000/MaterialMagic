using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 埋点门面。对外只暴露 Track / 公共属性 / 账号标识；内部把每条事件分发给已注册的
/// <see cref="IAnalyticsSink"/>。
///
/// 关键约定：
/// 1. 任何方法都不得抛异常、不得阻塞游戏流程；
/// 2. SDK 尚未初始化（未同意隐私政策）时事件进入内存队列，等真正的后端挂上来后补发；
/// 3. 同一属性名的取值类型必须恒定，列表统一编码成字符串（见 Assets/Docs/埋点后台录入表.md）。
/// </summary>
public static class AnalyticsService
{
    private static readonly List<IAnalyticsSink> sinks = new List<IAnalyticsSink>();
    private static readonly Dictionary<string, object> deviceProperties = new Dictionary<string, object>();
    private static readonly List<KeyValuePair<string, Dictionary<string, object>>> pendingEvents =
        new List<KeyValuePair<string, Dictionary<string, object>>>();

    private const int MaxPendingEvents = 64;

    /// <summary>本地待发文件的体积上限（字节）。超过后本会话不再落盘，下次启动补发完自动恢复。</summary>
    private const long MaxSpillBytes = 1024 * 1024;

    /// <summary>运行期上报开关（设置面板的「测试数据收集」）。独立于 <see cref="Config"/>，
    /// 这样即使还没 <see cref="Init"/> 也能一致地生效。</summary>
    private static bool runtimeEnabled = true;

    /// <summary>本进程 TapDB 原生采集是否开启（由 GameInitializer 在 Init 时确定，运行期改不了）。</summary>
    private static bool sessionCollectsNatively = true;

    /// <summary>TapDB 后端是否已挂上：只有它代表事件真的发出去了。</summary>
    private static bool tapSinkAttached;

    /// <summary>本地待发文件满的告警只打一次。</summary>
    private static bool warnedSpillFull;

    /// <summary>
    /// 本地待发文件：在“已同意但本会话原生采集未开启”的窗口里产生的事件先存这里
    /// （首启未决→同意、或开关从关到开），下次启动真正可采集时自动补发。
    /// </summary>
    public static string SpillPath => Path.Combine(Application.persistentDataPath, "Save", "analytics_pending.jsonl");

    public static bool HasPendingOnDisk
    {
        get
        {
            try { return File.Exists(SpillPath); }
            catch (Exception) { return false; }
        }
    }

    public static AnalyticsConfig Config { get; private set; }
    public static bool IsReady { get; private set; }

    /// <summary>
    /// 是否内部版本（编辑器 / Development 包 / 调试包）。作为**公共事件属性** `is_dev` 挂在每条事件上
    /// （见 Assets/Docs/eventProp.csv 与 AnalyticsProperty.IsDev）：TapDB 接收端对未登记的「用户属性」
    /// 会整条丢弃，事件属性通道才稳定；用途是把内部测试数据排除在数值平衡样本之外。
    /// </summary>
    public static readonly bool IsDevBuild = Application.isEditor || Debug.isDebugBuild;

    /// <summary>
    /// 运行期上报开关（对应设置面板的「测试数据收集」）。关闭后我们自己的事件立即停止入队/下发，
    /// 并清空待发队列；设备属性也不再推送。
    ///
    /// 注意：**TapDB 没有运行期 disable 接口**，SDK 自己的预置事件（`device_login` / `play_game`）
    /// 要到下次启动才彻底停止。因此关闭开关时必须同时把同意状态写成 `Declined`
    /// （见 `PrivacyConsentGate` / `Assets/Scripts/Platform/AnalyticsConsentToggleUI.cs`），
    /// 这样下次启动就不再初始化 SDK。
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        runtimeEnabled = enabled;
        if (Config != null)
            Config.enabled = enabled;

        if (!enabled)
            pendingEvents.Clear();
    }

    public static bool IsEnabled => runtimeEnabled && (Config == null || Config.enabled);

    /// <summary>
    /// 告知本进程 TapDB 原生采集是否开启（只在 Init 时能定）。为 false 时，同意后的事件会
    /// 落盘到 <see cref="SpillPath"/>，等下次启动真正可采集时补发（见 <see cref="FlushPendingOnDisk"/>）。
    /// </summary>
    public static void SetSessionCollection(bool collectsNatively)
    {
        sessionCollectsNatively = collectsNatively;
    }

    /// <summary>
    /// 补发上次未能上报的事件（在 TapDB 后端挂上之后调用）。补发的事件带
    /// <see cref="AnalyticsProperty.Replayed"/>=true，便于分析时区分；补发前先删文件，
    /// 避免补发中途崩溃导致重复补发。
    /// </summary>
    public static void FlushPendingOnDisk()
    {
        if (sinks.Count == 0 || !HasPendingOnDisk)
            return;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(SpillPath);
            File.Delete(SpillPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 读取待发文件失败：" + exception.Message);
            return;
        }

        int replayed = 0;
        int skipped = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string name;
            Dictionary<string, object> properties;
            if (!TryParseSpillLine(line, out name, out properties))
            {
                skipped++;
                continue;
            }

            properties[AnalyticsProperty.Replayed] = true;
            foreach (IAnalyticsSink sink in sinks)
                SafeLog(sink, name, properties);
            replayed++;
        }

        Debug.Log("[Analytics] 补发上次未上报的事件 " + replayed + " 条"
            + (skipped > 0 ? "（跳过损坏行 " + skipped + " 行）" : string.Empty));
    }

    /// <summary>丢弃本地待发数据（未同意/撤回同意时调用，保证不留未获同意的数据）。</summary>
    public static void DiscardPendingOnDisk()
    {
        try
        {
            if (!File.Exists(SpillPath))
                return;

            File.Delete(SpillPath);
            Debug.Log("[Analytics] 已丢弃未上报的本地待发数据（未获得采集同意）。");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 删除待发文件失败：" + exception.Message);
        }
    }

    public static void Init(AnalyticsConfig config)
    {
        Config = config ?? new AnalyticsConfig();
        IsReady = true;

        if (Config.logToConsole)
            AddSink(new ConsoleAnalyticsSink());
        if (Config.writeLocalFile)
            AddSink(new FileAnalyticsSink(FileAnalyticsSink.DefaultPath));
    }

    public static void AddSink(IAnalyticsSink sink)
    {
        if (sink == null || sinks.Contains(sink))
            return;

        sinks.Add(sink);
        if (sink is TapAnalyticsSink)
            tapSinkAttached = true;

        // 新后端挂上时：先补设备属性，再补发 SDK 就绪之前产生的事件。
        PushDevicePropertiesTo(sink);

        for (int i = 0; i < pendingEvents.Count; i++)
            SafeLog(sink, pendingEvents[i].Key, pendingEvents[i].Value);
        pendingEvents.Clear();
    }

    /// <summary>
    /// 设置设备属性（TapDB 的「用户属性 - 设备」，对应 Assets/Docs/userProp.csv）。
    /// 这类信息是筛选/分部维度，不需要每个事件重复携带。
    /// </summary>
    public static void SetDeviceProperty(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (value == null)
            deviceProperties.Remove(key);
        else
            deviceProperties[key] = value;

        PushDeviceProperties();
    }

    public static void SetDeviceProperty(string key, bool value) { SetDeviceProperty(key, (object)value); }
    public static void SetDeviceProperty(string key, int value) { SetDeviceProperty(key, (object)value); }
    public static void SetDeviceProperty(string key, string value) { SetDeviceProperty(key, (object)value); }

    private static void PushDeviceProperties()
    {
        for (int i = 0; i < sinks.Count; i++)
            PushDevicePropertiesTo(sinks[i]);
    }

    /// <summary>
    /// 重新推送一次当前设备属性。用于 TapDB 原生层设备注册尚未完成、首次上报被
    /// “设备初始化中，请稍后再试”拒掉的场景（重推是幂等的）。
    /// </summary>
    public static void RepushDeviceProperties()
    {
        PushDeviceProperties();
    }

    private static void PushDevicePropertiesTo(IAnalyticsSink sink)
    {
        if (sink == null || deviceProperties.Count == 0)
            return;

        // 运行期已关闭上报时不再推送设备属性。
        if (!IsEnabled)
            return;

        try
        {
            sink.SetDeviceProperties(new Dictionary<string, object>(deviceProperties));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 上报设备属性失败（不影响游戏）：" + exception.Message);
        }
    }

    public static void SetUserId(string userId)
    {
        foreach (IAnalyticsSink sink in sinks)
        {
            try { sink.SetUserId(userId); }
            catch (Exception exception) { Debug.LogWarning("[Analytics] SetUserId 失败：" + exception.Message); }
        }
    }

    public static void ClearUser()
    {
        foreach (IAnalyticsSink sink in sinks)
        {
            try { sink.ClearUser(); }
            catch (Exception exception) { Debug.LogWarning("[Analytics] ClearUser 失败：" + exception.Message); }
        }
    }

    public static void Track(string name)
    {
        Track(name, null);
    }

    public static void Track(string name, Dictionary<string, object> properties)
    {
        if (string.IsNullOrEmpty(name))
            return;

        // 总开关关闭时完全不上报（包括本地文件输出与落盘）。
        if (!IsEnabled)
            return;

        Dictionary<string, object> payload = BuildPayload(properties);

        // TapDB 后端还没挂上，分两种情形：
        //  * 本进程原生采集是开的 → 只是后端还没挂上，放内存队列，挂上后同会话补发；
        //  * 本进程原生采集是关的（首启未决、或曾被拒绝后重新打开）→ 落盘，下次启动补发；
        //    未同意时两者都不做（不采集、不缓存）。
        if (!tapSinkAttached)
        {
            if (sessionCollectsNatively)
            {
                if (pendingEvents.Count < MaxPendingEvents)
                    pendingEvents.Add(new KeyValuePair<string, Dictionary<string, object>>(name, payload));
            }
            else if (PrivacyConsentGate.HasConsented)
            {
                SpillToDisk(name, payload);
            }
        }

        foreach (IAnalyticsSink sink in sinks)
            SafeLog(sink, name, payload);
    }

    /// <summary>把一条事件追加到本地待发文件（JSONL，一行一条；损坏行会在补发时跳过）。</summary>
    private static void SpillToDisk(string name, Dictionary<string, object> payload)
    {
        try
        {
            string path = SpillPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            FileInfo info = new FileInfo(path);
            if (info.Exists && info.Length > MaxSpillBytes)
            {
                if (!warnedSpillFull)
                {
                    warnedSpillFull = true;
                    Debug.LogWarning("[Analytics] 本地待发文件已达上限，本会话不再落盘；下次启动补发完自动恢复。");
                }

                return;
            }

            string line = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["event"] = name,
                ["props"] = payload
            });
            File.AppendAllText(path, line + "\n", Encoding.UTF8);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 写入本地待发文件失败：" + exception.Message);
        }
    }

    private static bool TryParseSpillLine(string line, out string name, out Dictionary<string, object> properties)
    {
        name = null;
        properties = null;

        try
        {
            Dictionary<string, object> row = JsonConvert.DeserializeObject<Dictionary<string, object>>(line);
            if (row == null || !row.TryGetValue("event", out object rawName))
                return false;

            name = rawName as string;
            if (string.IsNullOrEmpty(name))
                return false;

            properties = new Dictionary<string, object>();
            if (!row.TryGetValue("props", out object rawProps) || !(rawProps is Newtonsoft.Json.Linq.JObject props))
                return true;

            foreach (KeyValuePair<string, Newtonsoft.Json.Linq.JToken> pair in props)
            {
                switch (pair.Value.Type)
                {
                    case Newtonsoft.Json.Linq.JTokenType.Boolean:
                        properties[pair.Key] = pair.Value.ToObject<bool>();
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.Integer:
                        properties[pair.Key] = pair.Value.ToObject<long>();
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.Float:
                        properties[pair.Key] = pair.Value.ToObject<double>();
                        break;
                    case Newtonsoft.Json.Linq.JTokenType.Null:
                        break;
                    default:
                        properties[pair.Key] = pair.Value.ToString();
                        break;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Flush()
    {
        // TapDB 自身负责断网续传，这里只保证本地文件 sink 的数据已经落盘。
        if (Config == null || !Config.logToConsole)
            return;
    }

    /// <summary>用户名/文案等文本属性统一截断，避免超长字段被后台丢弃。</summary>
    public static string Truncate(string value, int maxLength = 60)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>列表统一编码成 "," 分隔字符串；超出上限时截断（调用方自行决定是否附 truncated）。</summary>
    public static string JoinList(IEnumerable<string> values, out bool truncated)
    {
        truncated = false;
        if (values == null)
            return string.Empty;

        int maxItems = Config != null ? Mathf.Max(1, Config.maxListItems) : 20;
        StringBuilder builder = new StringBuilder();
        int count = 0;
        foreach (string value in values)
        {
            if (count >= maxItems)
            {
                truncated = true;
                break;
            }

            if (count > 0)
                builder.Append(',');
            builder.Append(value);
            count++;
        }

        return builder.ToString();
    }

    public static string JoinList(IEnumerable<string> values)
    {
        bool ignored;
        return JoinList(values, out ignored);
    }

    private static Dictionary<string, object> BuildPayload(Dictionary<string, object> properties)
    {
        Dictionary<string, object> payload = new Dictionary<string, object>();

        if (properties != null)
        {
            foreach (KeyValuePair<string, object> pair in properties)
                payload[pair.Key] = pair.Value;
        }

        // 公共标记：与 eventProp.csv 里绑定到全部事件的字段一致（排除内部测试数据）。
        payload[AnalyticsProperty.IsDev] = IsDevBuild;

        int maxLength = Config != null ? Mathf.Max(16, Config.maxStringLength) : 200;
        List<string> longKeys = null;
        foreach (KeyValuePair<string, object> pair in payload)
        {
            string text = pair.Value as string;
            if (text == null || text.Length <= maxLength)
                continue;

            if (longKeys == null)
                longKeys = new List<string>();
            longKeys.Add(pair.Key);
        }

        if (longKeys != null)
        {
            foreach (string key in longKeys)
                payload[key] = Truncate((string)payload[key], maxLength);
            payload[AnalyticsProperty.Truncated] = true;
        }

        return payload;
    }

    private static void SafeLog(IAnalyticsSink sink, string name, Dictionary<string, object> payload)
    {
        try
        {
            sink.LogEvent(name, payload);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 上报 " + name + " 失败（不影响游戏）：" + exception.Message);
        }
    }
}
