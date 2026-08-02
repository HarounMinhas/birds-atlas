import asyncio
from dataclasses import dataclass

from birds_atlas.application.ports.bird_catalog import BirdCatalogPort
from birds_atlas.application.ports.cache import CachePort
from birds_atlas.domain.models import TaxonomyOptions


@dataclass(slots=True)
class GetTaxonomyOptions:
    catalog: BirdCatalogPort
    cache: CachePort

    async def execute(self) -> TaxonomyOptions:
        cached = await self.cache.get("taxonomy-options")
        if isinstance(cached, TaxonomyOptions):
            return cached
        orders, families, genera = await asyncio.gather(
            self.catalog.taxonomy_names(rank="order", page_size=100),
            self.catalog.taxonomy_names(rank="family", page_size=200),
            self.catalog.taxonomy_names(rank="genus", page_size=200),
        )
        result = TaxonomyOptions(orders=orders, families=families, genera=genera)
        await self.cache.set("taxonomy-options", result, 86_400)
        return result
