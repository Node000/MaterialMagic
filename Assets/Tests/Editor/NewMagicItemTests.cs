using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class NewMagicItemTests
{
    private static PlayerStatus CreatePlayer(IRuleContentProvider content = null)
        => new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), content);

    private static MagicData Data(string script, params MaterialEnum[] recipe)
        => new MagicData { id = script, script = script, recipe = recipe };

    [TestCase(1, 3, 1)]
    [TestCase(4, 8, 4)]
    public void BlueprintUsesTheExactRightSlotAndCannotChain(int stride, int slots, int right)
    {
        var player = CreatePlayer();
        player.SetMagicSlotLayout(slots, stride);
        var blueprint = MagicFactory.Create(Data(nameof(BlueprintMagicModel)));
        var active = MagicFactory.Create(Data(nameof(StoneWallMagicModel), MaterialEnum.Earth));
        player.SetMagicAtSlot(blueprint, 0);
        player.SetMagicAtSlot(active, right + 1);
        Assert.That(blueprint.GetCombatTrigger(player).Length, Is.Zero, "Do not skip an empty slot.");
        player.ClearMagicSlot(right + 1);
        player.SetMagicAtSlot(active, right);
        Assert.That(blueprint.GetCombatTrigger(player).Source, Is.SameAs(active));
        player.SetMagicAtSlot(MagicFactory.Create(Data(nameof(BlueprintMagicModel))), right);
        Assert.That(blueprint.GetCombatTrigger(player).Length, Is.Zero);
        player.SetMagicAtSlot(MagicFactory.Create(Data(nameof(BarChartMagicModel))), right);
        Assert.That(blueprint.GetCombatTrigger(player).Length, Is.Zero);
        player.ClearMagicSlot(0);
        player.SetMagicAtSlot(blueprint, slots - 1);
        Assert.That(player.GetRightNeighborIndex(slots - 1), Is.EqualTo(-1), "Do not wrap at a row end.");
        Assert.That(blueprint.GetCombatTrigger(player).Length, Is.Zero);
    }

    [Test]
    public void BlueprintCopiesEffectiveRecipeButNotTheOriginalModifier()
    {
        var player = CreatePlayer();
        player.SetMagicSlotLayout(2, 1);
        var blueprint = MagicFactory.Create(Data(nameof(BlueprintMagicModel)));
        var source = MagicFactory.Create(Data(nameof(StoneWallMagicModel), MaterialEnum.Fire));
        var modifier = new RecipeAndShieldModifier();
        Assert.That(source.AddModifier(modifier), Is.True);
        Assert.That(blueprint.AddModifier(new RecipeAndShieldModifier()), Is.False);
        player.SetMagicAtSlot(blueprint, 0);
        player.SetMagicAtSlot(source, 1);
        var card = new MaterialModel("arrow", MaterialEnum.Earth);
        Assert.That(blueprint.IsMatch(new[] { card }, 0, player), Is.True);
        Assert.That(blueprint.IsMatch(new[] { new MaterialModel("wrong", MaterialEnum.Fire) }, 0, player), Is.False);
        var battle = new BattleManager(player);
        try
        {
            battle.AddEnemy(new EnemyModel(new EnemyData { id = "dummy", maxHealth = 100 }));
            var casts = new List<BattleResolutionStep>();
            using (var session = new BattlePlayZoneResolutionSession(player, battle, new[] { card }, player.MagicBook))
                session.RunToCompletion(step => { if (step.Kind == BattleResolutionStepKind.AfterMagic) casts.Add(step); });
            Assert.That(casts.Count, Is.EqualTo(2));
            Assert.That(casts[0].Magic, Is.SameAs(blueprint));
            Assert.That(casts[0].MagicEffectSource, Is.SameAs(source));
            Assert.That(casts[0].TriggerMaterials, Is.EqualTo(new[] { MaterialEnum.Earth }));
            Assert.That(casts[0].CastResult.playerShield, Is.EqualTo(2));
            Assert.That(casts[1].CastResult.playerShield, Is.EqualTo(7));
            Assert.That(battle.ContinuousCastCount, Is.EqualTo(2));
        }
        finally { BattleManager.ClearInstance(battle); }
    }

    [Test]
    public void NoRequirementNeverRegistersACastOrConsumesRepeat()
    {
        var player = CreatePlayer();
        var battle = new BattleManager(player);
        try
        {
            battle.AddEnemy(new EnemyModel(new EnemyData { id = "dummy", maxHealth = 100 }));
            player.AddBuff(BuffEnum.NextMagicRepeat, 1);
            var cards = new[] { new MaterialModel("arrow", MaterialEnum.Earth) };
            var empty = new MagicModel(Data("empty"));
            var price = MagicFactory.Create(Data(nameof(BarChartMagicModel)));
            Assert.That(empty.IsMatch(cards, 0), Is.False);
            using (var session = new BattlePlayZoneResolutionSession(player, battle, cards, new[] { empty, price }))
                session.RunToCompletion();
            price.Cast(player, battle);
            Assert.That(battle.ContinuousCastCount, Is.Zero);
            Assert.That(player.GetBuffStack(BuffEnum.NextMagicRepeat), Is.EqualTo(1));
        }
        finally { BattleManager.ClearInstance(battle); }
    }

    [Test]
    public void BarChartAccumulatesPerInstanceAndRestoresZeroWithoutRollingAgain()
    {
        var data = Data(nameof(BarChartMagicModel));
        data.numericId = 54;
        var content = new RuleContentSnapshot().AddNumeric(new Dictionary<int, MagicData> { [54] = data }).Freeze();
        var player = CreatePlayer(content);
        var run = new RunManager(player);
        var chart = MagicFactory.Create(data);
        player.SetMagicAtSlot(chart, 0);
        int basePrice = ShopOfferGenerator.GetMagicSellPrice(chart, player);
        var room = new LevelData { numericId = 1, levelType = LevelType.Battle };
        var shop = new LevelData { numericId = 2, levelType = LevelType.Shop };
        void EnterWithDelta(int delta)
        {
            int seed = 1;
            while (RunRandom.Range(seed, 0, -2, 3) != delta) seed++;
            player.SetRunRandomState(seed, 0);
            int copies = player.MagicBook.Count;
            Assert.That(run.Session.SelectNode(room).Accepted, Is.True);
            Assert.That(run.Session.SelectNode(room).Accepted, Is.True);
            run.BeginLevel(room);
            Assert.That(player.RunRandomStep, Is.EqualTo(copies));
            Assert.That(run.Session.BattleWon().Accepted, Is.True);
            Assert.That(run.Session.CompleteReward(shop).Accepted, Is.True);
            run.BeginLevel(shop);
            Assert.That(player.RunRandomStep, Is.EqualTo(copies));
            Assert.That(run.Session.CompleteNode().Accepted, Is.True);
        }
        EnterWithDelta(2);
        EnterWithDelta(2);
        Assert.That(ShopOfferGenerator.GetMagicSellPrice(chart, player), Is.EqualTo(basePrice + 4));
        var late = MagicFactory.Create(data);
        player.SetMagicAtSlot(late, 1);
        Assert.That(ShopOfferGenerator.GetMagicSellPrice(late, player), Is.EqualTo(basePrice));
        for (int i = 0; i < 5; i++) EnterWithDelta(-2);
        Assert.That(ShopOfferGenerator.GetMagicSellPrice(chart, player), Is.Zero);

        var save = RuleJson.Clone(RuleRunSnapshot.ExportCurrentRun(player, run));
        var restored = RuleRunSnapshot.CreatePlayerStatus(save, PlayerStateServices.Simulation, content: content);
        var restoredRun = new RunManager(restored);
        restoredRun.RestoreCurrentNodeLevel(room);
        restoredRun.BeginLevel(room);
        Assert.That(restored.RunRandomStep, Is.EqualTo(player.RunRandomStep));
        Assert.That(ShopOfferGenerator.GetMagicSellPrice(restored.GetItemByIndex(0), restored), Is.Zero);
    }

    private sealed class RecipeAndShieldModifier : MagicModifierModel
    {
        public RecipeAndShieldModifier() : base(new MagicModifierData()) { }
        public override MaterialEnum[] ModifyRecipe(MaterialEnum[] recipe) => new[] { MaterialEnum.Earth };
        public override void BeforeGainShield(ref int shieldValue) => shieldValue += 5;
    }
}
