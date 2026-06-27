using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevelType
{
    Battle = 0,
    Event = 1,
    Shop = 2,
    Rest = 3,
    Reward = 4,
    Elite = 5,
    RemoveMaterial = 6,
    AddMaterial = 7
}

[Serializable]
public class EconomyConfigData : INumericDataRecord
{
    public int numericId;
    public string id;
    public int shopSpellPrice = 4;
    public int shopMaterialPrice = 6;
    public int shopRemoveMaterialPrice = 7;
    public int shopProductPoolId = 1;
    public int battleGoldMin = 1;
    public int battleGoldMax = 2;
    public int eliteBattleGoldMin = 4;
    public int eliteBattleGoldMax = 5;
    public int shopMagicRewardPoolId = 1;
    public MaterialEnum[] shopMaterialPool = Array.Empty<MaterialEnum>();

    public int NumericId => numericId;
}

[Serializable]
public class ShopMaterialOfferData
{
    public MaterialEnum material;
    public string modifierId;
    public int price;
}

[Serializable]
public class ShopProductPoolData : INumericDataRecord
{
    public int numericId;
    public string id;
    public int[] magicIds = Array.Empty<int>();
    public ShopMaterialOfferData[] strongMaterialOffers = Array.Empty<ShopMaterialOfferData>();
    public ShopMaterialOfferData[] normalMaterialOffers = Array.Empty<ShopMaterialOfferData>();
    public ShopMaterialOfferData[] weakMaterialOffers = Array.Empty<ShopMaterialOfferData>();
    public float weakMaterialChance = 0.1f;

    public int NumericId => numericId;
}

public enum MaterialEnum
{
    None = 0,
    Fire = 1,
    Wind = 2,
    Water = 3,
    Earth = 4,
    Wild = 5
}

public enum BuffEnum
{
    None = 0,
    Shield = 1,
    Burning = 2,
    Weak = 3,
    Vulnerable = 6,
    Slow = 7,
    Arc = 9,
    SpellPower = 10,
    BurningNextTurn = 11,
    ShieldReflect = 12,
    ExtraDraw = 13,
    ExtraRefresh = 14,
    Sturdy = 15,
    Stable = 16,
    Disorder = 17,
    DefensePower = 18,
    BurnOnAttack = 19,
    RepeatSpell = 20,
    DebuffPower = 21,
    VortexNextDraw = 22,
    EggDeathExplosion = 23,
    ShieldOnHealthLoss = 24,
    ShuffleHandOnInvokeChance = 25,
    AttributeDisabled = 26,
    Curse = 27,
    DirectionDamageBonus = 28,
    DirectionWeakBonus = 29,
    DirectionExtraDraw = 30,
    DirectionShieldBonus = 31,
    MaterialOverplayDebuff = 32,
    PreparedShield = 33,
    KindlingNextDraw = 34,
    DrawOnEnemyAttack = 35,
    BurningOnEnemyAttack = 36,
    ExtraDrawOnEnemyDamage = 37,
    ShieldReflectBoost = 38,
    MagicAttackAll = 39,
    NextMagicRepeat = 40,
    Claw = 41,
    LazyNextDraw = 42,
    ChargeNextDraw = 43,
    TutorialDeath = 44
}

public enum BuffKindEnum
{
    Buff = 0,
    DeBuff = 1,
    Neutral = 2
}

[Serializable]
public class BuffStackData
{
    public BuffEnum buffType;
    public int stack;
}

public enum MagicMatchRule
{
    ExactRecipe = 0,
    AnyTwoDifferentElements = 1
}

public enum MagicEffectType
{
    None = 0,
    Damage = 1,
    GainShield = 2,
    Heal = 3,
    ApplyBuff = 4,
    DrawNextTurn = 5
}

public enum EnemyIntentType
{
    None = 0,
    Attack = 1,
    Defend = 2,
    ApplyBuff = 3,
    ApplyDebuff = 4,
    Summon = 5
}

public enum EnemyActionType
{
    None = 0,
    Attack = 1,
    GainShield = 2,
    ApplyBuff = 3,
    AddPollution = 4,
    CounterFirstMagic = 5,
    ApplyDebuff = 6,
    Summon = 7,
    AttackAll = 8,
    Special = 9,
    Stunned = 10
}

