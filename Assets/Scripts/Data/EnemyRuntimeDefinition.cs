using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRuntimeDefinition
{
    public EnemyData BaseData { get; }
    public int MaxHealth { get; set; }
    public int BaseAttack { get; set; }
    public bool IsMinion { get; set; }
    public List<BuffStackData> InitialBuffs { get; } = new List<BuffStackData>();
    public EnemyIntentPlan IntentPlan { get; }

    public EnemyRuntimeDefinition(EnemyData baseData)
    {
        BaseData = baseData;
        MaxHealth = baseData != null ? Mathf.Max(1, baseData.maxHealth) : 1;
        BaseAttack = baseData != null ? baseData.baseAttack : 0;
        IsMinion = baseData != null && baseData.isMinion;
        CopyBuffs(baseData != null ? baseData.initialBuffs : null, InitialBuffs);
        IntentPlan = EnemyIntentPlan.FromEnemyData(baseData);
    }

    public int NumericId => BaseData != null ? BaseData.numericId : 0;
    public string Id => BaseData != null ? BaseData.Id : string.Empty;

    public void AddInitialBuff(BuffEnum buffType, int stack)
    {
        if (buffType == BuffEnum.None || stack <= 0)
            return;

        for (int i = 0; i < InitialBuffs.Count; i++)
        {
            BuffStackData buff = InitialBuffs[i];
            if (buff != null && buff.buffType == buffType)
            {
                buff.stack += stack;
                return;
            }
        }

        InitialBuffs.Add(new BuffStackData { buffType = buffType, stack = stack });
    }

    private static void CopyBuffs(BuffStackData[] source, List<BuffStackData> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            BuffStackData buff = source[i];
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0)
                target.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        }
    }
}

public static class EnemyRuntimeDefinitionFactory
{
    public static EnemyRuntimeDefinition Create(EnemyData data, DifficultyUpgradeContext context)
    {
        if (data == null)
            return null;

        EnemyRuntimeDefinition definition = new EnemyRuntimeDefinition(data);
        DifficultyUpgradeSystem.ApplyEnemyUpgrades(definition, context);
        return definition;
    }
}

public class EnemyIntentPlanState
{
    public int Phase;
    public int ActionIndex;
    public HashSet<int> ConsumedOnlyOnceIntentIds = new HashSet<int>();
    public int LastResolvedIntentGroupId = -1;
    public int SelectedIntentPhase = -1;
    public int SelectedIntentActionIndex = -1;
    public int SelectedIntentGroupId;
}

public class EnemyIntentPlan
{
    private readonly EnemyPhaseData[] phases;
    private readonly EnemyIntentGroupData[] intentGroups;
    private readonly EnemyIntentLoopData[] intentLoop;
    private readonly EnemyActionData[] actionLoop;

    private EnemyIntentPlan(EnemyPhaseData[] phases, EnemyIntentGroupData[] intentGroups, EnemyIntentLoopData[] intentLoop, EnemyActionData[] actionLoop)
    {
        this.phases = phases ?? Array.Empty<EnemyPhaseData>();
        this.intentGroups = intentGroups ?? Array.Empty<EnemyIntentGroupData>();
        this.intentLoop = intentLoop ?? Array.Empty<EnemyIntentLoopData>();
        this.actionLoop = actionLoop ?? Array.Empty<EnemyActionData>();
    }

    public static EnemyIntentPlan FromEnemyData(EnemyData data)
    {
        return new EnemyIntentPlan(
            ClonePhases(data != null ? data.phases : null),
            CloneIntentGroups(data != null ? data.intentGroups : null),
            CloneIntentLoop(data != null ? data.intentLoop : null),
            CloneActionLoop(data != null ? data.actionLoop : null));
    }

    public EnemyActionData GetCurrentAction(EnemyIntentPlanState state)
    {
        if (actionLoop == null || actionLoop.Length == 0)
            return null;

        int actionIndex = state != null ? state.ActionIndex : 0;
        return actionLoop[Mathf.Max(0, actionIndex) % actionLoop.Length];
    }

    public List<EnemyIntentData> SelectCurrentIntents(EnemyIntentPlanState state, Func<int, int, int> nextRandomInt)
    {
        List<EnemyIntentData> results = new List<EnemyIntentData>();
        EnemyIntentGroupData group = GetCurrentIntentGroup(state, nextRandomInt);
        if (group != null && group.intents != null && group.intents.Length > 0)
        {
            for (int i = 0; i < group.intents.Length; i++)
                results.Add(NormalizeIntent(CloneIntent(group.intents[i])));
            return results;
        }

        EnemyActionData action = GetCurrentAction(state);
        if (action != null)
            results.Add(CreateIntentFromAction(action));
        return results;
    }

