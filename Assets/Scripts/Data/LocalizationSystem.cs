using System;
using System.Collections.Generic;
using UnityEngine;

public enum MaterialModifierDisplayKind
{
    Temporary,
    Charge,
    Vortex,
    Kindling,
    Flow,
    Liquefy
}

public static class LocalizationKeys
{
    public static string GetMaterialName(MaterialEnum material)
    {
        return LocalizationSystem.GetText("material." + GetMaterialKey(material) + ".name", material.ToString());
    }

    public static string GetBuffName(BuffEnum buffType)
    {
        return LocalizationSystem.GetText("buff." + GetBuffKey(buffType) + ".name", buffType.ToString());
    }

    public static string GetBuffDescription(BuffEnum buffType)
    {
        return LocalizationSystem.GetText("buff." + GetBuffKey(buffType) + ".desc", string.Empty);
    }

    public static string GetModifierName(object modifier)
    {
        if (modifier == null)
            return string.Empty;

        if (MaterialModifierDisplayDatabase.TryGetName(modifier, out string name))
            return name;

        return LocalizationSystem.GetText("modifier." + GetModifierKey(modifier) + ".name", modifier.GetType().Name);
    }

    public static string GetModifierDescription(object modifier)
    {
        if (modifier == null)
            return string.Empty;

        if (MaterialModifierDisplayDatabase.TryGetDescription(modifier, out string description))
            return description;

        return LocalizationSystem.GetText("modifier." + GetModifierKey(modifier) + ".desc", string.Empty);
    }

    public static string GetModifierName(MaterialModifierDisplayKind kind)
    {
        return LocalizationSystem.GetText("modifier." + GetModifierKey(kind) + ".name", kind.ToString());
    }

    public static string GetModifierDescription(MaterialModifierDisplayKind kind)
    {
        return LocalizationSystem.GetText("modifier." + GetModifierKey(kind) + ".desc", string.Empty);
    }

    public static string GetTagName(TagData tag)
    {
        if (tag == null || string.IsNullOrEmpty(tag.nameKey))
            return string.Empty;

        return LocalizationSystem.GetText(tag.nameKey, string.Empty);
    }

    public static string GetTagDescription(TagData tag)
    {
        if (tag == null || string.IsNullOrEmpty(tag.descriptionKey))
            return string.Empty;

        return LocalizationSystem.GetText(tag.descriptionKey, string.Empty);
    }

