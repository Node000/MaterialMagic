using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;

public class ShopSessionTests
{
    private PlayerState player;
    private EconomyConfigData economy;
    private ShopGenerationServices services;
    private readonly Dictionary<int, MagicData> magic = new Dictionary<int, MagicData>();
    private readonly Dictionary<string, MaterialModifierData> modifiers = new Dictionary<string, MaterialModifierData>();

    [SetUp]
    public void SetUp()
    {
        player = new PlayerState(50, 100, PlayerStateServices.Simulation, new RunDifficultySaveData());
        economy = new EconomyConfigData { shopMaterialPrice = 5, shopRefreshPrice = 2, shopRemoveMaterialPrice = 3, shopSpellPrice = 10 };
        magic.Clear();
        modifiers.Clear();
        services = new ShopGenerationServices
        {
            NextRandomInt = (min, max) => min,
            IsMagicUnlocked = data => true,
            IsModifierUnlocked = data => true,
            FindMagic = id => magic.TryGetValue(id, out MagicData data) ? data : null,
            FindRewardPool = id => null,
            FindProductPool = id => null,
            AllMagic = () => magic.Values,
            AllProductPools = () => Array.Empty<ShopProductPoolData>(),
            FindModifier = id => id != null && modifiers.TryGetValue(id, out MaterialModifierData data) ? data : null,
            ModifierPriceDelta = id => modifiers.TryGetValue(id, out MaterialModifierData data) ? data.price : 0,
            ModifyPrice = price => price,
            QuoteMagic = data => ShopOfferGenerator.GetMagicBuyPrice(data, economy, price => price)
        };
    }

    private ShopSession SavedSession(params ShopOfferSaveData[] offers)
    {
        var session = new ShopSession(player, economy, 2, services);
        session.Open(null, new ShopNodeSaveData { offers = offers });
        return session;
    }

    private MagicData AddMagic(int id)
    {
        var data = new MagicData { numericId = id, id = "shop_test_" + id, rarity = MagicRarity.Common };
        magic.Add(id, data);
        return data;
    }

    private static ShopOfferSaveData MaterialOffer(int price = 4)
        => new ShopOfferSaveData { kind = (int)ShopItemKind.Material, price = price, material = (int)MaterialEnum.Fire };

