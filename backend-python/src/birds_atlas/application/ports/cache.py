from typing import Protocol


class CachePort(Protocol):
    async def get(self, key: str) -> object | None: ...

    async def set(self, key: str, value: object, ttl_seconds: int) -> None: ...
