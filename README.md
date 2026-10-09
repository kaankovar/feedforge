# FeedForge — Ürün Feed Dönüştürücü API

E-ticaret ürün feed'lerini formatlar arasında dönüştüren ve doğrulayan REST API.

Desteklenen formatlar: `google` (Merchant XML), `facebook` (Catalog CSV), `trendyol` (XML), `json`, `csv`.

## Lokal çalıştırma

Gereksinim: .NET 8 SDK (https://dotnet.microsoft.com/download)

```bash
cd feedforge
dotnet run
```

Varsayılan adres genelde `http://localhost:5000` (konsolda yazar).

## Test

```bash
# Sağlık kontrolü
curl http://localhost:5000/health

# Format listesi
curl http://localhost:5000/formats

# Google -> Facebook dönüşümü
curl -X POST "http://localhost:5000/convert?from=google&to=facebook" \
  -H "Content-Type: application/xml" \
  --data-binary @samples/sample-google.xml

# Google -> Trendyol, JSON zarfıyla (sorun listesi dahil)
curl -X POST "http://localhost:5000/convert/json?from=google&to=trendyol" \
  --data-binary @samples/sample-google.xml

# Feed doğrulama
curl -X POST "http://localhost:5000/validate?format=google" \
  --data-binary @samples/sample-google.xml
```

## Endpoint'ler

| Endpoint | Açıklama |
|---|---|
| `POST /convert?from=X&to=Y` | Gövdedeki ham feed'i dönüştürür, saf feed döner. `&validate=true` eklersen sorun sayıları yanıt başlıklarında gelir. |
| `POST /convert/json?from=X&to=Y` | Aynı dönüşüm, JSON zarfında: `feed` + `issues` + istatistikler. |
| `POST /validate?format=X` | Feed'i doğrular; hata/uyarı listesi döner (eksik GTIN, hatalı fiyat, tekrar eden ID vb.). |
| `GET /formats` | Desteklenen formatlar. |
| `GET /health` | Sağlık kontrolü. |
