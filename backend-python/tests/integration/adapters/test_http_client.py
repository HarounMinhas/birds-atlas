import httpx
import pytest

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.domain.exceptions import (
    UpstreamInvalidResponseError,
    UpstreamRateLimitError,
    UpstreamTimeoutError,
    UpstreamUnavailableError,
)


def adapter(handler):
    client = AsyncHttpClient(
        provider="test",
        base_url="https://example.test/",
        connect_timeout=1,
        read_timeout=1,
    )
    old = client.client
    client.client = httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="https://example.test/",
    )
    return client, old


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("status", "exception"),
    [(429, UpstreamRateLimitError), (500, UpstreamUnavailableError)],
)
async def test_status_translation(status, exception) -> None:
    client, old = adapter(lambda request: httpx.Response(status, json={}))
    await old.aclose()
    with pytest.raises(exception):
        await client.get_json("resource")
    await client.aclose()


@pytest.mark.asyncio
async def test_invalid_json_translation() -> None:
    client, old = adapter(lambda request: httpx.Response(200, content=b"not-json"))
    await old.aclose()
    with pytest.raises(UpstreamInvalidResponseError):
        await client.get_json("resource")
    await client.aclose()


@pytest.mark.asyncio
async def test_timeout_translation() -> None:
    def timeout(request):
        raise httpx.ReadTimeout("timeout", request=request)

    client, old = adapter(timeout)
    await old.aclose()
    with pytest.raises(UpstreamTimeoutError):
        await client.get_json("resource")
    await client.aclose()
