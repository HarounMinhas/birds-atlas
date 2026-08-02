from typing import Protocol

from birds_atlas.domain.models import GbifTaxonomy


class TaxonomyProviderPort(Protocol):
    async def match_species(self, scientific_name: str, *, required: bool) -> GbifTaxonomy: ...

    async def continents(self, gbif_key: int) -> tuple[str, ...]: ...

    async def has_occurrence(self, gbif_key: int, continent: str) -> bool: ...
