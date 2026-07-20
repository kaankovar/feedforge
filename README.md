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

## Deploy (Render örneği — ücretsiz plan)

1. Projeyi bir GitHub reposuna pushla.
2. Köke şu `Dockerfile`'ı ekle:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FeedForge.dll"]
```

3. render.com → New → Web Service → repo'yu seç → Runtime: Docker.
4. Deploy bitince `https://<servis-adin>.onrender.com/health` ile kontrol et.

## RapidAPI'ye ekleme

1. https://rapidapi.com/provider → Add New API.
2. Base URL olarak Render adresini gir.
3. Endpoint'leri tanımla (`/convert`, `/convert/json`, `/validate`, `/formats`) — her birine örnek istek/yanıt ekle.
4. **Güvenlik:** RapidAPI, Provider ayarlarında sana bir "Proxy Secret" verir. Render'da
   `RAPIDAPI_PROXY_SECRET` ortam değişkenine bu değeri gir. Böylece API sadece RapidAPI
   üzerinden gelen istekleri kabul eder.
5. Fiyat planları (öneri):
   - **Basic (ücretsiz):** 50 istek/ay — insanların denemesi için şart.
   - **Pro (~$10/ay):** 5.000 istek/ay.
   - **Ultra (~$25/ay):** 25.000 istek/ay + aşım ücreti.
6. API açıklamasını İngilizce yaz, "product feed converter", "Google Merchant",
   "Facebook catalog" gibi aranan anahtar kelimeleri kullan — RapidAPI içi aramada bulunmak
   satışın en büyük kaynağı.
