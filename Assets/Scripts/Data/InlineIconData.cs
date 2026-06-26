using System;

[Serializable]
public class InlineIconData : IDataRecord
{
    public string id;
    public string spriteName;
    public string fallbackText;
    public bool tint;

    public string Id => id;
}
