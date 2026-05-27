using UnityEngine;

public static class GameLog
{
    public static void Data(string message)
    {
        Debug.Log("[GameData] " + message);
    }
}
