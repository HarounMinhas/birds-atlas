from __future__ import annotations

from typing import Protocol

from birds_atlas.domain.models import Bird, ResolvedTaxon, TaxonPage


class BirdCatalogPort(Protocol):
    async def search_page(
        self,
        *,
        query: str,
        page: int,
        page_size: int,
        taxon_id: int,
        order_by: str,
    ) -> TaxonPage: ...

    async def get_bird(self, bird_id: int) -> Bird | None: ...

    async def resolve_taxon(self, *, rank: str, name: str) -> ResolvedTaxon | None: ...

    async def taxonomy_names(self, *, rank: str, page_size: int) -> tuple[str, ...]: ...
