using System.Xml.Linq;
using FeedForge.Models;

namespace FeedForge.Formats;

/// <summary>
/// Trendyol tarzı XML ürün feed'i.
/// Not: Trendyol'un resmi entegrasyonu REST API'dir; bu format, entegratörlerin
/// yaygın kullandığı XML feed şemasını hedefler (Products > Product > Barcode, Title, ...).
/// Gerekirse alan adları kolayca uyarlanabilir.
/// </summary>
public class TrendyolXmlFormat : IFeedFormat
{
    public string Key => "trendyol";
    public string DisplayName => "Trendyol XML";
    public string ContentType => "application/xml";

    public ParseResult Parse(string raw)
    {
        var result = new ParseResult();
        XDocument doc;
        try { doc = XDocument.Parse(raw); }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssue { Severity = "error", Message = "XML okunamadı: " + ex.Message });
            return result;
        }

        var items = doc.Descendants("Product").ToList();
        if (items.Count == 0) items = doc.Descendants("product").ToList();
        if (items.Count == 0)
            result.Issues.Add(new ValidationIssue { Severity = "error", Message = "<Product> öğesi bulunamadı." });

        foreach (var item in items)
        {
            string V(params string[] names)
            {
                foreach (var n in names)
                {
                    var el = item.Element(n) ?? item.Element(n.ToLowerInvariant());
                    if (el != null) return el.Value.Trim();
                }
                return "";
            }

            var p = new Product
            {
                Id = V("StockCode", "ProductCode", "Id"),
                Gtin = V("Barcode"),
                Title = V("Title", "Name", "ProductName"),
                Description = V("Description"),
                Brand = V("Brand"),
                Category = V("CategoryName", "Category"),
                ImageLink = V("Image1", "ImageUrl", "Image"),
                Link = V("ProductUrl", "Url"),
                Mpn = V("StockCode")
            };

            if (string.IsNullOrEmpty(p.Id)) p.Id = p.Gtin;

            var (listPrice, cur1) = ParseHelpers.ParsePrice(V("ListPrice"));
            var (salePrice, cur2) = ParseHelpers.ParsePrice(V("SalePrice", "Price"));
            p.Price = listPrice ?? salePrice;
            if (listPrice.HasValue && salePrice.HasValue && salePrice < listPrice)
                p.SalePrice = salePrice;
            p.Currency = cur1 ?? cur2 ?? "TRY";

            if (int.TryParse(V("Quantity", "Stock", "StockAmount"), out var q))
            {
                p.StockQuantity = q;
                p.Availability = q > 0 ? "in_stock" : "out_of_stock";
            }

            // Ek görseller: Image2..Image8
            for (int i = 2; i <= 8; i++)
            {
                var img = V("Image" + i);
                if (!string.IsNullOrEmpty(img)) p.AdditionalImageLinks.Add(img);
            }

            var vat = V("VatRate", "KDV");
            if (!string.IsNullOrEmpty(vat)) p.Attributes["VatRate"] = vat;

            result.Products.Add(p);
        }
        return result;
    }

    public string Write(IEnumerable<Product> products)
    {
        var root = new XElement("Products");
        foreach (var p in products)
        {
            var el = new XElement("Product",
                new XElement("StockCode", string.IsNullOrEmpty(p.Mpn) ? p.Id : p.Mpn),
                new XElement("Barcode", p.Gtin),
                new XElement("Title", p.Title),
                new XElement("Description", p.Description),
                new XElement("Brand", p.Brand),
                new XElement("CategoryName", p.Category),
                new XElement("Quantity", p.StockQuantity ?? (p.Availability == "in_stock" ? 1 : 0)),
                new XElement("Image1", p.ImageLink));

            int i = 2;
            foreach (var img in p.AdditionalImageLinks.Take(7))
                el.Add(new XElement("Image" + i++, img));

            if (p.Price.HasValue) el.Add(new XElement("ListPrice",
                p.Price.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            var effectiveSale = p.SalePrice ?? p.Price;
            if (effectiveSale.HasValue) el.Add(new XElement("SalePrice",
                effectiveSale.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));

            if (p.Attributes.TryGetValue("VatRate", out var vat))
                el.Add(new XElement("VatRate", vat));

            root.Add(el);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return doc.Declaration + Environment.NewLine + doc.ToString();
    }
}
