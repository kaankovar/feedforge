using FeedForge.Models;

namespace FeedForge.Formats;

/// <summary>
/// Facebook / Meta Commerce katalog CSV formatı.
/// Zorunlu kolonlar: id, title, description, availability, condition, price, link, image_link, brand
/// </summary>
public class FacebookCatalogFormat : IFeedFormat
{
    public string Key => "facebook";
    public string DisplayName => "Facebook / Meta Catalog CSV";
    public string ContentType => "text/csv";

    public ParseResult Parse(string raw)
    {
        var result = new ParseResult();
        var rows = Csv.Parse(raw);
        if (rows.Count < 2)
        {
            result.Issues.Add(new ValidationIssue { Severity = "error", Message = "CSV boş veya sadece başlık satırı içeriyor." });
            return result;
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Col(string name) => Array.IndexOf(header, name);
        string Get(string[] row, string name)
        {
            int i = Col(name);
            return (i >= 0 && i < row.Length) ? row[i].Trim() : "";
        }

        if (Col("id") < 0)
            result.Issues.Add(new ValidationIssue { Severity = "error", Field = "id", Message = "Başlıkta 'id' kolonu yok." });

        foreach (var row in rows.Skip(1))
        {
            var p = new Product
            {
                Id = Get(row, "id"),
                Title = Get(row, "title"),
                Description = Get(row, "description"),
                Link = Get(row, "link"),
                ImageLink = Get(row, "image_link"),
                Brand = Get(row, "brand"),
                Gtin = Get(row, "gtin"),
                Mpn = Get(row, "mpn"),
                Category = Get(row, "google_product_category"),
                Condition = string.IsNullOrEmpty(Get(row, "condition")) ? "new" : Get(row, "condition"),
                Availability = ParseHelpers.NormalizeAvailability(Get(row, "availability"))
            };

            var (price, cur) = ParseHelpers.ParsePrice(Get(row, "price"));
            p.Price = price;
            if (cur != null) p.Currency = cur;
            var (sale, _) = ParseHelpers.ParsePrice(Get(row, "sale_price"));
            p.SalePrice = sale;

            if (int.TryParse(Get(row, "quantity_to_sell_on_facebook"), out var q)) p.StockQuantity = q;

            var extra = Get(row, "additional_image_link");
            if (!string.IsNullOrEmpty(extra))
                p.AdditionalImageLinks.AddRange(extra.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));

            result.Products.Add(p);
        }
        return result;
    }

    public string Write(IEnumerable<Product> products)
    {
        var header = new[]
        {
            "id","title","description","availability","condition","price","link",
            "image_link","brand","gtin","mpn","google_product_category","sale_price",
            "additional_image_link","quantity_to_sell_on_facebook"
        };

        var rows = new List<IEnumerable<string?>> { header };
        foreach (var p in products)
        {
            rows.Add(new[]
            {
                p.Id,
                p.Title,
                p.Description,
                p.Availability == "preorder" ? "available for order" : p.Availability.Replace("_", " "),
                p.Condition,
                p.Price.HasValue ? ParseHelpers.FormatPrice(p.Price.Value, p.Currency) : "",
                p.Link,
                p.ImageLink,
                p.Brand,
                p.Gtin,
                p.Mpn,
                p.Category,
                p.SalePrice.HasValue ? ParseHelpers.FormatPrice(p.SalePrice.Value, p.Currency) : "",
                string.Join(",", p.AdditionalImageLinks),
                p.StockQuantity?.ToString() ?? ""
            });
        }
        return Csv.WriteRows(rows);
    }
}