    private static string GetMaterialKey(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return "fire";
            case MaterialEnum.Wind: return "wind";
            case MaterialEnum.Water: return "water";
            case MaterialEnum.Earth: return "earth";
            case MaterialEnum.Wild: return "wild";
            default: return "none";
        }
    }

    private static string GetBuffKey(BuffEnum buffType)
    {
        switch (buffType)
        {
            case BuffEnum.Shield: return "shield";
            case BuffEnum.Burning: return "burning";
            case BuffEnum.Weak: return "weak";
            case BuffEnum.Vulnerable: return "vulnerable";
            case BuffEnum.Slow: return "slow";
            case BuffEnum.Arc: return "arc";
            case BuffEnum.SpellPower: return "spell_power";
            case BuffEnum.DefensePower: return "defense_power";
            case BuffEnum.BurningNextTurn: return "burning_next_turn";
            case BuffEnum.BurnOnAttack: return "burn_on_attack";
            case BuffEnum.RepeatSpell: return "repeat_spell";
            case BuffEnum.DebuffPower: return "debuff_power";
            case BuffEnum.VortexNextDraw: return "vortex_next_draw";
            case BuffEnum.ShieldReflect: return "shield_reflect";
            case BuffEnum.ExtraDraw: return "extra_draw";
            case BuffEnum.ExtraRefresh: return "extra_refresh";
            case BuffEnum.Sturdy: return "sturdy";
            case BuffEnum.Stable: return "stable";
            case BuffEnum.Disorder: return "disorder";
            case BuffEnum.KindlingNextDraw: return "kindling_next_draw";
            case BuffEnum.DrawOnEnemyAttack: return "draw_on_enemy_attack";
            case BuffEnum.BurningOnEnemyAttack: return "burning_on_enemy_attack";
            case BuffEnum.ExtraDrawOnEnemyDamage: return "extra_draw_on_enemy_damage";
            case BuffEnum.ShieldReflectBoost: return "shield_reflect_boost";
            case BuffEnum.MagicAttackAll: return "magic_attack_all";
            case BuffEnum.NextMagicRepeat: return "next_magic_repeat";
            case BuffEnum.KeepHand: return "keep_hand";
            case BuffEnum.RetainedNextDraw: return "retained_next_draw";
            case BuffEnum.DoubleEnemyBurningOnTurnEnd: return "double_enemy_burning_on_turn_end";
            case BuffEnum.ExtraEnemyDebuff: return "extra_enemy_debuff";
            case BuffEnum.MaterialBaseEffectRepeat: return "material_base_effect_repeat";
            case BuffEnum.KeepShieldNextTurn: return "keep_shield_next_turn";
            case BuffEnum.BurningDamageShieldNextTurn: return "burning_damage_shield_next_turn";
            case BuffEnum.SpellPowerOnExtraDraw: return "spell_power_on_extra_draw";
            case BuffEnum.WeakOnEnemyAttack: return "weak_on_enemy_attack";
            case BuffEnum.TemporaryWindOnMaterialConsumed: return "temporary_wind_on_material_consumed";
            case BuffEnum.WeakNextTurn: return "weak_next_turn";
            case BuffEnum.FoamShield: return "foam_shield";
            case BuffEnum.LazyNextDraw: return "lazy_next_draw";
            case BuffEnum.ChargeNextDraw: return "charge_next_draw";
            case BuffEnum.TutorialDeath: return "tutorial_death";
            case BuffEnum.Claw: return "claw";
            default: return buffType.ToString().ToLowerInvariant();
        }
    }

    private static string GetModifierKey(object modifier)
    {
        if (modifier is TemporaryModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Temporary);
        if (modifier is ChargeModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Charge);
        if (modifier is VortexModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Vortex);
        if (modifier is KindlingModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Kindling);
        if (modifier is FlowModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Flow);
        if (modifier is LiquefyModifier)
            return GetModifierKey(MaterialModifierDisplayKind.Liquefy);
        if (modifier is SturdyModifier)
            return "sturdy";
        if (modifier is DoomModifier)
            return "doom";
        if (modifier is LazyModifier)
            return "lazy";
        if (modifier is RepeatArrowModifier)
            return "repeat_arrow";
        if (modifier is EternalArrowModifier)
            return "eternal_arrow";
        if (modifier is FragileArrowModifier)
            return "fragile_arrow";
        if (modifier is RetainedArrowModifier)
            return "retained_arrow";
        if (modifier is HalfArrowModifier)
            return "half_arrow";

        return modifier != null ? modifier.GetType().Name.ToLowerInvariant() : string.Empty;
    }

    private static string GetModifierKey(MaterialModifierDisplayKind kind)
    {
        switch (kind)
        {
            case MaterialModifierDisplayKind.Temporary: return "temporary";
            case MaterialModifierDisplayKind.Charge: return "charge";
            case MaterialModifierDisplayKind.Vortex: return "vortex";
            case MaterialModifierDisplayKind.Kindling: return "kindling";
            case MaterialModifierDisplayKind.Flow: return "flow";
            case MaterialModifierDisplayKind.Liquefy: return "liquefy";
            default: return kind.ToString().ToLowerInvariant();
        }
    }
}

[Serializable]
public class LocalizationEntry
{
    public string key;
    public string text;
}

[Serializable]
public class LocalizationTable
{
    public List<LocalizationEntry> items = new List<LocalizationEntry>();
}

