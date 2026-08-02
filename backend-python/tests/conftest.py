from __future__ import annotations

from datetime import UTC, datetime
from typing import Any

import pytest
from fastapi.testclient import TestClient

from birds_atlas.adapters.inbound.fastapi.app import create_app
from birds_atlas.application.use_cases.check_health import CheckHealth
from birds_atlas.application.use_cases.get_bird_detail import GetBirdDetail
from birds_atlas.application.use_cases.get_occurrences import GetBirdOccurrences
from birds_atlas.application.use_cases.get_taxonomy_options import GetTaxonomyOptions
from birds_atlas.application.use_cases.search_birds import SearchBirds
from birds_atlas.bootstrap import Container
from birds_atlas.domain.models import (
    AVES_TAXON_ID,
    Bird,
    EncyclopediaEntry,
    GbifTaxonomy,
    OccurrencePoint,
    ResolvedTaxon,
    TaxonPage,
)


class FakeCache:
    def __init__(self) -> None:
        self.values: dict[str, object] = {}

    async def get(self, key: str) -> object | None:
        return self.values.get(key)

    async def set(self, key: str, value: object, ttl_seconds: int) -> None:
        self.values[key] = value


class FakeCatalog:
    bird = Bird(
        id=1,
        scientific_name="Turdus merula",
        common_name="Merel",
        english_name="Common Blackbird",
        image_url=None,
        photo_attribution=None,
        photo_license=None,
        iucn_status="LC",
        observation_count=42,
        wikipedia_url="https://nl.wikipedia.org/wiki/Merel",
        ancestor_ids=frozenset({AVES_TAXON_ID}),
    )

    async def search_page(self, **kwargs: Any) -> TaxonPage:
        if kwargs["page"] == 1:
            return TaxonPage(1, (self.bird,))
        return TaxonPage(1, ())

    async def get_bird(self, bird_id: int) -> Bird | None:
        return self.bird if bird_id == 1 else None

    async def resolve_taxon(self, *, rank: str, name: str) -> ResolvedTaxon | None:
        if name == "Valid":
            return ResolvedTaxon(10, rank, frozenset({AVES_TAXON_ID}))
        return None

    async def taxonomy_names(self, *, rank: str, page_size: int) -> tuple[str, ...]:
        return {
            "order": ("Passeriformes",),
            "family": ("Turdidae",),
            "genus": ("Turdus",),
        }[rank]


class FakeGbif:
    async def match_species(self, scientific_name: str, *, required: bool) -> GbifTaxonomy:
        return GbifTaxonomy(
            100,
            "Animalia",
            "Chordata",
            "Aves",
            "Passeriformes",
            "Turdidae",
            "Turdus",
            scientific_name,
        )

    async def continents(self, gbif_key: int) -> tuple[str, ...]:
        return ("EUROPE",)

    async def has_occurrence(self, gbif_key: int, continent: str) -> bool:
        return continent == "EUROPE"

    async def occurrences(self, gbif_key: int, limit: int) -> tuple[OccurrencePoint, ...]:
        return (
            OccurrencePoint(
                7,
                52.0,
                5.0,
                "Netherlands",
                "Utrecht",
                None,
                "https://www.gbif.org/occurrence/7",
            ),
        )


class FakeWikipedia:
    async def summary(self, wikipedia_url: str | None, scientific_name: str) -> EncyclopediaEntry:
        return EncyclopediaEntry("Summary", wikipedia_url)


class FakeRecordings:
    async def recordings(self, scientific_name: str) -> tuple[Any, ...]:
        return ()


class FakeClock:
    def utcnow(self) -> datetime:
        return datetime(2026, 8, 2, tzinfo=UTC)


@pytest.fixture
def fake_container() -> Container:
    catalog = FakeCatalog()
    gbif = FakeGbif()
    cache = FakeCache()
    return Container(
        search_birds=SearchBirds(catalog, gbif, cache),
        get_bird_detail=GetBirdDetail(catalog, gbif, FakeWikipedia(), FakeRecordings()),
        get_occurrences=GetBirdOccurrences(catalog, gbif, gbif),
        get_taxonomy_options=GetTaxonomyOptions(catalog, cache),
        check_health=CheckHealth(FakeClock()),
        http_clients=(),
    )


@pytest.fixture
def client(fake_container: Container) -> TestClient:
    with TestClient(create_app(fake_container)) as test_client:
        yield test_client
