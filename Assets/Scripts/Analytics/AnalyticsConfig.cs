using System;

/// <summary>
/// 埋点运行期配置。由 <see cref="GameInitializer"/> 按场景上序列化的字段组装；
/// 后续如需按包切换，可替换为 ScriptableObject 资产。
/// </summary>
[Serializable]
public class AnalyticsConfig
{
    /// <summary>总开关。关闭后只走本地/控制台输出，不产生任何外部上报。</summary>
    public bool enabled = true;

    /// <summary>把每条事件打到 Console，开发期核对字段用。</summary>
    public bool logToConsole = false;

    /// <summary>把每条事件追加写入本地 JSONL 文件（开发期验证用，不依赖原生 SDK）。</summary>
    public bool writeLocalFile = false;

    /// <summary>单条字符串属性的最大长度，超出截断并把 truncated 置为 true。</summary>
    public int maxStringLength = 200;

    /// <summary>列表类字段最多保留的元素个数，超出截断。</summary>
    public int maxListItems = 20;
}
