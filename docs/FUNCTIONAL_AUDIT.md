# Functional audit (2026-08-02)

Startpunt: `59b352aaa2a66afb40be5d0de1382f15c95a4336`. De audit gebruikt uitsluitend fake
ports en gemockte HTTP-responses; een Preview-smoke blijft bewust handmatig.

## Inventaris van gebruikersflows

Legenda ernst: **H** blokkeert of misleidt, **M** degradeert, **L** geen afwijking gevonden.
`App` verwijst naar `frontend/src/app/app.component.{ts,html}`, `Search` naar
`application/use_cases/search_birds.py`, `Detail/Map/Tax` naar de overeenkomstige use
case en outbound adapter.

| # | Flow | Verwacht en vastgesteld gedrag | Code | Afwijking / ernst | Regressie |
|---:|---|---|---|---|---|
| 1 | Applicatiestart | lokale state, taxonomie en lijst laden onafhankelijk | App, API | geen / L | build + API |
| 2 | Initiële lijst | 24 soorten en exact upstreamtotaal | App, Search, iNat | geen / L | API default |
| 3 | Nederlandse naam | `q` doorgeven, lokale iNat-namen doorzoeken | App, Search | geen / L | catalog-port |
| 4 | Engelse naam | idem | App, Search | geen / L | catalog-port |
| 5 | Wetenschappelijke naam | idem | App, Search | geen / L | catalog-port |
| 6 | Lege zoekopdracht | genormaliseerd naar `""` | schema, domain | geen / L | domain |
| 7 | Geen resultaat | lege state, totaal nul | App, Search | geen / L | boundary 0 |
| 8 | Snelle zoekopdrachten | vorige subscription en response-token ongeldig | App | geen / L | code-audit |
| 9 | Observatiesortering | stabiele iNat-volgorde met ID-deduplicatie | Search | veranderend upstreamtotaal wordt fout / M | adapter/use-case |
| 10 | Naamsortering | globale naamvolgorde vóór slicing | Search | geen / L | hoge-ID test bestaand |
| 11 | Continent | GBIF-filter binnen begrensde scan | Search, GBIF | totaal groeide per pagina / **H**, hersteld | derde-pagina test |
| 12 | Orde | geresolvede ancestor als catalogus-root | Search, iNat | geen / L | taxonomy |
| 13 | Familie | idem | Search, iNat | geen / L | taxonomy |
| 14 | Genus | idem | Search, iNat | geen / L | taxonomy |
| 15 | Taxonomiecombinatie | meest specifieke taxon moet ancestors bevatten | Search | geen / L | invalid taxonomy |
| 16 | Zoek + sort + filters | één context-key en zelfde pipeline | App, Search | cache-key nu volledig / M | contract/index |
| 17 | Paginering | alleen werkelijk ontdekte volgende pagina | App, Search | afgeleid van fictief totaal / **H**, hersteld | hasNextPage |
| 18 | Laatste pagina | Next uit | App | was totaal-afhankelijk / **H**, hersteld | 24/25/48/49 |
| 19 | Na laatste | leeg, geen hoger totaal of Next | Search | was fictief groter / **H**, hersteld | continent page 3 |
| 20 | Terug | cache-index bewaart volgorde en totaal | Search/cache | instance-lokaal / L | repeated page |
| 21 | Filter vanaf >1 | pagina wordt 1 | App | geen / L | code-audit |
| 22 | Detail openen | loading en detail apart | App, Detail | geen / L | API detail |
| 23 | Twee details snel | unsubscribe + token | App | geen / L | code-audit |
| 24 | Detail sluiten tijdens laden | request ongeldig en state schoon | App | geen / L | code-audit |
| 25 | Favorieten | direct lokaal toevoegen/verwijderen | App | geen / L | local-state audit |
| 26 | Lifelist | direct lokaal toevoegen/verwijderen | App | geen / L | local-state audit |
| 27 | localStorage | parse-fouten degraderen naar leeg | App | objectvorm slechts structureel gevalideerd / M | vervolgtest |
| 28 | Vergelijkselectie | max. vier unieke IDs | App | geen / L | code-audit |
| 29 | Vergelijking reload | IDs plus snapshots hersteld | App | geen / L | code-audit |
| 30 | Kaart | GBIF-coördinaten per soort | App, Occurrences, GBIF | geen / L | API/adapter |
| 31 | Kaart heropenen | map en requests worden vernietigd/herbouwd | App | geen / L | code-audit |
| 32 | Meerdere kaartsoorten | onafhankelijke lagen, max. vier | App | geen / L | code-audit |
| 33 | Partiële occurrence-fout | overige punten blijven, waarschuwing | App | geen / L | code-audit |
| 34 | Profiel | statistiek uit lokale collecties | App | geen / L | code-audit |
| 35 | Wikipedia | optionele summary met hostallowlist | Detail, Wikipedia | geen / L | adaptertests |
| 36 | GBIF-taxonomie | alleen species-match | Detail, GBIF | geen / L | adaptertests |
| 37 | Xeno zonder key | lege recordings | Xeno | geen / L | adaptertest |
| 38 | Xeno met key | genus/species-query, max. drie | Xeno | live secret niet getest / M | Preview |
| 39 | Geen afbeelding | placeholder blijft zichtbaar | App, iNat | geen / L | build/audit |
| 40 | Optionele data ontbreekt | lege/null metadata | Detail/adapters | geen / L | adaptertests |
| 41 | Timeout | 504 verplicht, optioneel degradeert | HTTP/errors | geen / L | HTTP test |
| 42 | 429 | 503 + Retry-After | HTTP/errors | geen / L | HTTP test |
| 43 | 500 | 502 voor verplichte bron | HTTP/errors | geen / L | HTTP test |
| 44 | Ongeldige JSON | typed upstreamfout | HTTP/parsing | geen / L | HTTP test |
| 45 | Onbekend ID | 404 | Detail/Occurrences | geen / L | API test |
| 46 | Niet-vogel-ID | Aves/rank/active-validatie, 404 | domain/use cases | geen / L | domain/API |
| 47 | Mobiel | gedeelde componentstate, responsive CSS | App/CSS | visuele Preview nodig / M | handmatig |
| 48 | Tablet | idem | App/CSS | visuele Preview nodig / M | handmatig |
| 49 | Desktop | idem | App/CSS | visuele Preview nodig / M | build + handmatig |
| 50 | Refresh/Vercel | niet-API SPA fallback, API eerst | vercel, api/index | Preview ontbreekt / M | Preview-smoke |

