namespace Solace.DB.Models.Admin;

public sealed class ShopOffer
{
    public string TabId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Cost { get; set; }
    public int Amount { get; set; } = 1;
    public string Rarity { get; set; } = "Common";
    public bool IsActive { get; set; } = true;
    public string Type { get; set; } = "InventoryItemOffer";
}