public enum EventRewardType
{
    None = 0,
    Heal = 1,
    LoseHealth = 2,
    GainGold = 3,
    GainMagic = 4,
    UpgradeMaterial = 5,
    RemovePollution = 6,
    GainRelic = 7,
    GainMagicModifier = 8,
    IncreaseMaxHealth = 9,
    GainMaterial = 10,
    GainRandomMaterial = 11,
    GainSameRandomMaterials = 12,
    IncreaseDrawCount = 13,
    RemoveMaterial = 14,
    GainNextBattleStartShield = 15,
    GainMaterialModifier = 16,
    SpendAllGold = 17,
    RandomizeDeckBasicMaterials = 18,
    GainRandomSyntaxMaterial = 19
}

public enum BonusRewardType
{
    None = 0,
    Gold = 1,
    Heal = 2
}

public enum MagicModifierTargetRule
{
    None = 0,
    Any = 1,
    Element = 2,
    EffectType = 3,
    Tag = 4
}

[Serializable]
public class PlayerStartMaterialData
{
    public MaterialEnum material;
    public int count;
}

[Serializable]
public class PlayerStartMagicData
{
    public int slotIndex;
    public int magicId;
}

[Serializable]
public class PlayerStartConfigData : IDataRecord
{
    public string id;
    public string displayName;
    public string texturePath;
    public string color;
    public int maxHealth = 50;
    public int gold;
    public int drawCount = 4;
    public int maxPlayCount = 3;
    public PlayerStartMaterialData[] initialMaterials = Array.Empty<PlayerStartMaterialData>();
    public PlayerStartMagicData[] initialMagics = Array.Empty<PlayerStartMagicData>();

    public string Id => id;
}

[Serializable]
public class MagicData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public string nameKey;
    public string descriptionKey;
    public string script;
    public string iconName;
    public MaterialEnum element;
    public string[] tagIds = Array.Empty<string>();
    public MaterialEnum[] recipe = Array.Empty<MaterialEnum>();
    public MagicMatchRule matchRule;
    public bool playPlayerCastAnimation = true;

    public string Id => id;
    public int NumericId => numericId;
}

[Serializable]
public class TagData : IDataRecord
{
    public string id;
    public string nameKey;
    public string descriptionKey;

    public string Id => id;
}

[Serializable]
public class MagicModifierData : IDataRecord
{
    public string id;
    public string nameKey;
    public string descriptionKey;
    public string iconName;
    public MagicModifierTargetRule targetRule = MagicModifierTargetRule.Any;
    public MaterialEnum targetElement;
    public MagicEffectType targetEffectType;
    public string targetTagId;
    public int value;
    public int weight = 1;

    public string Id => id;
}

[Serializable]
public class EnemyIntentData
{
    public EnemyIntentType intentType;
    public EnemyActionType actionType;
    public int value;
    public int times = 1;
    public float hitInterval = 0.2f;
    public int summonEnemyId;
    public int summonCount = 1;
    public BuffStackData[] buffs = Array.Empty<BuffStackData>();
    public string displayType;
    public string descriptionKey;
}

[Serializable]
public class EnemyIntentGroupData
{
    public int id;
    public bool onlyOnce;
    public EnemyIntentData[] intents = Array.Empty<EnemyIntentData>();
}

[Serializable]
public class EnemyIntentLoopData
{
    public int groupId;
    public int[] randomGroupIds = Array.Empty<int>();
    public bool onlyOnce;
}

[Serializable]
public class EnemyPhaseData
{
    public int phase;
    public EnemyIntentGroupData[] intentGroups = Array.Empty<EnemyIntentGroupData>();
    public EnemyIntentLoopData[] intentLoop = Array.Empty<EnemyIntentLoopData>();
    public EnemyIntentGroupData[] intentPool = Array.Empty<EnemyIntentGroupData>();
}

[Serializable]
public class EnemyActionData
{
    public EnemyActionType actionType;
    public int value;
    public int summonEnemyId;
    public int summonCount = 1;
    public BuffStackData[] buffs = Array.Empty<BuffStackData>();
    public string descriptionKey;
}

