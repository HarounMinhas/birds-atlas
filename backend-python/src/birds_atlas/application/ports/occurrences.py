from typing import Protocol

from birds_atlas.domain.models import OccurrencePoint


class OccurrenceProviderPort(Protocol):
    async def occurrences(self, gbif_key: int, limit: int) -> tuple[OccurrencePoint, ...]: ...
