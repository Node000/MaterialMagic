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
            case "mercury_weak":
                return new MercuryMagicModifierModel(data);
            case "mirror_reverse_recipe":
                return new MirrorMagicModifierModel(data);
            case "rupture_vulnerable":
                return new RuptureMagicModifierModel(data);
            case "midas_gold":
                return new MidasMagicModifierModel(data);
            case "regeneration_heal":
                return new RegenerationMagicModifierModel(data);
            case "bulwark_shield":
                return new BulwarkMagicModifierModel(data);
            default:
                return new GenericMagicModifierModel(data);
        }
    }
}
