import pytest

from birds_atlas.adapters.outbound.cache.memory import InMemoryTtlCacheAdapter
from birds_atlas.adapters.outbound.http.inaturalist import INaturalistHttpAdapter
from birds_atlas.domain.exceptions import UpstreamInvalidResponseError


class StubHttp:
    def __init__(self, payload):
        self.payload = payload

    async def get_json(self, *args, **kwargs):
        return self.payload


@pytest.mark.asyncio
async def test_inaturalist_parses_missing_photo_as_none() -> None:
    payload = {
        "total_results": 1,
        "results": [
            {
                "id": 1,
                "name": "Turdus merula",
                "observations_count": 1,
                "names": [],
            }
        ],
    }
    adapter = INaturalistHttpAdapter(
        StubHttp(payload), InMemoryTtlCacheAdapter()
    )
    page = await adapter.search_page(
        query="",
        page=1,
        page_size=24,
        taxon_id=3,
        order_by="observations_count",
    )
    assert page.items[0].image_url is None


@pytest.mark.asyncio
async def test_inaturalist_rejects_wrong_type() -> None:
    payload = {
        "total_results": 1,
        "results": [
            {
                "id": "bad",
                "name": "Turdus merula",
                "observations_count": 1,
            }
        ],
    }
    adapter = INaturalistHttpAdapter(
        StubHttp(payload), InMemoryTtlCacheAdapter()
    )
    with pytest.raises(UpstreamInvalidResponseError):
        await adapter.search_page(
            query="",
            page=1,
            page_size=24,
            taxon_id=3,
            order_by="observations_count",
        )
