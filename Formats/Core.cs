using System.Text;
using FeedForge.Models;

namespace FeedForge.Formats;

public interface IFeedFormat
{
    /// <summary>Format anahtarı: google, facebook, trendyol, json, csv</summary>
    string Key { get; }
    string DisplayName { get; }
    string ContentType { get; }

    ParseResult Parse(string raw);
    string Write(IEnumerable<Product> products);
}

public static class FormatRegistry
{
    private static readonly Dictionary<string, IFeedFormat> _formats =
        new(StringComparer.OrdinalIgnoreCase);

    static FormatRegistry()
    {
        Register(new GoogleMerchantFormat());
        Register(new FacebookCatalogFormat());
        Register(new TrendyolXmlFormat());
        Register(new GenericJsonFormat());
        Register(new GenericCsvFormat());
    }

    private static void Register(IFeedFormat f) => _formats[f.Key] = f;

    public static IFeedFormat? Get(string? key) =>
        key != null && _formats.TryGetValue(key, out var f) ? f : null;

    public static IEnumerable<object> List() =>
        _formats.Values.Select(f => new { key = f.Key, name = f.DisplayName, contentType = f.ContentType });
}

/// <summary>Harici paket kullanmadan RFC 4180 uyumlu basit CSV okuma/yazma.</summary>
public static class Csv
{
    public static List<string[]> Parse(string raw)
    {
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var current = new List<string>();
        bool inQuotes = false;
        int i = 0;

        // BOM temizle
        if (raw.Length > 0 && raw[0] == '\uFEFF') i = 1;

        while (i < raw.Length)
        {
            char c = raw[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { current.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* atla */ }
                else if (c == '\n')
                {
                    current.Add(field.ToString()); field.Clear();
                    if (current.Count > 1 || current[0].Length > 0) rows.Add(current.ToArray());
                    current = new List<string>();
                }
                else field.Append(c);
            }
            i++;
        }
        // son satır
        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            if (current.Count > 1 || current[0].Length > 0) rows.Add(current.ToArray());
        }
        return rows;
    }

    public static string Escape(string? value)
    {
        value ??= "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public static string WriteRows(IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return sb.ToString();
    }
}

/// <summary>Fiyat ve sayı ayrıştırma yardımcıları.</summary>
public static class ParseHelpers
{
    /// <summary>"123.45 TRY", "1.234,56 TL", "99.90" gibi değerleri ayrıştırır.</summary>
    public static (decimal? amount, string? currency) ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        raw = raw.Trim();

        string? currency = null;
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string numberPart = raw;
        if (parts.Length == 2)
        {
            // "123.45 TRY" veya "TRY 123.45"
            if (parts[1].All(char.IsLetter)) { numberPart = parts[0]; currency = parts[1].ToUpperInvariant(); }
            else if (parts[0].All(char.IsLetter)) { numberPart = parts[1]; currency = parts[0].ToUpperInvariant(); }
        }
        if (currency == "TL") currency = "TRY";

        numberPart = numberPart.Replace("₺", "").Replace("$", "").Replace("€", "").Trim();

        // Hem "1.234,56" hem "1,234.56" hem "1234.56" desteği
        bool hasComma = numberPart.Contains(',');
        bool hasDot = numberPart.Contains('.');
        if (hasComma && hasDot)
        {
            // son ayraç ondalıktır
            if (numberPart.LastIndexOf(',') > numberPart.LastIndexOf('.'))
                numberPart = numberPart.Replace(".", "").Replace(',', '.');
            else
                numberPart = numberPart.Replace(",", "");
        }
        else if (hasComma)
        {
            numberPart = numberPart.Replace(',', '.');
        }

        return decimal.TryParse(numberPart, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? (d, currency) : (null, currency);
    }

    public static string FormatPrice(decimal amount, string currency) =>
        amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + currency;

    public static string NormalizeAvailability(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
        return v switch
        {
            "in_stock" or "instock" or "available" or "true" or "1" or "evet" or "var" => "in_stock",
            "out_of_stock" or "outofstock" or "unavailable" or "false" or "0" or "hayir" or "hayır" or "yok" => "out_of_stock",
            "preorder" or "pre_order" or "backorder" => "preorder",
            _ => "in_stock"
        };
    }
}
