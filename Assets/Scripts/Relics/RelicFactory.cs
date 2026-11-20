public static class RelicFactory
{
    public static RelicModel Create(RelicData data)
    {
        if (data == null || data.numericId <= 0 || data.value < 0 || data.threshold < 0) return null;
        switch (data.script)
        {
            case nameof(OpeningShieldRelicModel): return new OpeningShieldRelicModel(data);
            case nameof(ArrowPulseRelicModel): return new ArrowPulseRelicModel(data);
            case nameof(FirstDrawRelicModel): return new FirstDrawRelicModel(data);
            case nameof(RetaliationRelicModel): return new RetaliationRelicModel(data);
            case nameof(GoldBonusRelicModel): return new GoldBonusRelicModel(data);
            case nameof(TurnBurnRelicModel): return new TurnBurnRelicModel(data);
            case nameof(BuffStudyRelicModel): return new BuffStudyRelicModel(data);
            case nameof(FirstMagicPowerRelicModel): return new FirstMagicPowerRelicModel(data);
            case nameof(EliteHealingRelicModel): return new EliteHealingRelicModel(data);
            case nameof(PeriodicTemporaryArrowRelicModel):
                return PeriodicTemporaryArrowRelicModel.IsValid(data) ? new PeriodicTemporaryArrowRelicModel(data) : null;
            default: return null;
        }
    }
}
