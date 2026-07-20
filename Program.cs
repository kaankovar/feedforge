using System.Text;
using FeedForge.Formats;
using FeedForge.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ---------------------------------------------------------------
// RapidAPI koruması:
// RAPIDAPI_PROXY_SECRET ortam değişkeni tanımlıysa, sadece
// X-RapidAPI-Proxy-Secret başlığı eşleşen istekler kabul edilir.
// Böylece API'yi RapidAPI dışından bedava kullanamazlar.
// Lokal geliştirmede değişkeni tanımlama, kontrol devre dışı kalır.
// ---------------------------------------------------------------
var proxySecret = Environment.GetEnvironmentVariable("RAPIDAPI_PROXY_SECRET");
app.Use(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(proxySecret) && ctx.Request.Path != "/health")
    {
        var header = ctx.Request.Headers["X-RapidAPI-Proxy-Secret"].ToString();
        if (header != proxySecret)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "Requests must come through RapidAPI." });
            return;
        }
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "FeedForge", time = DateTime.UtcNow }));

app.MapGet("/formats", () => Results.Ok(new { formats = FormatRegistry.List() }));

// -------- POST /convert?from=google&to=facebook --------
// Gövde: ham feed (XML/CSV/JSON). Yanıt: hedef formatta feed.
app.MapPost("/convert", async (HttpRequest req, HttpResponse res, string? from, string? to, bool? validate) =>
{
    var source = FormatRegistry.Get(from);
    var target = FormatRegistry.Get(to);
    if (source is null || target is null)
        return Results.BadRequest(new
        {
            error = "Geçersiz 'from' veya 'to' parametresi.",
            supported = FormatRegistry.List()
        });

    string raw;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        raw = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(raw))
        return Results.BadRequest(new { error = "İstek gövdesi boş. Ham feed içeriğini gövdede gönderin." });

    var parsed = source.Parse(raw);
    if (parsed.Issues.Any(i => i.Severity == "error") && parsed.Products.Count == 0)
        return Results.BadRequest(new { error = "Feed ayrıştırılamadı.", issues = parsed.Issues });

    var issues = parsed.Issues;
    if (validate == true)
        issues = issues.Concat(FeedValidator.Validate(parsed.Products)).ToList();

    var output = target.Write(parsed.Products);

    // Sorun varsa başlıkla bildir, gövdede saf feed dönsün ki direkt kullanılabilsin
    res.Headers["X-FeedForge-Products"] = parsed.Products.Count.ToString();
    res.Headers["X-FeedForge-Issues"] = issues.Count.ToString();

    return Results.Text(output, target.ContentType, Encoding.UTF8);
});

// -------- POST /convert/json?from=google&to=facebook --------
// Aynı işlem ama yanıt JSON zarfı içinde: feed + istatistik + sorun listesi.
app.MapPost("/convert/json", async (HttpRequest req, string? from, string? to) =>
{
    var source = FormatRegistry.Get(from);
    var target = FormatRegistry.Get(to);
    if (source is null || target is null)
        return Results.BadRequest(new { error = "Geçersiz 'from' veya 'to' parametresi.", supported = FormatRegistry.List() });

    string raw;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        raw = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(raw))
        return Results.BadRequest(new { error = "İstek gövdesi boş." });

    var parsed = source.Parse(raw);
    var issues = parsed.Issues.Concat(FeedValidator.Validate(parsed.Products)).ToList();
    var output = target.Write(parsed.Products);

    return Results.Ok(new
    {
        from = source.Key,
        to = target.Key,
        product_count = parsed.Products.Count,
        error_count = issues.Count(i => i.Severity == "error"),
        warning_count = issues.Count(i => i.Severity == "warning"),
        issues,
        feed = output
    });
});

// -------- POST /validate?format=google --------
app.MapPost("/validate", async (HttpRequest req, string? format) =>
{
    var source = FormatRegistry.Get(format);
    if (source is null)
        return Results.BadRequest(new { error = "Geçersiz 'format' parametresi.", supported = FormatRegistry.List() });

    string raw;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        raw = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(raw))
        return Results.BadRequest(new { error = "İstek gövdesi boş." });

    var parsed = source.Parse(raw);
    var issues = parsed.Issues.Concat(FeedValidator.Validate(parsed.Products)).ToList();

    return Results.Ok(new
    {
        format = source.Key,
        product_count = parsed.Products.Count,
        valid = !issues.Any(i => i.Severity == "error"),
        error_count = issues.Count(i => i.Severity == "error"),
        warning_count = issues.Count(i => i.Severity == "warning"),
        issues
    });
});

app.Run();
