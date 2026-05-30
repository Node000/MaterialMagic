public static class MagicModifierFactory
{
    public static MagicModifierModel Create(MagicModifierData data)
    {
        if (data == null)
            return null;

        switch (data.id)
        {
            case "echo_recast":
                return new EchoMagicModifierModel(data);
            case "searing_burning":
                return new SearingMagicModifierModel(data);
            case "pioneer_extra_draw":
                return new PioneerMagicModifierModel(data);
            default:
                return new GenericMagicModifierModel(data);
        }
    }
}
