from typing import Protocol

from birds_atlas.domain.models import EncyclopediaEntry


class EncyclopediaProviderPort(Protocol):
    async def summary(self, wikipedia_url: str | None, scientific_name: str) -> EncyclopediaEntry: ...
