import asyncio

import pytest

from birds_atlas.adapters.outbound.cache.memory import InMemoryTtlCacheAdapter


@pytest.mark.asyncio
async def test_cache_hit_miss_and_expiry() -> None:
    cache = InMemoryTtlCacheAdapter()
    assert await cache.get("key") is None
    await cache.set("key", "value", 1)
    assert await cache.get("key") == "value"
    await asyncio.sleep(1.01)
    assert await cache.get("key") is None
