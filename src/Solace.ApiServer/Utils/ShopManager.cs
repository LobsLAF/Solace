using Serilog;
using Solace.DB;
using Solace.DB.Models.Admin;
using Solace.StaticData;
using System.Collections.Immutable;

namespace Solace.ApiServer.Utils;

public sealed class ShopManager
{
    private readonly EarthDB _db;
    private readonly StaticData.StaticData _staticData;

    private ImmutableList<ShopOffer> _offers = ImmutableList<ShopOffer>.Empty;
    private ImmutableList<ShopTab> _tabs = ImmutableList<ShopTab>.Empty;

    public ShopManager(EarthDB db, StaticData.StaticData staticData)
    {
        _db = db;
        _staticData = staticData;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var offers = new List<ShopOffer>();
            var tabs = new List<ShopTab>();

            using (var connection = _db.OpenConnection(false))
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT value FROM {EarthDB.ObjectsTable} WHERE type = 'shop_offer'";
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var offer = EarthDB.FromJson<ShopOffer>(reader.GetString(0));
                            if (offer != null) offers.Add(offer);
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT value FROM {EarthDB.ObjectsTable} WHERE type = 'shop_tab' ORDER BY json_extract(value, '$.order')";
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var tab = EarthDB.FromJson<ShopTab>(reader.GetString(0));
                            if (tab != null) tabs.Add(tab);
                        }
                    }
                }
            }

            _offers = offers.ToImmutableList();
            _tabs = tabs.ToImmutableList();

            Log.Information("Reloaded {OfferCount} shop offers and {TabCount} shop tabs from database", _offers.Count, _tabs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload shop data from database");
        }
    }

    public IEnumerable<ShopOffer> GetActiveOffers() => _offers.Where(o => o.IsActive);
    public IEnumerable<ShopTab> GetTabs() => _tabs;

    public IEnumerable<Playfab.Item> GetVirtualItems()
    {
        foreach (var offer in _offers.Where(o => o.IsActive))
        {
            var item = CreateVirtualItem(offer);
            if (item != null) yield return item;
        }
    }

    public Playfab.Item? GetVirtualItem(Guid itemId)
    {
        var offer = _offers.FirstOrDefault(o => o.IsActive && o.ItemId == itemId.ToString());
        return offer != null ? CreateVirtualItem(offer) : null;
    }

    private Playfab.Item? CreateVirtualItem(ShopOffer offer)
    {
        if (!Guid.TryParse(offer.ItemId, out var itemId)) return null;

        Playfab.Item.ItemData data;
        if (offer.Type == "Genoa" || offer.Type == "BuildplateOffer")
        {
            data = new Playfab.Item.BuildplateData(itemId, offer.Cost, Playfab.Item.BuidplateSize.Medium, 1, Enum.Parse<Playfab.Item.Rarity>(offer.Rarity), "1.0.0");
        }
        else if (offer.Type == "RubyOffer")
        {
            data = new Playfab.Item.RubyData(offer.Amount, 0, "sku." + offer.ItemId, "Solace");
        }
        else
        {
            data = new Playfab.Item.InventoryItemData(itemId, offer.Cost, offer.Amount, Enum.Parse<Playfab.Item.Rarity>(offer.Rarity), "1.0.0");
        }

        return new Playfab.Item(
            true,
            data,
            offer.Name,
            offer.Name,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow,
            itemId,
            null,
            "Solace",
            "Solace",
            [],
            new Dictionary<string, Playfab.Item.KeywordValues>(),
            [],
            [],
            new Dictionary<string, string>(),
            new Dictionary<string, string>()
        );
    }

    public Playfab.Item.QueryManifestData GetQueryManifest(string minVersion, string maxVersion)
    {
        var playfabTabs = new List<Playfab.Tab>();

        // If we have dynamic tabs, use them. Otherwise, fallback to static tabs but filtered by our offers?
        // Actually, let's just use dynamic tabs if they exist, or generate one default tab for our offers.
        
        if (_tabs.Count > 0)
        {
            foreach (var tab in _tabs)
            {
                var queries = new List<Playfab.Tab.ScreenLayoutQuery>();
                
                // For now, let's just put all offers for this tab in one grid query
                var offerIds = _offers
                    .Where(o => o.TabId == tab.Title && o.IsActive) // Match by title as ID for now
                    .Select(o => o.ItemId)
                    .ToList();

                if (offerIds.Count > 0)
                {
                    queries.Add(new Playfab.Tab.ScreenLayoutQuery(
                        Playfab.Tab.ColumnType.Grid,
                        [new Playfab.Tab.Query(offerIds, [Playfab.ContentType.InventoryItemOffer, Playfab.ContentType.Genoa, Playfab.ContentType.RubyOffer], offerIds.Count)],
                        Guid.NewGuid() // Component ID
                    ));
                }

                playfabTabs.Add(new Playfab.Tab(tab.Title, tab.Title, tab.Icon, queries));
            }
        }
        else
        {
            // Fallback to static tabs
            return _staticData.Playfab.Items.Values
                .Select(i => i.Data)
                .OfType<Playfab.Item.QueryManifestData>()
                .FirstOrDefault() ?? new Playfab.Item.QueryManifestData(minVersion, maxVersion, [], []);
        }

        return new Playfab.Item.QueryManifestData(minVersion, maxVersion, playfabTabs, []);
    }
}
