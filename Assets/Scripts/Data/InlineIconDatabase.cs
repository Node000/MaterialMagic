using System.Collections.Generic;

public static class InlineIconDatabase
{
    private static Dictionary<string, InlineIconData> dataById;

    public static bool TryGet(string id, out InlineIconData data)
    {
        EnsureLoaded();
        return dataById.TryGetValue(id, out data);
    }

    public static void ClearCache()
    {
        dataById = null;
    }

    private static void EnsureLoaded()
    {
        if (dataById != null)
            return;

        dataById = GameDataReader.LoadDictionary<InlineIconData>("InlineIconData");
    }
}
