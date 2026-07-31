# 🐦 Birds Atlas

A full-stack bird encyclopedia app powered by open-source biodiversity data.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 17 + Bootstrap 5 |
| Backend | .NET 8 Web API (C#) |
| Database | SQL Server + Entity Framework Core 8 |
| Sync | Hangfire background jobs |
| Data Sources | GBIF API, iNaturalist API, Xeno-canto API |

## Features

- 🌍 Filter by continent (Europe, Africa, Asia, Americas, Oceania)
- 🔬 Filter by taxonomy (Order → Family → Genus)
- 🎨 Filter by physical traits (beak color, breast color, size, habitat)
- 🖼️ Bird photos from iNaturalist
- 🔊 Bird sounds from Xeno-canto
- 🔄 Daily background sync from GBIF + iNaturalist

## Getting Started

### Backend
```bash
cd BirdsAtlas.API
dotnet restore
# Set connection string in appsettings.json
dotnet ef database update
dotnet run
```

### Frontend
```bash
cd birds-atlas-ui
npm install
ng serve
```

## Data Sources

- [GBIF](https://www.gbif.org/) — taxonomy, species lists, continent distribution
- [iNaturalist](https://www.inaturalist.org/) — photos, observation data
- [Xeno-canto](https://xeno-canto.org/) — bird sound recordings
- [eBird](https://ebird.org/) — regional observation hotspots
