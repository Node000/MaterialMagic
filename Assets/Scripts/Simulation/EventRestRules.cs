using System;

/// <summary>Shared rest node definition/recipe generation; its effects are resolved by EventResolutionSession.</summary>
public static class EventRestRules
{
    public const int HealResultId = 300;
    public const int StudyResultId = 301;
    public const int ArrowModifierResultId = 302;
    public const float HealRatio = 0.3f;

    public static EventModel CreateModel(LevelData level, int drawCount, Func<int, int, int> nextRandomInt)
    {
        if (nextRandomInt == null) throw new ArgumentNullException(nameof(nextRandomInt));
        string studyRecipe = nextRandomInt(1, 5).ToString();
        string arrowRecipe;
        do { arrowRecipe = nextRandomInt(1, 5).ToString(); } while (arrowRecipe == studyRecipe);
        string[] text = level?.restTextKeys ?? Array.Empty<string>();
        string startText = text.Length > 0 ? text[0] : string.Empty;
        string endText = text.Length > 1 ? text[1] : startText;
        return new EventModel(new EventData
        {
            id = level != null ? level.id : "rest", titleKey = level != null ? level.titleKey : "level.rest_001.title",
            startNodeId = "start", drawCount = drawCount,
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start", textKeys = !string.IsNullOrEmpty(startText) ? new[] { startText } : Array.Empty<string>(),
                    options = new[]
                    {
                        new EventOptionData { id = "default_rest", titleKey = "rest.option.rest", iconName = "休息", resultId = HealResultId, nextNodeId = "rest_result", isExitOption = true },
                        new EventOptionData { id = "study_magic", titleKey = "rest.option.study", iconName = "道具附魔", recipe = studyRecipe, resultId = StudyResultId },
                        new EventOptionData { id = "arrow_modifier", titleKey = "rest.option.arrow_modifier", iconName = "箭头附魔", recipe = arrowRecipe, resultId = ArrowModifierResultId }
                    }
                },
                new EventNodeData { id = "rest_result", textKeys = !string.IsNullOrEmpty(endText) ? new[] { endText } : Array.Empty<string>(), options = Array.Empty<EventOptionData>() }
            }
        }, nextRandomInt);
    }
}
