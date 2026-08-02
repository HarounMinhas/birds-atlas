import pytest

from birds_atlas.adapters.outbound.http.wikipedia import WikipediaHttpAdapter
from birds_atlas.adapters.outbound.http.xeno_canto import XenoCantoHttpAdapter


class StubHttp:
    def __init__(self, payload):
        self.payload = payload
        self.calls = []

    async def get_json(self, *args, **kwargs):
        self.calls.append((args, kwargs))
        return self.payload


@pytest.mark.asyncio
async def test_wikipedia_rejects_unsafe_source_and_uses_fallback() -> None:
    http = StubHttp(
        {
            "extract": "A bird",
            "content_urls": {"desktop": {"page": "https://en.wikipedia.org/wiki/Turdus_merula"}},
        }
    )
    adapter = WikipediaHttpAdapter(http)
    result = await adapter.summary("https://attackerwikipedia.org/wiki/x", "Turdus merula")
    assert result.url == "https://en.wikipedia.org/wiki/Turdus_merula"


@pytest.mark.asyncio
async def test_xeno_without_key_is_empty() -> None:
    adapter = XenoCantoHttpAdapter(StubHttp({}), None)
    assert await adapter.recordings("Turdus merula") == ()
