using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 交错演出队列（“伪同时”）：多项演出按固定间隔依次“启动”，彼此重叠执行，
/// 结尾由 WaitForAll() 统一等待全部完成。
///
/// 为什么需要它：一批死亡 / 入场如果同帧全部触发，会产生竞态（多个规则同帧写入、
/// 多次 LayoutEnemyViews 互相竞争）且反馈互相遮盖；完全串行又会让总时长变成 Σ(单项时长)。
/// 交错队列保证顺序确定、反馈可读，总时长约为 (N-1)*StartInterval + 单项时长。
///
/// 复用方式（参考死亡管线 HandSystemUI.RunEnemyDeathPipeline）：
///   runner.StartInterval = 0.15f;
///   runner.Start(itemA);                 // 第 1 项，立即启动
///   yield return new WaitForSeconds(runner.StartInterval);
///   runner.Start(itemB);                 // 第 2 项，与第 1 项重叠
///   yield return runner.WaitForAll();    // 全部结束后才继续
/// </summary>
public sealed class StaggeredPresentationRunner
{
    private readonly MonoBehaviour host;
    private readonly List<Coroutine> running = new List<Coroutine>();

    public StaggeredPresentationRunner(MonoBehaviour host)
    {
        this.host = host;
    }

    /// <summary>相邻两项的“启动”间隔（秒）。0 表示同一帧启动全部。</summary>
    public float StartInterval { get; set; } = 0.15f;

    public bool HasRunning => running.Count > 0;
    public int Count => running.Count;

    /// <summary>启动一项演出，不等待其结束。</summary>
    public Coroutine Start(IEnumerator item)
    {
        if (host == null || item == null)
            return null;

        Coroutine coroutine = host.StartCoroutine(item);
        running.Add(coroutine);
        return coroutine;
    }

    /// <summary>等待所有已启动的演出结束，并清空队列。</summary>
    public IEnumerator WaitForAll()
    {
        for (int i = 0; i < running.Count; i++)
        {
            Coroutine coroutine = running[i];
            if (coroutine != null)
                yield return coroutine;
        }
        running.Clear();
    }

    public void Clear()
    {
        running.Clear();
    }
}