[Serializable]
public class EnemyData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public string string_id;
    public string nameKey;
    public int maxHealth;
    public int baseAttack;
    public float imageScale = 1f;
    public bool hoverEffect = true;
    public Vector2 infoBoxSize;
    public Vector2 infoBoxOffset;
    public float healthBarWidth;
    public float intentOffsetX;
    public float intentOffsetY;
    public bool isMinion;
    public BuffStackData[] initialBuffs = Array.Empty<BuffStackData>();
    public string iconName;
    public string spriteAnimationPath;
    public float animationFrameRate = 8f;
    public EnemyPhaseData[] phases = Array.Empty<EnemyPhaseData>();
    public EnemyIntentGroupData[] intentGroups = Array.Empty<EnemyIntentGroupData>();
    public EnemyIntentLoopData[] intentLoop = Array.Empty<EnemyIntentLoopData>();
    public EnemyActionData[] actionLoop = Array.Empty<EnemyActionData>();

    public string Id => !string.IsNullOrEmpty(string_id) ? string_id : id;
    public int NumericId => numericId;
}

[Serializable]
public class EventEffectData
{
    public EventRewardType rewardType;
    public int amount;
    public int count;
    public int choiceCount;
    public int escalatePerUse;
    public MaterialEnum material;
    public string modifierId;
}

[Serializable]
public class EventOptionData
{
    public string id;
    public string titleKey;
    public string recipe;
    public int randomRecipeLength;
    public bool ignoreOrder;
    public int resultId;
    public bool isExitOption;
    public string nextNodeId;
    public int choiceCount;
    public string[] tagIds = Array.Empty<string>();
    public EventEffectData[] effects = Array.Empty<EventEffectData>();
}

[Serializable]
public class EventNodeData
{
    public string id;
    public string[] textKeys = Array.Empty<string>();
    public string nextNodeId;
    public EventOptionData[] options = Array.Empty<EventOptionData>();
}

[Serializable]
public class EventData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public string titleKey;
    public string startNodeId;
    public string defaultEndNodeId;
    public int drawCount = -1;
    public EventNodeData[] nodes = Array.Empty<EventNodeData>();

    public string Id => id;
    public int NumericId => numericId;
}

[Serializable]
public class LevelEnemyData
{
    public int enemyId;
    public float x;
    public float y;
}

[Serializable]
public class LevelEnemyGroupData
{
    public LevelEnemyData[] enemies = Array.Empty<LevelEnemyData>();
}

[Serializable]
public class LevelData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public string titleKey;
    public LevelType levelType;
    public LevelEnemyData[] enemies = Array.Empty<LevelEnemyData>();
    public LevelEnemyGroupData[] randomEnemyGroups = Array.Empty<LevelEnemyGroupData>();
    public int[] enemyIds = Array.Empty<int>();
    public int rewardPoolId;
    public int eventPoolId;
    public int bonusLevelId;
    public string[] restTextKeys = Array.Empty<string>();
    public int restHealAmount;

    public string Id => id;
    public int NumericId => numericId;
}

[Serializable]
public class RewardPoolData : INumericDataRecord
{
    public int numericId;
    public string id;
    public int[] magicIds = Array.Empty<int>();

    public int NumericId => numericId;
}

[Serializable]
public class BonusRewardData
{
    public BonusRewardType rewardType;
    public string rewardName;
    public int amount;
    public string texturePath;
}

[Serializable]
public class BonusLevelData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public int radius = 2;
    public int drawCount = 5;
    public BonusRewardData[] rewards = Array.Empty<BonusRewardData>();

    public string Id => id;
    public int NumericId => numericId;
}

[Serializable]
public class ChapterLevelPoolRangeData
{
    public int startProgress;
    public int endProgress;
    public int[] levelPoolIds = Array.Empty<int>();
}

[Serializable]
public class ChapterFixedLevelData
{
    public int levelIndex;
    public int levelId;
    public LevelType levelType;
}

[Serializable]
public class ChapterData : INumericDataRecord
{
    public int numericId;
    public string id;
    public string nameKey;
    public int levelLength;
    public int mapWidth = 5;
    public int mapHeight = 5;
    public int startMapX = 2;
    public int startMapY = 2;
    public float hiddenLevelWeight;
    public int battleMapLevelWeight = 10;
    public int eliteMapLevelWeight = 10;
    public int eventMapLevelWeight = 4;
    public int restMapLevelWeight = 2;
    public int rewardMapLevelWeight = 2;
    public int defaultMapLevelWeight = 4;
    public int shopMapLevelWeight = 2;
    public int removeMapLevelWeight = 2;
    public int addMapLevelWeight = 2;
    public int[] BeginPool = Array.Empty<int>();
    public int[] MidPool = Array.Empty<int>();
    public int[] NormalPool = Array.Empty<int>();
    public int[] EventPool = Array.Empty<int>();
    public int[] ElitePool = Array.Empty<int>();
    public int[] BossPool = Array.Empty<int>();
    public int[] levelPoolIds = Array.Empty<int>();
    public ChapterLevelPoolRangeData[] levelPoolRanges = Array.Empty<ChapterLevelPoolRangeData>();
    public ChapterFixedLevelData[] fixed_level = Array.Empty<ChapterFixedLevelData>();
    public int[] eventPoolIds = Array.Empty<int>();

