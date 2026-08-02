import pytest

from birds_atlas.domain.enums import Continent, SortOption
from birds_atlas.domain.exceptions import DomainValidationError
from birds_atlas.domain.models import BirdSearchQuery


def test_query_normalization_and_defaults() -> None:
    query = BirdSearchQuery.normalized(
        query="  merel ",
        page=1,
        page_size=24,
        continent="north america",
        order=None,
        family=None,
        genus=None,
        sort="NAME",
    )
    assert query.query == "merel"
    assert query.continent is Continent.NORTH_AMERICA
    assert query.sort is SortOption.NAME


def test_deep_continent_page_is_rejected() -> None:
    with pytest.raises(DomainValidationError):
        BirdSearchQuery(page=12, page_size=24, continent=Continent.EUROPE)
