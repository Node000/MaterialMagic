using System;
using System.Collections.Generic;

public class EventModel
{
    private const string ImplicitDefaultEndNodeId = "default_end";
    private readonly Dictionary<string, EventNodeData> nodes = new Dictionary<string, EventNodeData>();
    private readonly Dictionary<string, int> optionResolveCounts = new Dictionary<string, int>();
    private EventOptionData defaultEndOption;

    public EventData Data { get; }
    public EventNodeData CurrentNode { get; private set; }

    public string Id => Data.id;
    public string Title => LocalizationSystem.GetText(Data.titleKey, Data.id);
    public string[] CurrentTexts => CurrentNode != null ? CurrentNode.textKeys : Array.Empty<string>();
    public EventOptionData[] CurrentOptions => CurrentNode != null && CurrentNode.options != null ? CurrentNode.options : Array.Empty<EventOptionData>();

    public EventModel(EventData data)
    {
        Data = data;
        if (data.nodes != null)
        {
            for (int i = 0; i < data.nodes.Length; i++)
            {
                EventNodeData node = data.nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.id))
                    nodes[node.id] = node;
            }
        }

        if (!string.IsNullOrEmpty(data.startNodeId) && nodes.TryGetValue(data.startNodeId, out EventNodeData startNode))
            CurrentNode = startNode;
        else if (data.nodes != null && data.nodes.Length > 0)
            CurrentNode = data.nodes[0];

        RandomizeRecipes();
    }

    public static EventModel CreateTextOnly(string id, string titleKey, string[] textKeys)
    {
        EventData data = new EventData
        {
            id = id,
            titleKey = titleKey,
            startNodeId = "start",
            nodes = new[]
            {
                new EventNodeData
                {
                    id = "start",
                    textKeys = textKeys != null && textKeys.Length > 0 ? textKeys : Array.Empty<string>(),
                    options = Array.Empty<EventOptionData>()
                }
            }
        };
        return new EventModel(data);
    }

    public bool TryGetMatchedOption(IReadOnlyList<MaterialModel> sequence, out EventOptionData option)
    {
        option = null;
        EventOptionData[] options = CurrentOptions;
        for (int i = 0; i < options.Length; i++)
        {
            EventOptionData currentOption = options[i];
            if (currentOption != null && currentOption.isExitOption)
                continue;
            if (IsOptionMatch(currentOption, sequence))
            {
                option = currentOption;
                return true;
            }
        }

        return false;
    }

    public bool TryGetExitOption(out EventOptionData option)
    {
        option = null;
        EventOptionData[] options = CurrentOptions;
        for (int i = 0; i < options.Length; i++)
        {
            EventOptionData currentOption = options[i];
            if (currentOption != null && currentOption.isExitOption)
            {
                option = currentOption;
                return true;
            }
        }
        return false;
    }

    public bool TryGetDefaultEndOption(out EventOptionData option)
    {
        option = null;
        if (CurrentNode == null || CurrentOptions.Length == 0)
            return false;

        string nodeId = GetDefaultEndNodeId();
        if (string.IsNullOrEmpty(nodeId) || CurrentNode.id == nodeId || !nodes.ContainsKey(nodeId))
            return false;

        if (defaultEndOption == null || defaultEndOption.nextNodeId != nodeId)
        {
            defaultEndOption = new EventOptionData
            {
                id = "default_end",
                isExitOption = true,
                nextNodeId = nodeId
            };
        }

        option = defaultEndOption;
        return true;
    }

    public void MarkOptionResolved(EventOptionData option)
    {
        if (option == null || string.IsNullOrEmpty(option.id))
            return;

        optionResolveCounts.TryGetValue(option.id, out int count);
        optionResolveCounts[option.id] = count + 1;
    }

    public int GetOptionResolveCount(EventOptionData option)
    {
        if (option == null || string.IsNullOrEmpty(option.id))
            return 0;

        optionResolveCounts.TryGetValue(option.id, out int count);
        return count;
    }

    public bool IsOptionMatch(EventOptionData option, IReadOnlyList<MaterialModel> sequence)
    {
        MaterialEnum[] recipe = ParseRecipe(option != null ? option.recipe : null);
        if (option == null || recipe.Length == 0 || sequence == null || recipe.Length != sequence.Count)
            return false;

        if (!option.ignoreOrder)
            return IsOrderedMatch(recipe, sequence);

        return IsUnorderedMatch(recipe, sequence);
    }

    public bool TryAdvanceToNextNode()
    {
        if (CurrentNode == null || string.IsNullOrEmpty(CurrentNode.nextNodeId))
            return false;

        if (!nodes.TryGetValue(CurrentNode.nextNodeId, out EventNodeData nextNode))
            return false;

        CurrentNode = nextNode;
        GameLog.Data($"Event {Id} advance node={nextNode.id}");
        return true;
    }

    public bool AdvanceToNextNode(EventOptionData option)
    {
        if (option == null || string.IsNullOrEmpty(option.nextNodeId))
            return false;

        if (!nodes.TryGetValue(option.nextNodeId, out EventNodeData nextNode))
            return false;

        CurrentNode = nextNode;
        GameLog.Data($"Event {Id} advance node={nextNode.id}");
        return true;
    }

    public void ResolveResult(int resultId, PlayerState playerState)
    {
        switch (resultId)
        {
            case 1:
                playerState.Heal(10);
                GameLog.Data("Event result heal player 10");
                break;
            case 2:
                playerState.DrawCount++;
                GameLog.Data($"Event result player draw count +1 now={playerState.DrawCount}");
                break;
            case 101:
                playerState.AddDeckMaterial(MaterialEnum.Fire);
                break;
            case 102:
                playerState.AddDeckMaterial(MaterialEnum.Wind);
                break;
            case 103:
                playerState.AddDeckMaterial(MaterialEnum.Water);
                break;
            case 104:
                playerState.AddDeckMaterial(MaterialEnum.Earth);
                break;
        }
    }

    public static MaterialModifierModel CreateModifierForResult(int resultId)
    {
        switch (resultId)
        {
            case 201:
                return new KindlingModifier();
            case 202:
                return new FlowModifier();
            case 203:
                return new LiquefyModifier();
            default:
                return null;
        }
    }

    public static MaterialEnum[] ParseRecipe(string recipe)
    {
        if (string.IsNullOrEmpty(recipe))
            return Array.Empty<MaterialEnum>();

        List<MaterialEnum> materials = new List<MaterialEnum>(recipe.Length);
        for (int i = 0; i < recipe.Length; i++)
        {
            int value = recipe[i] - '0';
            if (value <= 0)
                continue;

            materials.Add((MaterialEnum)value);
        }

        return materials.ToArray();
    }

    public static string GetRecipeDisplay(EventOptionData option)
    {
        MaterialEnum[] materials = ParseRecipe(option != null ? option.recipe : null);
        if (materials.Length == 0)
            return MaterialCardView.GetMaterialName(MaterialEnum.None);

        string result = string.Empty;
        for (int i = 0; i < materials.Length; i++)
        {
            if (i > 0)
                result += "+";
            result += MaterialCardView.GetMaterialName(materials[i]);
        }

        return result;
    }

    private void RandomizeRecipes()
    {
        if (Data.nodes == null)
            return;

        for (int nodeIndex = 0; nodeIndex < Data.nodes.Length; nodeIndex++)
        {
            EventNodeData node = Data.nodes[nodeIndex];
            if (node == null || node.options == null)
                continue;

            HashSet<string> usedRecipeKeys = new HashSet<string>();
            for (int optionIndex = 0; optionIndex < node.options.Length; optionIndex++)
            {
                EventOptionData option = node.options[optionIndex];
                if (option == null || option.isExitOption || option.randomRecipeLength > 0 || string.IsNullOrEmpty(option.recipe))
                    continue;

                usedRecipeKeys.Add(CreateRecipeKey(option.recipe, option.ignoreOrder));
            }

            for (int optionIndex = 0; optionIndex < node.options.Length; optionIndex++)
            {
                EventOptionData option = node.options[optionIndex];
                if (option == null || option.randomRecipeLength <= 0)
                    continue;

                option.recipe = CreateUniqueRandomRecipe(option.randomRecipeLength, option.ignoreOrder, usedRecipeKeys);
                usedRecipeKeys.Add(CreateRecipeKey(option.recipe, option.ignoreOrder));
            }
        }
    }

    private static string CreateUniqueRandomRecipe(int length, bool ignoreOrder, HashSet<string> usedRecipeKeys)
    {
        if (usedRecipeKeys == null)
            return CreateRandomRecipe(length);

        List<string> candidates = new List<string>();
        HashSet<string> candidateKeys = new HashSet<string>();
        CollectUnusedRecipeCandidates(Math.Max(1, length), ignoreOrder, usedRecipeKeys, candidateKeys, new char[Math.Max(1, length)], 0, candidates);
        if (candidates.Count == 0)
            return CreateRandomRecipe(length);

        return candidates[NextRunRandomInt(0, candidates.Count)];
    }

    private static void CollectUnusedRecipeCandidates(int length, bool ignoreOrder, HashSet<string> usedRecipeKeys, HashSet<string> candidateKeys, char[] buffer, int index, List<string> candidates)
    {
        if (index >= length)
        {
            string recipe = new string(buffer);
            string key = CreateRecipeKey(recipe, ignoreOrder);
            if (!usedRecipeKeys.Contains(key) && candidateKeys.Add(key))
                candidates.Add(recipe);
            return;
        }

        for (char material = '1'; material <= '4'; material++)
        {
            buffer[index] = material;
            CollectUnusedRecipeCandidates(length, ignoreOrder, usedRecipeKeys, candidateKeys, buffer, index + 1, candidates);
        }
    }

    private static string CreateRecipeKey(string recipe, bool ignoreOrder)
    {
        if (string.IsNullOrEmpty(recipe) || !ignoreOrder)
            return recipe ?? string.Empty;

        char[] chars = recipe.ToCharArray();
        Array.Sort(chars);
        return new string(chars);
    }

    private static string CreateRandomRecipe(int length)
    {
        char[] recipe = new char[Math.Max(1, length)];
        for (int i = 0; i < recipe.Length; i++)
            recipe[i] = (char)('0' + NextRunRandomInt(1, 5));
        return new string(recipe);
    }

    private static int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return RunManager.Current != null ? RunManager.Current.NextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private string GetDefaultEndNodeId()
    {
        if (!string.IsNullOrEmpty(Data.defaultEndNodeId))
            return Data.defaultEndNodeId;

        return nodes.ContainsKey(ImplicitDefaultEndNodeId) ? ImplicitDefaultEndNodeId : null;
    }

    private static bool IsOrderedMatch(MaterialEnum[] recipe, IReadOnlyList<MaterialModel> sequence)
    {
        for (int i = 0; i < recipe.Length; i++)
        {
            MaterialModel card = sequence[i];
            if (card == null || !card.CanActAs(recipe[i]))
                return false;
        }

        return true;
    }

    private static bool IsUnorderedMatch(MaterialEnum[] recipe, IReadOnlyList<MaterialModel> sequence)
    {
        bool[] used = new bool[sequence.Count];

        for (int recipeIndex = 0; recipeIndex < recipe.Length; recipeIndex++)
        {
            bool found = false;
            for (int cardIndex = 0; cardIndex < sequence.Count; cardIndex++)
            {
                if (used[cardIndex])
                    continue;

                MaterialModel card = sequence[cardIndex];
                if (card == null || !card.CanActAs(recipe[recipeIndex]))
                    continue;

                used[cardIndex] = true;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }
}
