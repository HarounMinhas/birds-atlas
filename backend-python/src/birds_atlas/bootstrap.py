from __future__ import annotations

from dataclasses import dataclass

from birds_atlas.adapters.outbound.cache.memory import InMemoryTtlCacheAdapter
from birds_atlas.adapters.outbound.clock import SystemClockAdapter
from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.adapters.outbound.http.gbif import GbifHttpAdapter
from birds_atlas.adapters.outbound.http.inaturalist import INaturalistHttpAdapter
from birds_atlas.adapters.outbound.http.wikipedia import WikipediaHttpAdapter
from birds_atlas.adapters.outbound.http.xeno_canto import XenoCantoHttpAdapter
from birds_atlas.application.use_cases.check_health import CheckHealth
from birds_atlas.application.use_cases.get_bird_detail import GetBirdDetail
from birds_atlas.application.use_cases.get_occurrences import GetBirdOccurrences
from birds_atlas.application.use_cases.get_taxonomy_options import GetTaxonomyOptions
from birds_atlas.application.use_cases.search_birds import SearchBirds
from birds_atlas.config.settings import Settings


@dataclass(slots=True)
class Container:
    search_birds: SearchBirds
    get_bird_detail: GetBirdDetail
    get_occurrences: GetBirdOccurrences
    get_taxonomy_options: GetTaxonomyOptions
    check_health: CheckHealth
    http_clients: tuple[AsyncHttpClient, ...]

    async def close(self) -> None:
        for client in self.http_clients:
            await client.aclose()


def build_container(settings: Settings | None = None) -> Container:
    settings = settings or Settings.from_environment()
    cache = InMemoryTtlCacheAdapter()
    inat_http = AsyncHttpClient(
        provider="iNaturalist",
        base_url=settings.inaturalist_base_url,
        connect_timeout=settings.http_connect_timeout_seconds,
        read_timeout=settings.http_read_timeout_seconds,
    )
    gbif_http = AsyncHttpClient(
        provider="GBIF",
        base_url=settings.gbif_base_url,
        connect_timeout=settings.http_connect_timeout_seconds,
        read_timeout=settings.http_read_timeout_seconds,
    )
    wiki_http = AsyncHttpClient(
        provider="Wikipedia",
        base_url=settings.wikipedia_base_url,
        connect_timeout=settings.http_connect_timeout_seconds,
        read_timeout=settings.http_read_timeout_seconds,
        follow_redirects=False,
    )
    xeno_http = AsyncHttpClient(
        provider="Xeno-canto",
        base_url=settings.xeno_canto_base_url,
        connect_timeout=settings.http_connect_timeout_seconds,
        read_timeout=settings.http_read_timeout_seconds,
    )
    catalog = INaturalistHttpAdapter(inat_http, cache)
    gbif = GbifHttpAdapter(gbif_http, cache)
    wikipedia = WikipediaHttpAdapter(wiki_http)
    xeno = XenoCantoHttpAdapter(xeno_http, settings.xeno_canto_api_key)
    return Container(
        search_birds=SearchBirds(catalog, gbif, cache),
        get_bird_detail=GetBirdDetail(catalog, gbif, wikipedia, xeno),
        get_occurrences=GetBirdOccurrences(catalog, gbif, gbif),
        get_taxonomy_options=GetTaxonomyOptions(catalog, cache),
        check_health=CheckHealth(SystemClockAdapter()),
        http_clients=(inat_http, gbif_http, wiki_http, xeno_http),
    )