    public void MultiplyAttackIntentValues(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            return;

        ApplyToAllIntents(intent =>
        {
            if (intent != null && (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll))
                intent.value = Mathf.Max(0, Mathf.RoundToInt(intent.value * multiplier));
        });

        for (int i = 0; i < actionLoop.Length; i++)
        {
            EnemyActionData enemyAction = actionLoop[i];
            if (enemyAction != null && (enemyAction.actionType == EnemyActionType.Attack || enemyAction.actionType == EnemyActionType.AttackAll))
                enemyAction.value = Mathf.Max(0, Mathf.RoundToInt(enemyAction.value * multiplier));
        }
    }

    private void ApplyToAllIntents(Action<EnemyIntentData> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < intentGroups.Length; i++)
            ApplyToGroupIntents(intentGroups[i], action);
        for (int i = 0; i < phases.Length; i++)
        {
            EnemyPhaseData phase = phases[i];
            if (phase == null)
                continue;
            for (int groupIndex = 0; phase.intentGroups != null && groupIndex < phase.intentGroups.Length; groupIndex++)
                ApplyToGroupIntents(phase.intentGroups[groupIndex], action);
            for (int groupIndex = 0; phase.intentPool != null && groupIndex < phase.intentPool.Length; groupIndex++)
                ApplyToGroupIntents(phase.intentPool[groupIndex], action);
        }
    }

    private static void ApplyToGroupIntents(EnemyIntentGroupData group, Action<EnemyIntentData> action)
    {
        for (int i = 0; group != null && group.intents != null && i < group.intents.Length; i++)
            action(group.intents[i]);
    }

    private EnemyIntentGroupData GetCurrentIntentGroup(EnemyIntentPlanState state, Func<int, int, int> nextRandomInt)
    {
        int phase = state != null ? state.Phase : 0;
        int actionIndex = state != null ? state.ActionIndex : 0;
        if (TryGetSelectedIntentGroup(state, phase, actionIndex, out EnemyIntentGroupData selectedGroup))
            return selectedGroup;

        EnemyIntentGroupData[] groups;
        EnemyIntentLoopData[] loop = GetCurrentSeparatedIntentLoop(phase, out groups);
        if (loop != null && loop.Length > 0)
            return GetCurrentSeparatedIntentGroup(loop, groups, state, nextRandomInt);

        EnemyIntentGroupData[] pool = GetCurrentIntentPool(phase);
        if (pool == null || pool.Length == 0)
            return null;

        int checkedCount = 0;
        int index = Mathf.Max(0, actionIndex) % pool.Length;
        while (checkedCount < pool.Length)
        {
            EnemyIntentGroupData group = pool[index];
            if (group != null && (!group.onlyOnce || group.id == 0 || state == null || !state.ConsumedOnlyOnceIntentIds.Contains(group.id)) && CanUseIntentGroup(group.id, pool.Length, state))
            {
                if (state != null)
                {
                    if (group.onlyOnce && group.id != 0)
                        state.ConsumedOnlyOnceIntentIds.Add(group.id);
                    state.LastResolvedIntentGroupId = group.id;
                    RememberSelectedIntentGroup(state, group.id);
                }
                return group;
            }

            index = (index + 1) % pool.Length;
            checkedCount++;
        }

        return null;
    }

    private EnemyIntentGroupData GetCurrentSeparatedIntentGroup(EnemyIntentLoopData[] loop, EnemyIntentGroupData[] groups, EnemyIntentPlanState state, Func<int, int, int> nextRandomInt)
    {
        if (loop == null || loop.Length == 0 || groups == null || groups.Length == 0)
            return null;

        int actionIndex = state != null ? state.ActionIndex : 0;
        int checkedCount = 0;
        int index = Mathf.Max(0, actionIndex) % loop.Length;
        while (checkedCount < loop.Length)
        {
            EnemyIntentLoopData entry = loop[index];
            if (entry != null)
            {
                int groupId = ResolveLoopGroupId(entry, groups, nextRandomInt);
                int onceKey = -(index + 1);
                bool onceAllowed = !entry.onlyOnce || state == null || !state.ConsumedOnlyOnceIntentIds.Contains(onceKey);
                if (groupId != 0 && onceAllowed && CanUseIntentGroup(groupId, groups.Length, state))
                {
                    EnemyIntentGroupData group = FindIntentGroup(groups, groupId);
                    if (group != null)
                    {
                        if (state != null)
                        {
                            if (entry.onlyOnce)
                                state.ConsumedOnlyOnceIntentIds.Add(onceKey);
                            state.LastResolvedIntentGroupId = groupId;
                            RememberSelectedIntentGroup(state, groupId);
                        }
                        return group;
                    }
                }
            }

            index = (index + 1) % loop.Length;
            checkedCount++;
        }

        return null;
    }

    private int ResolveLoopGroupId(EnemyIntentLoopData entry, EnemyIntentGroupData[] groups, Func<int, int, int> nextRandomInt)
    {
        if (entry.randomGroupIds != null && entry.randomGroupIds.Length > 0)
        {
            if (entry.randomGroupIds.Length == 1)
                return entry.randomGroupIds[0];

            int[] candidates = entry.randomGroupIds;
            int fallback = candidates[NextRandomInt(nextRandomInt, 0, candidates.Length)];
            for (int i = 0; i < candidates.Length; i++)
            {
                int candidate = candidates[NextRandomInt(nextRandomInt, 0, candidates.Length)];
                if (CanUseIntentGroup(candidate, groups != null ? groups.Length : candidates.Length, null))
                    return candidate;
            }
            return fallback;
        }

        return entry.groupId;
    }

    private bool TryGetSelectedIntentGroup(EnemyIntentPlanState state, int phase, int actionIndex, out EnemyIntentGroupData group)
    {
        group = null;
        if (state == null || state.SelectedIntentPhase != phase || state.SelectedIntentActionIndex != actionIndex || state.SelectedIntentGroupId == 0)
            return false;

        EnemyIntentGroupData[] groups;
        EnemyIntentLoopData[] loop = GetCurrentSeparatedIntentLoop(phase, out groups);
        group = FindIntentGroup(loop != null && loop.Length > 0 ? groups : GetCurrentIntentPool(phase), state.SelectedIntentGroupId);
        return group != null;
    }

    private static void RememberSelectedIntentGroup(EnemyIntentPlanState state, int groupId)
    {
        if (state == null)
            return;

        state.SelectedIntentPhase = state.Phase;
        state.SelectedIntentActionIndex = state.ActionIndex;
        state.SelectedIntentGroupId = groupId;
    }

    private static int NextRandomInt(Func<int, int, int> nextRandomInt, int minInclusive, int maxExclusive)
    {
        return nextRandomInt != null ? nextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private static bool CanUseIntentGroup(int groupId, int totalGroupCount, EnemyIntentPlanState state)
    {
        if (groupId == 0 || totalGroupCount <= 1 || state == null || state.LastResolvedIntentGroupId == -1)
            return true;

        return groupId != state.LastResolvedIntentGroupId;
    }

    private EnemyIntentLoopData[] GetCurrentSeparatedIntentLoop(int phase, out EnemyIntentGroupData[] groups)
    {
        groups = null;
        for (int i = 0; i < phases.Length; i++)
        {
            EnemyPhaseData phaseData = phases[i];
            if (phaseData != null && phaseData.phase == phase)
            {
                groups = phaseData.intentGroups;
                return phaseData.intentLoop;
            }
        }

        groups = intentGroups;
        return intentLoop;
    }

    private EnemyIntentGroupData[] GetCurrentIntentPool(int phase)
    {
        for (int i = 0; i < phases.Length; i++)
        {
            EnemyPhaseData phaseData = phases[i];
            if (phaseData != null && phaseData.phase == phase)
                return phaseData.intentPool;
        }

        return null;
    }

    private static EnemyIntentGroupData FindIntentGroup(EnemyIntentGroupData[] groups, int groupId)
    {
        for (int i = 0; groups != null && i < groups.Length; i++)
        {
            EnemyIntentGroupData group = groups[i];
            if (group != null && group.id == groupId)
                return group;
        }

        return null;
    }

    private static EnemyPhaseData[] ClonePhases(EnemyPhaseData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<EnemyPhaseData>();

        EnemyPhaseData[] result = new EnemyPhaseData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            EnemyPhaseData phase = source[i];
            result[i] = phase != null ? new EnemyPhaseData
            {
                phase = phase.phase,
                intentGroups = CloneIntentGroups(phase.intentGroups),
                intentLoop = CloneIntentLoop(phase.intentLoop),
                intentPool = CloneIntentGroups(phase.intentPool)
            } : null;
        }
        return result;
    }

    private static EnemyIntentGroupData[] CloneIntentGroups(EnemyIntentGroupData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<EnemyIntentGroupData>();

        EnemyIntentGroupData[] result = new EnemyIntentGroupData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            EnemyIntentGroupData group = source[i];
            result[i] = group != null ? new EnemyIntentGroupData
            {
                id = group.id,
                onlyOnce = group.onlyOnce,
                intents = CloneIntents(group.intents)
            } : null;
        }
        return result;
    }

    private static EnemyIntentLoopData[] CloneIntentLoop(EnemyIntentLoopData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<EnemyIntentLoopData>();

        EnemyIntentLoopData[] result = new EnemyIntentLoopData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            EnemyIntentLoopData entry = source[i];
            result[i] = entry != null ? new EnemyIntentLoopData
            {
                groupId = entry.groupId,
                randomGroupIds = CloneIntArray(entry.randomGroupIds),
                onlyOnce = entry.onlyOnce
            } : null;
        }
        return result;
    }

    private static EnemyActionData[] CloneActionLoop(EnemyActionData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<EnemyActionData>();

        EnemyActionData[] result = new EnemyActionData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            EnemyActionData action = source[i];
            result[i] = action != null ? new EnemyActionData
            {
                actionType = action.actionType,
                value = action.value,
                summonEnemyId = action.summonEnemyId,
                summonCount = action.summonCount,
                buffs = CloneBuffArray(action.buffs),
                descriptionKey = action.descriptionKey
            } : null;
        }
        return result;
    }

    private static EnemyIntentData[] CloneIntents(EnemyIntentData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<EnemyIntentData>();

        EnemyIntentData[] result = new EnemyIntentData[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = CloneIntent(source[i]);
        return result;
    }

    private static EnemyIntentData CloneIntent(EnemyIntentData intent)
    {
        if (intent == null)
            return null;

        return new EnemyIntentData
        {
            intentType = intent.intentType,
            actionType = intent.actionType,
            value = intent.value,
            times = intent.times,
            hitInterval = intent.hitInterval,
            summonEnemyId = intent.summonEnemyId,
            summonCount = intent.summonCount,
            buffs = CloneBuffArray(intent.buffs),
            displayType = intent.displayType,
            descriptionKey = intent.descriptionKey
        };
    }

    private static BuffStackData[] CloneBuffArray(BuffStackData[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<BuffStackData>();

        BuffStackData[] result = new BuffStackData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            BuffStackData buff = source[i];
            result[i] = buff != null ? new BuffStackData { buffType = buff.buffType, stack = buff.stack } : null;
        }
        return result;
    }

    private static int[] CloneIntArray(int[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<int>();

        int[] result = new int[source.Length];
        Array.Copy(source, result, source.Length);
        return result;
    }

    private static EnemyIntentData NormalizeIntent(EnemyIntentData intent)
    {
        if (intent == null)
            return null;

        intent.intentType = GetIntentType(intent.actionType);
        if (intent.times <= 0)
            intent.times = 1;
        if (intent.hitInterval <= 0f)
            intent.hitInterval = 0.2f;
        if (intent.summonCount <= 0)
            intent.summonCount = 1;
        return intent;
    }

    private static EnemyIntentData CreateIntentFromAction(EnemyActionData action)
    {
        return NormalizeIntent(new EnemyIntentData
        {
            actionType = action.actionType,
            value = action.value,
            times = 1,
            buffs = CloneBuffArray(action.buffs),
            summonEnemyId = action.summonEnemyId,
            summonCount = action.summonCount,
            descriptionKey = action.descriptionKey
        });
    }

    private static EnemyIntentType GetIntentType(EnemyActionType actionType)
    {
        switch (actionType)
        {
            case EnemyActionType.Attack:
            case EnemyActionType.AttackAll:
                return EnemyIntentType.Attack;
            case EnemyActionType.GainShield:
                return EnemyIntentType.Defend;
            case EnemyActionType.ApplyBuff:
                return EnemyIntentType.ApplyBuff;
            case EnemyActionType.ApplyDebuff:
                return EnemyIntentType.ApplyDebuff;
            case EnemyActionType.Summon:
                return EnemyIntentType.Summon;
            case EnemyActionType.Special:
            case EnemyActionType.Stunned:
            default:
                return EnemyIntentType.None;
        }
    }
}