    public int NumericId => numericId;
}

[Serializable]
public class MaterialModel
{
    public string instanceId;
    public MaterialEnum material;
    public MaterialEnum alternateMaterial;
    public List<string> enhancementIds = new List<string>();
    public List<MaterialModifierModel> modifiers = new List<MaterialModifierModel>();
    public List<MaterialModel> linkedCards = new List<MaterialModel>();
    public List<MaterialModel> packedCards = new List<MaterialModel>();
    public bool isPlayed;
    public bool isTemporary;
    public bool isRetained;
    public bool removeCardAfterBattle;

    public MaterialModel(string instanceId, MaterialEnum material)
    {
        this.instanceId = instanceId;
        this.material = material;
    }

    public bool CanActAs(MaterialEnum targetMaterial)
    {
        if (targetMaterial == MaterialEnum.None)
            return material == MaterialEnum.None;

        if (material == targetMaterial || alternateMaterial == targetMaterial || HasDefaultWildBehavior())
            return true;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].CanActAs(targetMaterial))
                return true;
        }

        return false;
    }

    private bool HasDefaultWildBehavior()
    {
        if (material != MaterialEnum.Wild && alternateMaterial != MaterialEnum.Wild)
            return false;

        if (linkedCards.Count > 0 || packedCards.Count > 0)
            return false;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].SuppressesDefaultWildBehavior())
                return false;
        }
        return true;
    }

    public bool IsArrowReadable()
    {
        bool readable = material != MaterialEnum.None;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null)
                readable = modifiers[i].IsArrowReadable(readable);
        }
        return readable;
    }

    public MaterialEnum GetArrowDisplayMaterial()
    {
        MaterialEnum displayMaterial = material;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null)
                displayMaterial = modifiers[i].GetArrowDisplayMaterial(displayMaterial);
        }
        return displayMaterial;
    }

    public void FillArrowBaseEffectDirections(ArrowReadStep step)
    {
        if (step == null)
            return;

        bool usesBaseEffect = material != MaterialEnum.None;
        if (HasDefaultWildBehavior() || IsLinkedArrowContainer())
            usesBaseEffect = false;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null)
                usesBaseEffect = modifiers[i].UsesArrowBaseEffect(usesBaseEffect);
        }

        if (usesBaseEffect)
            step.AddBaseEffectDirection(material);
        if (HasDefaultWildBehavior())
        {
            step.AddBaseEffectDirection(MaterialEnum.Fire);
            step.AddBaseEffectDirection(MaterialEnum.Water);
            step.AddBaseEffectDirection(MaterialEnum.Wind);
            step.AddBaseEffectDirection(MaterialEnum.Earth);
        }

        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i]?.FillArrowBaseEffectDirections(step);
    }

    public int GetArrowMatchTokenCount()
    {
        int tokenCount = 1;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null)
                tokenCount = modifiers[i].GetArrowMatchTokenCount(tokenCount);
        }
        return tokenCount < 0 ? 0 : tokenCount;
    }

    public int GetAdditionalArrowReadCount()
    {
        int readCount = 0;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null)
                readCount += modifiers[i].GetAdditionalArrowReadCount();
        }
        return readCount < 0 ? 0 : readCount;
    }

    public void TriggerBeforeArrowRead(ArrowReadContext context)
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i]?.OnBeforeArrowRead(context);
    }

    public void TriggerOnArrowBaseEffectResolve(ArrowReadContext context)
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i]?.OnArrowBaseEffectResolve(context);
    }

    public bool ShouldStopArrowReadSequence()
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].ShouldStopArrowReadSequence())
                return true;
        }
        return false;
    }

    public bool ShouldPackFollowingArrows()
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].ShouldPackFollowingArrows())
                return true;
        }
        return false;
    }

    public bool IsLinkedArrowContainer()
    {
        if (linkedCards.Count > 0 || packedCards.Count > 0)
            return true;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].IsLinkedArrowContainer())
                return true;
        }
        return false;
    }

    public void SetPackedCards(IEnumerable<MaterialModel> cards)
    {
        packedCards.Clear();
        if (cards == null)
            return;

        foreach (MaterialModel card in cards)
        {
            if (card != null)
                packedCards.Add(card);
        }
    }

    public void ClearPackedCards()
    {
        packedCards.Clear();
        for (int i = 0; i < linkedCards.Count; i++)
            linkedCards[i]?.ClearPackedCards();
    }

    public IReadOnlyList<MaterialModel> GetArrowLinkedCards()
    {
        return packedCards.Count > 0 ? packedCards : linkedCards;
    }

    public bool ShouldRemoveSourceAfterArrowRead()
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].ShouldRemoveSourceAfterArrowRead())
                return true;
        }
        return false;
    }

    public ArrowReadAfterReadAction GetArrowAfterReadAction()
    {
        ArrowReadAfterReadAction action = ArrowReadAfterReadAction.None;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] == null)
                continue;

            ArrowReadAfterReadAction modifierAction = modifiers[i].GetArrowAfterReadAction();
            if (modifierAction != ArrowReadAfterReadAction.None)
                action = modifierAction;
        }
        return action;
    }

    public ArrowReadDirectionChange GetArrowReadDirectionChange()
    {
        ArrowReadDirectionChange change = ArrowReadDirectionChange.None;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] == null)
                continue;

            ArrowReadDirectionChange modifierChange = modifiers[i].GetArrowReadDirectionChange();
            if (modifierChange != ArrowReadDirectionChange.None)
                change = modifierChange;
        }
        return change;
    }

    public bool ShouldRemoveAfterBattle()
    {
        if (removeCardAfterBattle)
            return true;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] != null && modifiers[i].RemoveCardAfterBattle)
                return true;
        }
        return false;
    }

    public void RemoveBattleOnlyModifiers()
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i] != null && modifiers[i].RemoveModifierAfterBattle)
                modifiers.RemoveAt(i);
        }
        RebuildModifierFlags();
    }

    public MaterialModel CloneForBattle(string newInstanceId)
    {
        MaterialModel clone = new MaterialModel(newInstanceId, material)
        {
            alternateMaterial = alternateMaterial,
            isTemporary = isTemporary,
            isRetained = isRetained,
            removeCardAfterBattle = removeCardAfterBattle
        };
        clone.enhancementIds.AddRange(enhancementIds);
        for (int i = 0; i < linkedCards.Count; i++)
        {
            MaterialModel linkedCard = linkedCards[i]?.CloneForBattle(newInstanceId + "_linked_" + i);
            if (linkedCard != null)
                clone.linkedCards.Add(linkedCard);
        }
        for (int i = 0; i < modifiers.Count; i++)
        {
            MaterialModifierModel modifier = modifiers[i]?.Clone();
            if (modifier != null)
                clone.AddModifier(modifier);
        }
        return clone;
    }
    public void AddModifier(MaterialModifierModel modifier)
    {
        if (modifier == null || modifiers.Contains(modifier))
            return;

        modifier.model = this;
        modifiers.Add(modifier);
        RebuildModifierFlags();
    }

    private void RebuildModifierFlags()
    {
        isTemporary = false;
        isRetained = false;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i] is TemporaryModifier)
                isTemporary = true;
            if (modifiers[i] is RetainedArrowModifier)
                isRetained = true;
        }
    }

    public void TriggerOnDraw()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnDraw();
    }

    public void TriggerOnBegin()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnBegin();
    }

    public void TriggerOnJoin()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnJoin();
    }

    public void TriggerOnEnd()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnEnd();
    }

    public void TriggerOnDiscard()
    {
        isPlayed = false;
        for (int i = modifiers.Count - 1; i >= 0; i--)
            modifiers[i].OnDiscard();
    }

    public void TriggerOnPlayedDiscard()
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
            modifiers[i].OnPlayedDiscard();
    }

    public void TriggerOnRefresh()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnRefresh();
    }

    public void TriggerOnInvoke()
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnInvoke();
    }

    public void TriggerOnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
    {
        for (int i = 0; i < modifiers.Count; i++)
            modifiers[i].OnTokenInvoke(token, step);
    }

    public void RemoveModifiers<T>() where T : MaterialModifierModel
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i] is T)
                modifiers.RemoveAt(i);
        }
        RebuildModifierFlags();
    }
}

[Serializable]
public class MaterialCardModel : MaterialModel
{
    public MaterialCardModel(string instanceId, MaterialEnum material) : base(instanceId, material)
    {
    }
}
