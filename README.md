# Birds Atlas

Responsive full-stack vogelatlas op basis van de aangeleverde desktop-, tablet- en smartphone-mockup.

## Functies

- Live zoeken op Nederlandse, Engelse en wetenschappelijke naam via iNaturalist.
- Taxonomische filters voor orde, familie en genus.
- Continentfilters die soorten controleren op actuele GBIF-occurrences.
- Responsive grid: 4 kolommen desktop, 3 tablet en 2 smartphone.
- Detailweergave met foto- en licentiebron, taxonomie, Wikipedia-samenvatting, verspreiding en externe bronlinks.
- Leaflet-kaart met GBIF occurrence-punten en vergelijking van maximaal vier soorten.
- Xeno-canto audio-integratie wanneer een API-key is ingesteld.
- Favorieten en persoonlijke lifelist in `localStorage`.
- Profielstatistieken voor geziene soorten en continenten.

## Structuur

- `frontend/` - Angular standalone-app.
- `backend/BirdsAtlas.Api/` - .NET 8 minimal API die iNaturalist, GBIF, Wikipedia en Xeno-canto combineert.

## Lokaal starten

### Backend

```bash
cd backend/BirdsAtlas.Api
dotnet restore
dotnet run
```

De API draait standaard op `http://localhost:5078`.

Optioneel voor Xeno-canto:

```bash
export XENO_CANTO_API_KEY="jouw-api-key"
```

### Frontend

```bash
cd frontend
npm install
npm start
```

De Angular-app draait standaard op `http://localhost:4200` en proxyt `/api` naar de .NET API.

## Databronnen

- iNaturalist: soortnamen, actuele taxondata en gelicentieerde foto's.
- GBIF: taxonomische match en occurrence-punten.
- Wikipedia/MediaWiki: samenvattingen.
- Xeno-canto: geluidsopnames, indien een API-key is geconfigureerd.

Externe records kunnen onvolledig zijn. De interface toont daarom duidelijke lege staten en bronlinks wanneer een bron geen data teruggeeft.