    [Test]
    public void GeneratedAndRefreshedMagicUsesCurrentOwnershipWithoutRerollingSavedOffers()
    {
        MagicData first = AddMagic(70001);
        MagicData second = AddMagic(70002);
        player.SetMagicAtSlot(new MagicModel(first), 0);
        // Rarity roll is zero; the within-rarity roll is 25%, past the owned item's 20% interval.
        int calls = 0;
        services.NextRandomInt = (min, max) => calls++ % 2 == 0 ? min : max / 4;
        var layers = new[] { new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Item, 1f } }, 2) };
        var session = new ShopSession(player, economy, 2, services);
        session.Open(layers);
        Assert.That(session.Offers.Single().magicData, Is.SameAs(second));
        ShopNodeSaveData save = session.ExportState();

        // Simulate selling: the same session reads the live inventory on refresh.
        player.MagicBook.Clear();
        calls = 0;
        Assert.That(session.TryRefresh(out _), Is.True);
        Assert.That(session.Offers.Single().magicData, Is.SameAs(first));

        int draws = calls;
        var restored = new ShopSession(player, economy, 2, services);
        restored.Open(layers, save);
        Assert.That(restored.Offers.Single().magicData, Is.SameAs(second));
        Assert.That(calls, Is.EqualTo(draws));
    }

    [Test]
    public void SeededShopReopensIdenticallyAndRejectedSaveRestoresRefresh()
    {
        for (int id = 70001; id <= 70008; id++) AddMagic(id);
        services.NextRandomInt = (min, max) => throw new InvalidOperationException("Shop used shared RNG");
        var layers = new[] { new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Item, 1f } }, 6) };
        var session = new ShopSession(player, economy, 2, services);
        session.Open(layers, nodeSeed: 101);
        ShopNodeSaveData initial = session.ExportState();
        var reopened = new ShopSession(player, economy, 2, services);
        reopened.Open(layers, nodeSeed: 101);
        Assert.That(reopened.Offers.Select(x => x.magicData.numericId), Is.EqualTo(initial.offers.Select(x => x.magicNumericId)));
        ShopOffer before = session.Offers[0];
        Assert.That(session.TryRefresh(out _, () => false), Is.False);
        Assert.That(session.Offers[0], Is.SameAs(before));
        Assert.That(player.Gold, Is.EqualTo(100));
        Assert.That(session.RefreshCount, Is.Zero);
        ShopNodeSaveData after = null;
        Assert.That(session.TryRefresh(out int cost, () => { after = session.ExportState(); return true; }), Is.True);
        Assert.That(player.Gold, Is.EqualTo(100 - cost));
        Assert.That(after.refreshCount, Is.EqualTo(1));
        reopened.Open(layers, after, nodeSeed: 999);
        Assert.That(reopened.Offers.Select(x => x.magicData.numericId), Is.EqualTo(after.offers.Select(x => x.magicNumericId)));
        Assert.That(reopened.RefreshCount, Is.EqualTo(1));
        Assert.That(reopened.ExportState().batchSeed, Is.EqualTo(101));
    }

    [Test]
    public void ShopMagicMultiplierCanBeDisabledAndAssortmentStaysUnique()
    {
        MagicData first = AddMagic(70001);
        AddMagic(70002);
        AddMagic(70003);
        player.SetMagicAtSlot(new MagicModel(first), 0);
        economy.ownedMagicWeightMultiplier = 1f;
        services.NextRandomInt = (min, max) => min;
        var session = new ShopSession(player, economy, 2, services);
        session.Open(new[] { ShopLayer.CreateItemLayer() });
        Assert.That(session.Offers[0].magicData, Is.SameAs(first));
        Assert.That(session.Offers.Select(offer => offer.magicData).Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public void MaterialPurchaseCommitsInventoryPaymentPurchasedAndReceiptBeforePresentation()
    {
        ShopSession session = SavedSession(MaterialOffer());
        ShopOffer offer = session.Offers[0];
        Assert.That(session.TryPurchase(offer, out ShopTransaction receipt, out ShopTransactionFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.None));
        Assert.That(player.Gold, Is.EqualTo(96));
        Assert.That(player.Deck.Single(), Is.SameAs(receipt.AddedMaterial));
        Assert.That(player.DrawPile.Single(), Is.SameAs(receipt.AddedMaterial));
        Assert.That(offer.purchased, Is.True);
        ShopNodeSaveData save = session.ExportState();
        Assert.That(save.offers[0].purchased, Is.True);
        Assert.That(save.undo.addedMaterial.instanceId, Is.EqualTo(receipt.AddedMaterial.instanceId));
        Assert.That(save.purchaseInProgress, Is.False);
        Assert.That(session.TryPurchase(offer, out _, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.AlreadyPurchased));
        Assert.That(player.Deck.Count, Is.EqualTo(1));
        Assert.That(player.Gold, Is.EqualTo(96));
    }

    [Test]
    public void MagicPurchaseUsesFirstFreeSlotAndNeverReplacesWhenFull()
    {
        MagicData first = AddMagic(70001);
        MagicData second = AddMagic(70002);
        player.SetMagicAtSlot(MagicFactory.Create(first, 0), 0);
        ShopSession session = SavedSession(new ShopOfferSaveData { kind = (int)ShopItemKind.Magic, magicNumericId = second.numericId, price = 8 });
        Assert.That(session.TryPurchase(session.Offers[0], out ShopTransaction receipt, out _), Is.True);
        Assert.That(receipt.MagicSlotIndex, Is.EqualTo(1));
        Assert.That(receipt.ResultingMagicCount, Is.EqualTo(2));
        Assert.That(player.GetMagicAtSlot(1), Is.SameAs(receipt.AcquiredMagic));
        Assert.That(player.GetMagicAtSlot(0).Data, Is.SameAs(first));
        ShopSession full = SavedSession(new ShopOfferSaveData { kind = (int)ShopItemKind.Magic, magicNumericId = first.numericId, price = 8 });
        Assert.That(full.TryPurchase(full.Offers[0], out _, out ShopTransactionFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.NoMagicSlot));
        Assert.That(player.Gold, Is.EqualTo(92));
        Assert.That(full.Offers[0].purchased, Is.False);
    }

    [Test]
    public void InvalidOrUnaffordablePurchaseDoesNotMutateOrErasePreviousUndo()
    {
        ShopSession session = SavedSession(MaterialOffer(1), MaterialOffer(1000));
        Assert.That(session.TryPurchase(session.Offers[0], out ShopTransaction first, out _), Is.True);
        Assert.That(session.TryPurchase(session.Offers[1], out _, out ShopTransactionFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.NotEnoughGold));
        Assert.That(session.TryPurchase(new ShopOffer { kind = ShopItemKind.Material }, out _, out _), Is.False);
        Assert.That(session.UndoTransaction, Is.SameAs(first));
        Assert.That(player.Gold, Is.EqualTo(99));
        Assert.That(player.Deck.Count, Is.EqualTo(1));
    }

    [Test]
    public void RemoveSelectionCancelAndInvalidTargetNeverChargeOrConsumeAction()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel placeholder = player.AddDeckPlaceholderMaterial();
        ShopSession session = SavedSession();
        Assert.That(session.BeginRemovalSelection(), Is.True);
        session.CancelSelection();
        Assert.That(player.Gold, Is.EqualTo(100));
        Assert.That(session.RemoveArrowUsed, Is.False);
        Assert.That(session.TryRemoveMaterial(placeholder, out _, out _), Is.False);
        Assert.That(session.TryRemoveMaterial(new MaterialModel("foreign", MaterialEnum.Fire), out _, out _), Is.False);
        Assert.That(player.Deck, Does.Contain(card));
        Assert.That(session.TryRemoveMaterial(card, out ShopTransaction receipt, out _), Is.True);
        Assert.That(player.Gold, Is.EqualTo(97));
        Assert.That(session.RemoveArrowUsed, Is.True);
        Assert.That(player.Deck.Contains(card), Is.False);
        Assert.That(player.DrawPile.Contains(card), Is.False);
        Assert.That(session.TryRemoveMaterial(card, out _, out ShopTransactionFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.RemoveAlreadyUsed));
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(player.Gold, Is.EqualTo(100));
        Assert.That(player.Deck, Does.Contain(receipt.RemovedMaterial));
        Assert.That(session.RemoveArrowUsed, Is.False);
    }

    [Test]
    public void RemoveOfferValidatesTargetBeforeSpendingAndSupportsUndo()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Wind);
        ShopSession session = SavedSession(new ShopOfferSaveData { kind = (int)ShopItemKind.RemoveMaterial, price = 6 });
        ShopOffer offer = session.Offers[0];
        Assert.That(session.TryPurchase(offer, out _, out _, null), Is.False);
        Assert.That(player.Gold, Is.EqualTo(100));
        Assert.That(session.TryPurchase(offer, out _, out _, card), Is.True);
        Assert.That(player.Gold, Is.EqualTo(94));
        Assert.That(offer.purchased, Is.True);
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(offer.purchased, Is.False);
        Assert.That(player.Deck, Does.Contain(card));
    }

    [Test]
    public void UndoUsesLatestReceiptOnceAndRetainsLegacyAbsoluteGoldSnapshot()
    {
        ShopSession session = SavedSession(MaterialOffer(4), MaterialOffer(7));
        session.TryPurchase(session.Offers[0], out _, out _);
        session.TryPurchase(session.Offers[1], out _, out _);
        player.AddGold(20, false, RelicGoldReason.Refund);
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(player.Gold, Is.EqualTo(96));
        Assert.That(player.Deck.Count, Is.EqualTo(1));
        Assert.That(session.Offers[0].purchased, Is.True);
        Assert.That(session.Offers[1].purchased, Is.False);
        Assert.That(session.TryUndo(out _), Is.False);
    }

    [Test]
    public void RestoredMaterialUndoResolvesActualDeckInstanceAndClearsAllPiles()
    {
        ShopSession original = SavedSession(MaterialOffer());
        original.TryPurchase(original.Offers[0], out ShopTransaction receipt, out _);
        ShopNodeSaveData save = original.ExportState();
        // A load creates new model objects; the undo receipt must rebind by instanceId.
        var loaded = new MaterialModel(receipt.AddedMaterial.instanceId, MaterialEnum.Fire);
        player.Deck.Clear(); player.DrawPile.Clear();
        player.Deck.Add(loaded); player.DrawPile.Add(loaded); player.Hand.Add(loaded);
        var restored = new ShopSession(player, economy, 2, services);
        restored.Open(null, save);
        Assert.That(restored.TryUndo(out _), Is.True);
        Assert.That(player.Deck, Is.Empty);
        Assert.That(player.DrawPile, Is.Empty);
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.Gold, Is.EqualTo(100));
    }

    [Test]
    public void RefreshCommitsNewOffersCounterAndRemovalResetAndInvalidatesUndo()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        ShopSession session = SavedSession(MaterialOffer());
        session.TryRemoveMaterial(card, out _, out _);
        Assert.That(session.TryRefresh(out int cost), Is.True);
        Assert.That(cost, Is.EqualTo(2));
        Assert.That(player.Gold, Is.EqualTo(95));
        Assert.That(session.RefreshCount, Is.EqualTo(1));
        Assert.That(session.RefreshCost, Is.EqualTo(3));
        Assert.That(session.RemoveArrowUsed, Is.False);
        Assert.That(session.UndoTransaction, Is.Null);
        Assert.That(session.Offers.All(offer => !offer.purchased), Is.True);
        Assert.That(session.TryRefresh(out cost), Is.True);
        Assert.That(cost, Is.EqualTo(3));
        Assert.That(session.ExportState().refreshCount, Is.EqualTo(2));
    }

    [Test]
    public void RestorePreservesAllOffersLayersAndRefreshCountWithoutConsumingRandom()
    {
        int draws = 0;
        services.NextRandomInt = (min, max) => { draws++; return min; };
        var saved = new ShopNodeSaveData
        {
            offers = new[] { MaterialOffer(0), MaterialOffer(8), MaterialOffer(9) },
            layerOfferCounts = new[] { 0, 3 }, refreshCount = 4, removeArrowUsed = true, purchaseInProgress = true
        };
        var session = new ShopSession(player, economy, 2, services);
        session.Open(null, saved);
        Assert.That(draws, Is.Zero);
        Assert.That(session.Offers.Select(offer => offer.price), Is.EqualTo(new[] { 0, 8, 9 }));
        Assert.That(session.LayerOffers.Select(row => row.Count), Is.EqualTo(new[] { 0, 3 }));
        Assert.That(session.LayerOffers[1][2], Is.SameAs(session.Offers[2]));
        Assert.That(session.RefreshCost, Is.EqualTo(6));
        Assert.That(session.ExportState().purchaseInProgress, Is.False);
        Assert.That(session.RemoveArrowUsed, Is.True);
    }

    [Test]
    public void PaidRemovalSurchargeIsAddedAfterDifficultyAndSurvivesRefreshAndNextShop()
    {
        services.ModifyPrice = price => price * 2;
        MaterialModel first = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel second = player.AddDeckMaterial(MaterialEnum.Water);
        ShopSession session = SavedSession();
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(6));
        Assert.That(session.TryRemoveMaterial(first, out ShopTransaction receipt, out _), Is.True);
        Assert.That(receipt.Price, Is.EqualTo(6));
        Assert.That(player.PaidShopArrowRemovalCount, Is.EqualTo(1));
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(7));
        Assert.That(session.TryRefresh(out _), Is.True);
        Assert.That(session.RemoveArrowUsed, Is.False);
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(7));

        ShopSession next = SavedSession();
        Assert.That(next.TryRemoveMaterial(second, out receipt, out _), Is.True);
        Assert.That(receipt.Price, Is.EqualTo(7));
        Assert.That(next.RemoveArrowPrice, Is.EqualTo(8));
        Assert.That(player.PaidShopArrowRemovalCount, Is.EqualTo(2));
        Assert.That(new PlayerState().PaidShopArrowRemovalCount, Is.Zero);
    }

    [Test]
    public void CancelInvalidUnaffordableAndFreeRemovalDoNotIncreasePrice()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel freeCard = player.AddDeckMaterial(MaterialEnum.Wind);
        ShopSession session = SavedSession();
        Assert.That(session.BeginRemovalSelection(), Is.True);
        session.CancelSelection();
        Assert.That(session.TryRemoveMaterial(new MaterialModel("foreign", MaterialEnum.Fire), out _, out _), Is.False);
        player.AddGold(-player.Gold);
        Assert.That(session.TryRemoveMaterial(card, out _, out ShopTransactionFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(ShopTransactionFailure.NotEnoughGold));
        Assert.That(session.RemoveArrowUsed, Is.False);
        // The event/free-effect path uses the general removal API, not the paid shop transaction.
        Assert.That(player.RemoveCardEverywhere(freeCard), Is.True);
        Assert.That(player.PaidShopArrowRemovalCount, Is.Zero);
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(3));

        economy.shopRemoveMaterialPrice = 0;
        Assert.That(session.TryRemoveMaterial(card, out ShopTransaction receipt, out _), Is.True);
        Assert.That(receipt.CountedPaidArrowRemoval, Is.False);
        Assert.That(player.PaidShopArrowRemovalCount, Is.Zero);
        Assert.That(session.RemoveArrowPrice, Is.Zero);
    }

    [Test]
    public void UndoPaidRemovalRestoresCounterAndCannotBeRepeated()
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Fire);
        ShopSession session = SavedSession();
        Assert.That(session.TryRemoveMaterial(card, out _, out _), Is.True);
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(4));
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(player.PaidShopArrowRemovalCount, Is.Zero);
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(3));
        Assert.That(session.RemoveArrowUsed, Is.False);
        Assert.That(player.Gold, Is.EqualTo(100));
        Assert.That(player.Deck, Does.Contain(card));
        Assert.That(session.TryUndo(out _), Is.False);
    }

    [TestCase(0, 0)]
    [TestCase(6, 1)]
    public void RemovalOfferCountsOnlyPaidPurchaseAndUndoRestoresCounter(int price, int expectedCount)
    {
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Wind);
        ShopSession session = SavedSession(new ShopOfferSaveData { kind = (int)ShopItemKind.RemoveMaterial, price = price });
        Assert.That(session.TryPurchase(session.Offers[0], out _, out _, card), Is.True);
        Assert.That(player.PaidShopArrowRemovalCount, Is.EqualTo(expectedCount));
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(3 + expectedCount));
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(player.PaidShopArrowRemovalCount, Is.Zero);
    }

    [Test]
    public void RunPlayerAndRemovalReceiptRoundTripPreserveActualPriceAndUndoCounter()
    {
        MaterialModel first = player.AddDeckMaterial(MaterialEnum.Fire);
        MaterialModel second = player.AddDeckMaterial(MaterialEnum.Wind);
        ShopSession session = SavedSession();
        Assert.That(session.TryRemoveMaterial(first, out _, out _), Is.True);
        session = SavedSession();
        Assert.That(session.TryRemoveMaterial(second, out ShopTransaction receipt, out _), Is.True);
        // Exercise the real player export without writing any user save slot.
        var export = typeof(RunSaveSystem).GetMethod("ExportPlayer", BindingFlags.NonPublic | BindingFlags.Static);
        var save = new RunSaveData
        {
            player = (PlayerSaveData)export.Invoke(null, new object[] { player }),
            currentNode = new CurrentNodeSaveData { shop = session.ExportState() }
        };
        RunSaveData loaded = JsonConvert.DeserializeObject<RunSaveData>(JsonConvert.SerializeObject(save));
        PlayerState restoredPlayer = RunSaveSystem.CreatePlayerStatus(loaded);
        var restored = new ShopSession(restoredPlayer, economy, 2, services);
        restored.Open(null, loaded.currentNode.shop);
        Assert.That(restoredPlayer.PaidShopArrowRemovalCount, Is.EqualTo(2));
        Assert.That(restored.RemoveArrowPrice, Is.EqualTo(5));
        Assert.That(restored.UndoTransaction.Price, Is.EqualTo(4));
        Assert.That(restored.RemoveArrowUsed, Is.True);
        Assert.That(restored.TryUndo(out _), Is.True);
        Assert.That(restoredPlayer.PaidShopArrowRemovalCount, Is.EqualTo(1));
        Assert.That(restored.RemoveArrowPrice, Is.EqualTo(4));
        Assert.That(restoredPlayer.Gold, Is.EqualTo(97));
        Assert.That(restoredPlayer.Deck.Single().instanceId, Is.EqualTo(receipt.RemovedMaterial.instanceId));
        Assert.That(restored.RemoveArrowUsed, Is.False);
    }

    [Test]
    public void LegacySaveDefaultsToZeroAndLegacyUndoDoesNotReduceNewCounter()
    {
        RunSaveData legacy = JsonConvert.DeserializeObject<RunSaveData>(
            "{\"player\":{\"maxHealth\":50,\"currentHealth\":50,\"gold\":97}}");
        Assert.That(RunSaveSystem.CreatePlayerStatus(legacy).PaidShopArrowRemovalCount, Is.Zero);
        MaterialModel first = player.AddDeckMaterial(MaterialEnum.Fire);
        ShopSession session = SavedSession();
        Assert.That(session.TryRemoveMaterial(first, out _, out _), Is.True);
        var oldShop = JsonConvert.DeserializeObject<ShopNodeSaveData>(
            "{\"offers\":[],\"removeArrowUsed\":true,\"undo\":{\"gold\":100,\"removedMaterial\":{\"instanceId\":\"legacy_removed\",\"material\":1}}}");
        Assert.That(oldShop.undo.price, Is.EqualTo(-1));
        session.Open(null, oldShop);
        Assert.That(session.TryUndo(out _), Is.True);
        Assert.That(player.PaidShopArrowRemovalCount, Is.EqualTo(1));
        Assert.That(session.RemoveArrowPrice, Is.EqualTo(4));
    }

    [Test]
    public void GeneratorRetainsArrowRandomCallOrderGuaranteedLastAndDynamicBaseQuote()
    {
        modifiers.Add("half_arrow", new MaterialModifierData { id = "half_arrow", script = nameof(HalfArrowModifier), price = -1 });
        modifiers.Add("charge", new MaterialModifierData { id = "charge", script = nameof(ChargeModifier), price = 2 });
        var pool = new ShopProductPoolData
        {
            weakMaterialChance = 0.5f,
            normalMaterialOffers = new[]
            {
                new ShopMaterialOfferData { material = MaterialEnum.Fire, price = 2 },
                new ShopMaterialOfferData { material = MaterialEnum.Wind, price = 7 }
            },
            weakMaterialOffers = new[] { new ShopMaterialOfferData { material = MaterialEnum.Water, modifierId = "half_arrow", price = 999 } },
            strongMaterialOffers = new[] { new ShopMaterialOfferData { material = MaterialEnum.Earth, modifierId = "charge", price = 999 } }
        };
        services.AllProductPools = () => new[] { pool };
        var rolls = new Queue<int>(new[] { 9999, 0, 0, 0, 9999, 0, 0 });
        var calls = new List<int>();
        services.NextRandomInt = (min, max) => { calls.Add(max); return rolls.Dequeue(); };
        var offers = new List<ShopOffer>();
        var rows = new List<List<ShopOffer>>();
        new ShopOfferGenerator(economy, services).Generate(new[] { ShopLayer.CreateArrowLayer() }, offers, rows);
        Assert.That(offers.Select(offer => offer.price), Is.EqualTo(new[] { 2, 6, 7, 4 }));
        Assert.That(calls, Is.EqualTo(new[] { 10000, 2, 10000, 1, 10000, 1, 1 }));
        Assert.That(offers.Last().materialModifierData.id, Is.EqualTo("charge"));
        Assert.That(rows[0][0], Is.SameAs(offers[0]));
        Assert.That(rolls, Is.Empty);
    }

    [TestCase(0f, 4)]
    [TestCase(1f, 4)]
    [TestCase(0.5f, 8)]
    public void ChanceEndpointsDoNotRollButSingletonPoolDrawsAreRetained(float chance, int expectedCalls)
    {
        var pool = new ShopProductPoolData { weakMaterialChance = chance };
        services.AllProductPools = () => new[] { pool };
        int calls = 0;
        services.NextRandomInt = (min, max) => { calls++; return min; };
        var offers = new List<ShopOffer>();
        new ShopOfferGenerator(economy, services).Generate(
            new[] { new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Arrow, 0f } }) }, offers, new List<List<ShopOffer>>());
        Assert.That(offers.Count, Is.EqualTo(4));
        Assert.That(calls, Is.EqualTo(expectedCalls));
    }

    [Test]
    public void ExplicitFreeAndNegativeFallbackPricesAndMagicRarityOffsetsRemainCompatible()
    {
        var generator = new ShopOfferGenerator(economy, services);
        Assert.That(generator.GetOfferPrice(0), Is.Zero);
        Assert.That(generator.GetOfferPrice(-1), Is.EqualTo(5));
        Assert.That(ShopOfferGenerator.GetMagicBuyPrice(new MagicData { rarity = MagicRarity.Common }, economy, x => x), Is.EqualTo(8));
        Assert.That(ShopOfferGenerator.GetMagicBuyPrice(new MagicData { rarity = MagicRarity.Rare }, economy, x => x), Is.EqualTo(9));
        Assert.That(ShopOfferGenerator.GetMagicBuyPrice(new MagicData { rarity = MagicRarity.Epic }, economy, x => x), Is.EqualTo(10));
        Assert.That(ShopOfferGenerator.GetMagicBuyPrice(new MagicData { rarity = MagicRarity.Legendary }, economy, x => x), Is.EqualTo(11));
    }
}
