public static class MagicFactory
{
    public static MagicModel Create(MagicData data, int slotIndex = 0)
    {
        if (data == null)
            return null;

        switch (data.script)
        {
            case nameof(IgniteMagicModel): return new IgniteMagicModel(data, slotIndex);
            case nameof(FireballArtMagicModel): return new FireballArtMagicModel(data, slotIndex);
            case nameof(FlameBarrierMagicModel): return new FlameBarrierMagicModel(data, slotIndex);
            case nameof(MagmaMagicModel): return new MagmaMagicModel(data, slotIndex);
            case nameof(MoltenMagicModel): return new MoltenMagicModel(data, slotIndex);
            case nameof(ExplosionMagicModel): return new ExplosionMagicModel(data, slotIndex);
            case nameof(BurningHandMagicModel): return new BurningHandMagicModel(data, slotIndex);
            case nameof(FlameDemonMagicModel): return new FlameDemonMagicModel(data, slotIndex);
            case nameof(AirflowMagicModel): return new AirflowMagicModel(data, slotIndex);
            case nameof(WindBladeMagicModel): return new WindBladeMagicModel(data, slotIndex);
            case nameof(BurningWindMagicModel): return new BurningWindMagicModel(data, slotIndex);
            case nameof(LightningMagicModel): return new LightningMagicModel(data, slotIndex);
            case nameof(StormHandMagicModel): return new StormHandMagicModel(data, slotIndex);
            case nameof(CurrentAttachmentMagicModel): return new CurrentAttachmentMagicModel(data, slotIndex);
            case nameof(SandstormMagicModel): return new SandstormMagicModel(data, slotIndex);
            case nameof(GaleMagicModel): return new GaleMagicModel(data, slotIndex);
            case nameof(IcePickMagicModel): return new IcePickMagicModel(data, slotIndex);
            case nameof(SwampMagicModel): return new SwampMagicModel(data, slotIndex);
            case nameof(PoisonFogMagicModel): return new PoisonFogMagicModel(data, slotIndex);
            case nameof(BoilingRainMagicModel): return new BoilingRainMagicModel(data, slotIndex);
            case nameof(BlizzardMagicModel): return new BlizzardMagicModel(data, slotIndex);
            case nameof(TurbidCurrentMagicModel): return new TurbidCurrentMagicModel(data, slotIndex);
            case nameof(TideHandMagicModel): return new TideHandMagicModel(data, slotIndex);
            case nameof(WaterCorrosionMagicModel): return new WaterCorrosionMagicModel(data, slotIndex);
            case nameof(StoneWallMagicModel): return new StoneWallMagicModel(data, slotIndex);
            case nameof(RockfallMagicModel): return new RockfallMagicModel(data, slotIndex);
            case nameof(EarthFireMagicModel): return new EarthFireMagicModel(data, slotIndex);
            case nameof(PetrifyMagicModel): return new PetrifyMagicModel(data, slotIndex);
            case nameof(FloatingMagicModel): return new FloatingMagicModel(data, slotIndex);
            case nameof(ThornBushMagicModel): return new ThornBushMagicModel(data, slotIndex);
            case nameof(RefineMagicModel): return new RefineMagicModel(data, slotIndex);
            case nameof(EarthHandMagicModel): return new EarthHandMagicModel(data, slotIndex);
            default: return new MagicModel(data, slotIndex);
        }
    }
}
