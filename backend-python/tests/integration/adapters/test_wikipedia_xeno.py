import httpx
import pytest

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
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
async def test_wikipedia_uses_configured_base_url() -> None:
    requested_urls: list[str] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requested_urls.append(str(request.url))
        return httpx.Response(200, json={"extract": "A bird"}, request=request)

    client = AsyncHttpClient(
        provider="Wikipedia",
        base_url="https://proxy.example/wiki/",
        connect_timeout=1,
        read_timeout=1,
        follow_redirects=False,
    )
    original_client = client.client
    client.client = httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="https://proxy.example/wiki/",
        follow_redirects=False,
    )
    await original_client.aclose()

    try:
        result = await WikipediaHttpAdapter(client).summary(None, "Turdus merula")
    finally:
        await client.aclose()

    assert result.summary == "A bird"
    assert requested_urls == ["https://proxy.example/wiki/api/rest_v1/page/summary/Turdus_merula"]


@pytest.mark.asyncio
async def test_xeno_without_key_is_empty() -> None:
    adapter = XenoCantoHttpAdapter(StubHttp({}), None)
    assert await adapter.recordings("Turdus merula") == ()
