using System.Text.Json;
using System.Text.Json.Serialization;
using FeedForge.Models;

namespace FeedForge.Formats;

/// <summary>Kanonik modelin doğrudan JSON temsili. API tüketicileri için en esnek format.</summary>
public class GenericJsonFormat : IFeedFormat
{
    public string Key => "json";
    public string DisplayName => "Generic JSON";
    public string ContentType => "application/json";

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public ParseResult Parse(string raw)
    {
        var result = new ParseResult();
        try
        {
            // Hem düz dizi [...] hem { "products": [...] } sarmalayıcısını destekle
            using var doc = JsonDocument.Parse(raw);
            JsonElement arr = doc.RootElement;
            if (arr.ValueKind == JsonValueKind.Object && arr.TryGetProperty("products", out var inner))
                arr = inner;

            if (arr.ValueKind != JsonValueKind.Array)
            {
                result.Issues.Add(new ValidationIssue { Severity = "error", Message = "JSON bir ürün dizisi ya da { \"products\": [...] } olmalı." });
                return result;
            }

            var list = JsonSerializer.Deserialize<List<Product>>(arr.GetRawText(), Opts);
            if (list != null) result.Products = list;
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssue { Severity = "error", Message = "JSON okunamadı: " + ex.Message });
        }
        return result;
    }

    public string Write(IEnumerable<Product> products) =>
        JsonSerializer.Serialize(new { products }, Opts);
}

/// <summary>Basit düz CSV — kanonik alan adlarıyla.</summary>
public class GenericCsvFormat : IFeedFormat
{
    public string Key => "csv";
    public string DisplayName => "Generic CSV";
    public string ContentType => "text/csv";

    private static readonly string[] Header =
    {
        "id","title","description","link","image_link","availability","price",
        "sale_price","currency","brand","gtin","mpn","condition","category","stock_quantity"
    };

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
        string Get(string[] row, string name)
        {
            int i = Array.IndexOf(header, name);
            return (i >= 0 && i < row.Length) ? row[i].Trim() : "";
        }

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
                Category = Get(row, "category"),
                Condition = string.IsNullOrEmpty(Get(row, "condition")) ? "new" : Get(row, "condition"),
                Availability = ParseHelpers.NormalizeAvailability(Get(row, "availability"))
            };

            var (price, cur) = ParseHelpers.ParsePrice(Get(row, "price"));
            p.Price = price;
            var (sale, _) = ParseHelpers.ParsePrice(Get(row, "sale_price"));
            p.SalePrice = sale;
            var curCol = Get(row, "currency");
            p.Currency = !string.IsNullOrEmpty(curCol) ? curCol.ToUpperInvariant() : (cur ?? "TRY");

            if (int.TryParse(Get(row, "stock_quantity"), out var q)) p.StockQuantity = q;

            result.Products.Add(p);
        }
        return result;
    }

    public string Write(IEnumerable<Product> products)
    {
        var rows = new List<IEnumerable<string?>> { Header };
        foreach (var p in products)
        {
            rows.Add(new[]
            {
                p.Id, p.Title, p.Description, p.Link, p.ImageLink, p.Availability,
                p.Price?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                p.SalePrice?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                p.Currency, p.Brand, p.Gtin, p.Mpn, p.Condition, p.Category,
                p.StockQuantity?.ToString() ?? ""
            });
        }
        return Csv.WriteRows(rows);
    }
}
