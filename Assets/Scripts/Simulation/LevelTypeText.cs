/// <summary>Shared localized level labels; UI and history summaries use the same mapping.</summary>
public static class LevelTypeText
{
    public static string GetName(LevelType type)
    {
        return type switch
        {
            LevelType.Shop => LocalizationSystem.GetText("ui.level_type.shop", "商店"),
            LevelType.Event => LocalizationSystem.GetText("ui.level_type.event", "事件"),
            LevelType.RemoveMaterial => LocalizationSystem.GetText("ui.level_type.remove_material", "删除"),
            LevelType.AddMaterial => LocalizationSystem.GetText("ui.level_type.add_material", "新增"),
            LevelType.Rest => LocalizationSystem.GetText("ui.level_type.rest", "休息"),
            LevelType.Reward => LocalizationSystem.GetText("ui.level_type.reward", "奖励"),
            LevelType.Elite => LocalizationSystem.GetText("ui.level_type.elite", "精英"),
            _ => LocalizationSystem.GetText("ui.level_type.battle", "战斗"),
        };
    }
}
