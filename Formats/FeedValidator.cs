using FeedForge.Models;

namespace FeedForge.Formats;

public static class FeedValidator
{
    public static List<ValidationIssue> Validate(List<Product> products)
    {
        var issues = new List<ValidationIssue>();
        var seenIds = new HashSet<string>();

        foreach (var p in products)
        {
            void Err(string field, string msg) =>
                issues.Add(new ValidationIssue { Severity = "error", ProductId = p.Id, Field = field, Message = msg });
            void Warn(string field, string msg) =>
                issues.Add(new ValidationIssue { Severity = "warning", ProductId = p.Id, Field = field, Message = msg });

            if (string.IsNullOrWhiteSpace(p.Id)) Err("id", "Ürün ID boş.");
            else if (!seenIds.Add(p.Id)) Err("id", $"Tekrarlanan ürün ID: {p.Id}");

            if (string.IsNullOrWhiteSpace(p.Title)) Err("title", "Başlık boş.");
            else if (p.Title.Length > 150) Warn("title", "Başlık 150 karakterden uzun; Google/Meta kırpabilir.");

            if (string.IsNullOrWhiteSpace(p.Description)) Warn("description", "Açıklama boş; reklam performansını düşürür.");

            if (string.IsNullOrWhiteSpace(p.Link)) Err("link", "Ürün linki boş.");
            else if (!p.Link.StartsWith("http")) Err("link", "Ürün linki http(s) ile başlamalı.");

            if (string.IsNullOrWhiteSpace(p.ImageLink)) Err("image_link", "Görsel linki boş.");
            else if (!p.ImageLink.StartsWith("http")) Err("image_link", "Görsel linki http(s) ile başlamalı.");

            if (!p.Price.HasValue) Err("price", "Fiyat yok veya okunamadı.");
            else if (p.Price <= 0) Err("price", "Fiyat 0 veya negatif.");

            if (p.SalePrice.HasValue && p.Price.HasValue && p.SalePrice >= p.Price)
                Warn("sale_price", "İndirimli fiyat, liste fiyatından düşük değil.");

            if (string.IsNullOrWhiteSpace(p.Gtin) && string.IsNullOrWhiteSpace(p.Mpn))
                Warn("gtin", "GTIN (barkod) ve MPN ikisi de boş; Google Merchant onay sorunları çıkarabilir.");
            else if (!string.IsNullOrWhiteSpace(p.Gtin) && p.Gtin.Length is not (8 or 12 or 13 or 14))
                Warn("gtin", $"GTIN uzunluğu ({p.Gtin.Length}) standart dışı; 8, 12, 13 veya 14 haneli olmalı.");

            if (string.IsNullOrWhiteSpace(p.Brand)) Warn("brand", "Marka boş.");
        }

        return issues;
    }
}
