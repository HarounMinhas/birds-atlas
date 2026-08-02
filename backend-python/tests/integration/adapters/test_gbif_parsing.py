import pytest

from birds_atlas.adapters.outbound.cache.memory import InMemoryTtlCacheAdapter
from birds_atlas.adapters.outbound.http.gbif import GbifHttpAdapter
from birds_atlas.domain.exceptions import UpstreamInvalidResponseError


class StubHttp:
    def __init__(self, payload):
        self.payload = payload

    async def get_json(self, *args, **kwargs):
        return self.payload


@pytest.mark.asyncio
async def test_gbif_match_rejects_higher_rank() -> None:
    adapter = GbifHttpAdapter(
        StubHttp({"matchType": "HIGHERRANK", "rank": "GENUS"}),
        InMemoryTtlCacheAdapter(),
    )
    result = await adapter.match_species("Turdus merula", required=True)
    assert result.usage_key is None


@pytest.mark.asyncio
async def test_occurrence_rejects_invalid_coordinates() -> None:
    adapter = GbifHttpAdapter(
        StubHttp(
            {
                "results": [
                    {
                        "key": 1,
                        "decimalLatitude": 100,
                        "decimalLongitude": 5,
                    }
                ]
            }
        ),
        InMemoryTtlCacheAdapter(),
    )
    with pytest.raises(UpstreamInvalidResponseError):
        await adapter.occurrences(1, 10)
