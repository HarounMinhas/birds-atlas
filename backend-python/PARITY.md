# .NET naar FastAPI parity-checklist

## Routes en contracten

- [x] `GET /api/health`
- [x] `GET /api/birds` met `q`, `page`, `pageSize`, `continent`, `order`, `family`, `genus`, `sort`
- [x] `GET /api/birds/{id}`
- [x] `GET /api/birds/{id}/occurrences` met `limit`
- [x] `GET /api/taxonomy/options`
- [x] camelCase JSON en bestaande null/lege-arraybetekenis
- [x] 404 voor onbekende of niet-Aves species-ID's
- [x] upstreamfouten onderscheiden van geldige lege resultaten

## Databronnen

- [x] iNaturalist taxa, lokale namen, foto's, licenties, IUCN en filters
- [x] GBIF species-match met EXACT/FUZZY + SPECIES-validatie
- [x] GBIF continentfacets, continentchecks en occurrences
- [x] Wikipedia HTTPS-hostallowlist en redirectblokkering
- [x] Xeno-canto genus/species-query en lege resultaten zonder key

## Gedrag

- [x] observatie- en naamordening
- [x] begrensde parallelle GBIF-verrijking
- [x] begrensde continentfan-out en diepe-offsetbeveiliging
- [x] datumintervallen/partiële datums degraderen naar `null`
- [x] in-memory TTL-cache als niet-durzame optimalisatie
- [x] stabiele begrensde continentindex met expliciete `hasNextPage`
- [x] exacte (`isEstimate=false`) en begrensde (`isEstimate=true`) totalen onderscheiden

## Validatie

- [x] `python -m compileall backend-python`
- [x] grens-, regressie-, adapter-, API- en contracttests zonder live netwerk
- [x] Angular production-build in de uitvoeromgeving
- [ ] Vercel Preview-smoketest met live externe APIs
- [ ] Xeno-canto live test met een Preview-secret

De .NET-backend blijft staan totdat de open deploymentchecks zijn bevestigd.

De volledige 50-flow-audit, pagineringssemantiek, bekende serverlessbeperking en verplichte
Preview-smoke staan in `docs/FUNCTIONAL_AUDIT.md`.
