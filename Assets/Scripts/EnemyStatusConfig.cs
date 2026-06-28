using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyStatusBreakpointData
{
    public int breakpoint;
    public float growthRate = 1f;
}

[CreateAssetMenu(fileName = "EnemyStatusConfig", menuName = "Config/Enemy Status Config")]
public class EnemyStatusConfig : ScriptableObject
{
    [SerializeField] private float defaultHealthBarWidth = 130f;
    [SerializeField] private List<EnemyStatusBreakpointData> healthBarGrowth = new List<EnemyStatusBreakpointData>
    {
        new EnemyStatusBreakpointData { breakpoint = 100, growthRate = 1f },
        new EnemyStatusBreakpointData { breakpoint = 200, growthRate = 0.4f }
    };
    [SerializeField] private float healthBarMinWidth = 90f;
    [SerializeField] private float healthBarMaxWidth = 220f;
    [SerializeField] private float buffSlotSize = 42f;
    [SerializeField] private float buffSlotSpacing = 6f;
    [SerializeField] private int buffMinColumnCount = 1;
    [SerializeField] private int buffMaxColumnCount = 5;
    [SerializeField] private float buffColumnsPerHealthBarWidth = 32f;

    public float DefaultHealthBarWidth => Mathf.Max(1f, defaultHealthBarWidth);
    public IReadOnlyList<EnemyStatusBreakpointData> HealthBarGrowth => healthBarGrowth;
    public float HealthBarMinWidth => Mathf.Max(1f, healthBarMinWidth);
    public float HealthBarMaxWidth => Mathf.Max(HealthBarMinWidth, healthBarMaxWidth);
    public float BuffSlotSize => Mathf.Max(1f, buffSlotSize);
    public float BuffSlotSpacing => Mathf.Max(0f, buffSlotSpacing);
    public int BuffMinColumnCount => Mathf.Max(1, buffMinColumnCount);
    public int BuffMaxColumnCount => Mathf.Max(BuffMinColumnCount, buffMaxColumnCount);
    public float BuffColumnsPerHealthBarWidth => Mathf.Max(1f, buffColumnsPerHealthBarWidth);
}

