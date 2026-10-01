using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 上报后端。所有实现都必须「永不抛异常」，绝不能影响游戏流程。
/// </summary>
public interface IAnalyticsSink
{
    void LogEvent(string name, Dictionary<string, object> properties);

    /// <summary>上报设备属性（对应 TapDB 的「用户属性 - 设备」）。</summary>
    void SetDeviceProperties(Dictionary<string, object> properties);

    /// <summary>设置账号维度标识（仅支持账号维度的后端有实现）。</summary>
    void SetUserId(string userId);

    /// <summary>清除账号维度标识。</summary>
    void ClearUser();
}

/// <summary>开发期控制台输出。</summary>
public class ConsoleAnalyticsSink : IAnalyticsSink
{
    public void LogEvent(string name, Dictionary<string, object> properties)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[Analytics] ").Append(name);
        if (properties != null)
        {
            foreach (KeyValuePair<string, object> pair in properties)
                builder.Append(" | ").Append(pair.Key).Append('=').Append(pair.Value);
        }
        Debug.Log(builder.ToString());
    }

    public void SetDeviceProperties(Dictionary<string, object> properties)
    {
        if (properties == null || properties.Count == 0)
            return;

        StringBuilder builder = new StringBuilder("[Analytics] DeviceProps");
        foreach (KeyValuePair<string, object> pair in properties)
            builder.Append(" | ").Append(pair.Key).Append('=').Append(pair.Value);
        Debug.Log(builder.ToString());
    }

    public void SetUserId(string userId) { }
    public void ClearUser() { }
}

/// <summary>开发期本地文件输出（JSONL），用于在没有原生 SDK 的环境下核对事件序列。</summary>
public class FileAnalyticsSink : IAnalyticsSink
{
    private readonly string path;
    private readonly object writeLock = new object();

    public FileAnalyticsSink(string path)
    {
        this.path = path;
    }

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "Save", "analytics_events.jsonl");

    public void LogEvent(string name, Dictionary<string, object> properties)
    {
        try
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                ["event"] = name,
                ["ts_utc"] = DateTime.UtcNow.ToString("o")
            };
            if (properties != null)
            {
                foreach (KeyValuePair<string, object> pair in properties)
                    payload[pair.Key] = pair.Value;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            lock (writeLock)
            {
                File.AppendAllText(path, JsonConvert.SerializeObject(payload) + "\n", Encoding.UTF8);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 写入本地文件失败：" + exception.Message);
        }
    }

    public void SetDeviceProperties(Dictionary<string, object> properties) { }

    public void SetUserId(string userId) { }
    public void ClearUser() { }
}

/// <summary>TapDB 上报：真机（Android/iOS）与 TapTap 后端可用时使用。</summary>
public class TapAnalyticsSink : IAnalyticsSink
{
    private readonly bool verbose;

    public TapAnalyticsSink(bool verbose = false)
    {
        this.verbose = verbose;
    }

    public void LogEvent(string name, Dictionary<string, object> properties)
    {
        try
        {
            TapSDK.Core.TapTapEvent.LogEvent(name, properties);
            if (verbose)
                Debug.Log("[Analytics] TapDB <- " + name);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] TapDB 上报失败（不影响游戏）：" + exception.Message);
        }
    }

    public void SetDeviceProperties(Dictionary<string, object> properties)
    {
        try
        {
            TapSDK.Core.TapTapEvent.DeviceUpdate(properties);
            if (verbose)
                Debug.Log("[Analytics] TapDB <- DeviceUpdate (" + (properties != null ? properties.Count : 0) + " 项)");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] 设备属性上报失败（不影响游戏）：" + exception.Message);
        }
    }

    public void SetUserId(string userId)
    {
        try
        {
            if (!string.IsNullOrEmpty(userId))
                TapSDK.Core.TapTapEvent.SetUserID(userId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] SetUserID 失败：" + exception.Message);
        }
    }

    public void ClearUser()
    {
        try
        {
            TapSDK.Core.TapTapEvent.ClearUser();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Analytics] ClearUser 失败：" + exception.Message);
        }
    }
}
