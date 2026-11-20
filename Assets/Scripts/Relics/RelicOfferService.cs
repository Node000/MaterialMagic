using System;
using System.Collections.Generic;

/// <summary>UI-independent candidate generation and idempotent selection/purchase. Caller persists offer with player.</summary>
public static class RelicOfferService
{
    public static RelicOfferSaveData Create(PlayerState player, string poolId, int count, bool paid = false)
    {
        if (player == null || !player.Relics.IsSettled || string.IsNullOrEmpty(poolId) || count <= 0 ||
            !GameDataDatabase.RelicPoolData.TryGetValue(poolId, out var pool) || pool.relicIds == null) return null;
        if (player.PendingRelicOffer != null && !player.PendingRelicOffer.committed)
            return player.PendingRelicOffer.poolId == poolId ? player.PendingRelicOffer : null;
        var candidates = new List<RelicData>();
        var weights = new List<int>();
        var seen = new HashSet<int>();
        for (int i = 0; i < pool.relicIds.Length; i++)
        {
            int id = pool.relicIds[i];
            int weight = pool.weights != null && i < pool.weights.Length ? pool.weights[i] : 1;
            if (weight <= 0 || !seen.Add(id) || player.Relics.Contains(id) ||
                !GameDataDatabase.TryGetRelicData(id, out var data) || RelicFactory.Create(data) == null) continue;
            candidates.Add(data); weights.Add(weight);
        }
        var ids = new List<int>();
        var prices = new List<int>();
        while (ids.Count < count && candidates.Count > 0)
        {
            int total = 0;
            foreach (int weight in weights) total = checked(total + weight);
            int roll = player.NextRelicRandomInt(0, total);
            int index = 0;
            while (roll >= weights[index]) { roll -= weights[index]; index++; }
            ids.Add(candidates[index].numericId);
            prices.Add(paid ? Math.Max(0, candidates[index].price) : 0);
            candidates.RemoveAt(index); weights.RemoveAt(index);
        }
        if (ids.Count == 0) return null;
        var offer = new RelicOfferSaveData { offerId = Guid.NewGuid().ToString("N"), poolId = poolId, relicIds = ids.ToArray(), prices = prices.ToArray() };
        player.PendingRelicOffer = offer;
        return offer;
    }

    public static RelicAcquireResult TryClaim(PlayerState player, RelicOfferSaveData offer, int index)
    {
        if (player == null || !player.Relics.IsSettled || offer == null || offer.committed ||
            player.PendingRelicOffer == null || player.PendingRelicOffer.offerId != offer.offerId)
            return RelicAcquireResult.Invalid;
        offer = player.PendingRelicOffer;
        if (offer.relicIds == null || offer.prices == null || offer.committed || index < 0 || index >= offer.relicIds.Length ||
            index >= offer.prices.Length || offer.prices[index] < 0 || player.Gold < offer.prices[index] ||
            !GameDataDatabase.TryGetRelicData(offer.relicIds[index], out var data) || RelicFactory.Create(data) == null)
            return RelicAcquireResult.Invalid;
        using (player.Relics.BeginOperation())
        {
            // Mark before callbacks so a synchronous double click/observer cannot claim twice.
            offer.committed = true;
            offer.selectedIndex = index;
            player.AddGold(-offer.prices[index], false, RelicGoldReason.Spend);
            return player.Relics.TryAcquire(data);
        }
    }
}