## Root cause en gekozen pagineringsmodel

De oude continentpipeline stopte zodra `offset + pageSize + 1` matches waren gevonden.
Daarna construeerde ze een `total` uit de aangevraagde offset. Daardoor veranderde dezelfde
zoekcontext tijdens navigatie van betekenis en creëerde iedere diepe pagina haar eigen
bewijs voor nóg een pagina.

De herstelde pipeline bouwt per genormaliseerde zoekcontext één **begrensde, stabiele
resultaatindex**. Zij scant steeds dezelfde bronprefix (maximaal 500 taxa), dedupliceert en
valideert de upstreamvolgorde, filtert die prefix via GBIF en cachet de geordende summaries
15 minuten. `total` is het aantal werkelijk ontdekte matches in die index. `isEstimate=false`
betekent dat iNaturalist volledig uitgeput is; `true` betekent dat `total` een expliciet
ondergrens/scanresultaat is. `hasNextPage` is uitsluitend waar wanneer de index na de huidige
slice al minstens één werkelijk resultaat bevat. De UI maakt dus geen diepe pagina's uit een
schatting. Een lege pagina vergroot niets en biedt nooit Next aan.

Naamordening blijft globaal: eerst wordt de volledige iNaturalist-set gevalideerd en gesorteerd,
dan wordt de vaste continent-scanprefix genomen. Observatieordening bewaart de gecontroleerde
upstreamvolgorde. Scanlimiet en externe concurrencylimieten zijn niet verhoogd.

## Serverless en handmatige controles

De indexcache is een optimalisatie binnen één warme instance. Cold starts en verschillende
Vercel-instances delen hem niet; correctheid is daarom ook bij een miss deterministisch voor één
upstreamsnapshot, maar een werkelijk gewijzigde providerdataset kan tussen instances verschillen.
Een gedeelde duurzame snapshot of cursor-token valt buiten deze wijziging.

Na creatie van de Preview moeten handmatig worden gecontroleerd: Angular root en asset, refresh
op een clientroute, `/api/health`, lijst, detail, occurrences en taxonomy; twee continentpagina's
heen/terug; Xeno-canto met Preview-secret; en 390 px, 768 px en desktop viewports. Noteer URL,
deployment-ID en resultaat in de PR voordat deze uit draft gaat.
