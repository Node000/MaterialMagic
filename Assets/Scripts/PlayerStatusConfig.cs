using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BreakpointRateData
{
    public int breakpoint;
    public float growthRate = 1f;
}

[CreateAssetMenu(fileName = "PlayerStatusConfig", menuName = "Config/Player Status Config")]
public class PlayerStatusConfig : ScriptableObject
{
    [SerializeField] private float buffSlotSize = 42f;
    [SerializeField] private float buffSlotSpacing = 6f;
    [SerializeField] private int buffRootColumnCount = 5;
    [SerializeField] private int buffRootRowCount = 2;

    public float BuffSlotSize => Mathf.Max(1f, buffSlotSize);
    public float BuffSlotSpacing => Mathf.Max(0f, buffSlotSpacing);
    public int BuffRootColumnCount => Mathf.Max(1, buffRootColumnCount);
    public int BuffRootRowCount => Mathf.Max(1, buffRootRowCount);
}
