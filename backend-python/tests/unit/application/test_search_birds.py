import asyncio
from typing import Any

import pytest

from birds_atlas.application.use_cases.search_birds import SearchBirds
from birds_atlas.domain.enums import Continent, SortOption
from birds_atlas.domain.models import (
    AVES_TAXON_ID,
    Bird,
    BirdSearchQuery,
    GbifTaxonomy,
    TaxonPage,
)
from conftest import FakeCache, FakeCatalog, FakeGbif


@pytest.mark.asyncio
async def test_search_enriches_and_filters() -> None:
    use_case = SearchBirds(FakeCatalog(), FakeGbif(), FakeCache())
    result = await use_case.execute(BirdSearchQuery(continent=None))
    assert result.total == 1
    assert result.items[0].family == "Turdidae"


@pytest.mark.asyncio
async def test_invalid_taxonomy_returns_empty() -> None:
    use_case = SearchBirds(FakeCatalog(), FakeGbif(), FakeCache())
    result = await use_case.execute(BirdSearchQuery(order="Missing"))
    assert result.items == ()
    assert result.total == 0


def bird(bird_id: int, common_name: str) -> Bird:
    return Bird(
        id=bird_id,
        scientific_name=f"Species {bird_id}",
        common_name=common_name,
        english_name=common_name,
        image_url=None,
        photo_attribution=None,
        photo_license=None,
        iucn_status="LC",
        observation_count=bird_id,
        ancestor_ids=frozenset({AVES_TAXON_ID}),
    )


class NameOrderCatalog:
    birds = (
        bird(1, "Zulu"),
        bird(2, "Omega"),
        bird(3, "Alpha"),
    )

    async def search_page(self, **kwargs: Any) -> TaxonPage:
        assert kwargs["order_by"] == "id"
        return TaxonPage(len(self.birds), self.birds)


class NameOrderGbif:
    async def match_species(
        self, scientific_name: str, *, required: bool
    ) -> GbifTaxonomy:
        return GbifTaxonomy(int(scientific_name.split()[-1]))

    async def has_occurrence(self, gbif_key: int, continent: str) -> bool:
        return gbif_key == 3 and continent == Continent.EUROPE.value


@pytest.mark.asyncio
async def test_continent_name_sort_scans_global_name_order_before_cap() -> None:
    use_case = SearchBirds(
        NameOrderCatalog(),
        NameOrderGbif(),
        FakeCache(),
        max_continent_source_scan=2,
    )
    result = await use_case.execute(
        BirdSearchQuery(
            page_size=1,
            continent=Continent.EUROPE,
            sort=SortOption.NAME,
        )
    )

    assert [item.common_name for item in result.items] == ["Alpha"]
    assert result.is_estimate is True


class ConcurrentCatalog:
    item = bird(1, "Alpha")

    async def search_page(self, **kwargs: Any) -> TaxonPage:
        return TaxonPage(1, (self.item,))


class ConcurrencyTrackingGbif:
    def __init__(self) -> None:
        self.active_matches = 0
        self.max_active_matches = 0
        self.active_occurrences = 0
        self.max_active_occurrences = 0

    async def match_species(
        self, scientific_name: str, *, required: bool
    ) -> GbifTaxonomy:
        self.active_matches += 1
        self.max_active_matches = max(self.max_active_matches, self.active_matches)
        await asyncio.sleep(0.01)
        self.active_matches -= 1
        return GbifTaxonomy(1)

    async def has_occurrence(self, gbif_key: int, continent: str) -> bool:
        self.active_occurrences += 1
        self.max_active_occurrences = max(
            self.max_active_occurrences,
            self.active_occurrences,
        )
        await asyncio.sleep(0.01)
        self.active_occurrences -= 1
        return True


@pytest.mark.asyncio
async def test_concurrency_gates_are_shared_across_requests() -> None:
    taxonomy = ConcurrencyTrackingGbif()
    use_case = SearchBirds(
        ConcurrentCatalog(),
        taxonomy,
        FakeCache(),
        enrichment_parallelism=1,
        occurrence_parallelism=1,
        max_continent_source_scan=1,
    )
    query = BirdSearchQuery(
        page_size=1,
        continent=Continent.EUROPE,
        sort=SortOption.OBSERVATIONS,
    )

    await asyncio.gather(use_case.execute(query), use_case.execute(query))

    assert taxonomy.max_active_matches == 1
    assert taxonomy.max_active_occurrences == 1
