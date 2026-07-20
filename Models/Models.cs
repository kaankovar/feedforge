namespace FeedForge.Models;

/// <summary>
/// Ortak (kanonik) ürün modeli. Her giriş formatı buna parse edilir,
/// her çıkış formatı bundan üretilir.
/// </summary>
public class Product
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Link { get; set; } = "";
    public string ImageLink { get; set; } = "";
    public List<string> AdditionalImageLinks { get; set; } = new();

    /// <summary>in_stock | out_of_stock | preorder</summary>
    public string Availability { get; set; } = "in_stock";

    public decimal? Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string Currency { get; set; } = "TRY";

    public string Brand { get; set; } = "";
    public string Gtin { get; set; } = "";       // barkod / EAN
    public string Mpn { get; set; } = "";        // üretici parça no / stok kodu
    /// <summary>new | refurbished | used</summary>
    public string Condition { get; set; } = "new";
    public string Category { get; set; } = "";
    public int? StockQuantity { get; set; }

    /// <summary>Formata özgü, modele oturmayan ekstra alanlar (kaybolmasın diye).</summary>
    public Dictionary<string, string> Attributes { get; set; } = new();
}

public class ValidationIssue
{
    public string Severity { get; set; } = "warning"; // error | warning
    public string ProductId { get; set; } = "";
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ParseResult
{
    public List<Product> Products { get; set; } = new();
    public List<ValidationIssue> Issues { get; set; } = new();
}