public static class LocalizationSystem
{
    private const string LocalizationRoot = "Data/Localization/";
    private const string DefaultLanguage = "zh-CN";
    private const string LanguagePrefsKey = "localization.language";
    private static readonly string[] SupplementalLanguageTables = { "_UI", "_Tutorial", "_Buff", "_Material", "_Modifier", "_MagicModifier", "_Enemy", "_Event", "_Tag" };
    private static readonly string[] LanguageCodes = { "zh-CN", "en-US" };
    private static readonly string[] LanguageDisplayNames = { "简体中文", "English" };
    private static readonly Dictionary<string, string> TextByKey = new Dictionary<string, string>();
    private static bool initialized;
    private static bool languageLoaded;

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;
    public static int LanguageCount => LanguageCodes.Length;
    public static event Action LanguageChanged;

    public static void Initialize()
    {
        EnsureInitialized();
    }

    public static string GetLanguageCode(int index)
    {
        return index >= 0 && index < LanguageCodes.Length ? LanguageCodes[index] : DefaultLanguage;
    }

    public static string GetLanguageDisplayName(int index)
    {
        return index >= 0 && index < LanguageDisplayNames.Length ? LanguageDisplayNames[index] : GetLanguageCode(index);
    }

    public static int GetCurrentLanguageIndex()
    {
        EnsureInitialized();
        for (int i = 0; i < LanguageCodes.Length; i++)
        {
            if (LanguageCodes[i] == CurrentLanguage)
                return i;
        }
        return 0;
    }

    public static void SetLanguage(string languageCode)
    {
        EnsureInitialized();
        string normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        if (languageLoaded && CurrentLanguage == normalizedLanguageCode)
            return;

        LoadLanguage(normalizedLanguageCode);
        PlayerPrefs.SetString(LanguagePrefsKey, normalizedLanguageCode);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static void LoadLanguage(string languageCode)
    {
        TextByKey.Clear();
        CurrentLanguage = NormalizeLanguageCode(languageCode);
        languageLoaded = true;

        if (CurrentLanguage != DefaultLanguage)
            LoadLanguageTables(DefaultLanguage);
        LoadLanguageTables(CurrentLanguage);
    }

    private static void LoadLanguageTables(string languageCode)
    {
        LoadLanguageAsset(LocalizationRoot + languageCode);
        for (int i = 0; i < SupplementalLanguageTables.Length; i++)
            LoadLanguageAsset(LocalizationRoot + languageCode + SupplementalLanguageTables[i]);
    }

    private static void LoadLanguageAsset(string path)
    {
        TextAsset asset = GameDataReader.LoadTextAsset(path);
        if (asset == null)
            return;

        LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(asset.text);
        if (table == null || table.items == null)
            return;

        for (int i = 0; i < table.items.Count; i++)
        {
            LocalizationEntry entry = table.items[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;

            TextByKey[entry.key] = entry.text;
        }
    }

    public static string GetText(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        EnsureLanguageLoaded();
        return TextByKey.TryGetValue(key, out string text) ? text : key;
    }

    public static string GetText(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return fallback;

        EnsureLanguageLoaded();
        return TextByKey.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text) ? text : fallback;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        string savedLanguage = PlayerPrefs.GetString(LanguagePrefsKey, DefaultLanguage);
        LoadLanguage(savedLanguage);
    }

    private static void EnsureLanguageLoaded()
    {
        EnsureInitialized();
        if (!languageLoaded)
            LoadLanguage(CurrentLanguage);
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return DefaultLanguage;

        for (int i = 0; i < LanguageCodes.Length; i++)
        {
            if (LanguageCodes[i] == languageCode)
                return languageCode;
        }
        return DefaultLanguage;
    }
}

[Serializable]
public struct LocalizedText
{
    public string key;
    public string fallback;

    public string Value => LocalizationSystem.GetText(key, fallback);

    public LocalizedText(string key, string fallback)
    {
        this.key = key;
        this.fallback = fallback;
    }
}
