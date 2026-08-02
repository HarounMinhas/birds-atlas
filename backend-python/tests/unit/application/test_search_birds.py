import pytest

from birds_atlas.application.use_cases.search_birds import SearchBirds
from birds_atlas.domain.models import BirdSearchQuery
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
