# 🐦 Birds Atlas

Full-stack bird encyclopedia — **zero local database**.
All data is fetched **live on-the-fly** from open biodiversity APIs.

## Architecture

```
Angular 17 UI
      ↓ HTTP
.NET 8 Web API  (BirdAggregatorService)
      ├─ GbifService      → GBIF Species API  (species list, taxonomy, continent occurrence)
      ├─ InatService      → iNaturalist API   (photos, common names, Wikipedia descriptions)
      └─ XenoCantoService → Xeno-canto API     (bird sound recordings)

Cache: IMemoryCache (30–60 min TTL — no persistence, server-reboot safe)
```

> No database. No migrations. No sync jobs.  
> `dotnet run` → app works immediately.

## Endpoints

| Method | URL | Description |
|---|---|---|
| GET | `/api/birds` | Search/filter birds live |
| GET | `/api/birds/{gbifKey}` | Full detail (GBIF + iNat + Xeno-canto) |
| GET | `/api/birds/filters` | Continents + orders for dropdowns |
| GET | `/api/taxonomy/orders` | All bird orders |

## Filters

| Parameter | Example | Source |
|---|---|---|
| `search` | `robin` | GBIF species search |
| `continent` | `EUROPE` | GBIF occurrence check |
| `order` | `Passeriformes` | GBIF taxonomy |
| `family` | `Turdidae` | GBIF taxonomy |
| `genus` | `Turdus` | GBIF taxonomy |

## Data Sources

| API | Used for | Auth |
|---|---|---|
| [GBIF](https://www.gbif.org/) | Species list, taxonomy, continent distribution | None |
| [iNaturalist](https://www.inaturalist.org/) | Photos, common names, descriptions | None |
| [Xeno-canto](https://xeno-canto.org/) | Bird sound recordings | None |

## Getting Started

```bash
# Backend
cd BirdsAtlas.API
dotnet restore
dotnet run
# Swagger UI: http://localhost:5000/swagger

# Frontend
cd birds-atlas-ui
npm install
ng serve
# App: http://localhost:4200
```

## Branch

All code lives on the `public` branch.
