# Migrations

No migrations needed — this app has **no local database**.

All bird data is fetched live from:
- GBIF API (`https://api.gbif.org/v1/`)
- iNaturalist API (`https://api.inaturalist.org/v1/`)
- Xeno-canto API (`https://xeno-canto.org/api/2/`)

Results are cached in `IMemoryCache` for 30–60 minutes to stay responsive without hammering external APIs.
