from __future__ import annotations

import asyncio
from dataclasses import dataclass

from birds_atlas.application.ports.bird_catalog import BirdCatalogPort
from birds_atlas.application.ports.cache import CachePort
from birds_atlas.application.ports.taxonomy import TaxonomyProviderPort
from birds_atlas.domain.exceptions import UpstreamInvalidResponseError
from birds_atlas.domain.models import (
    AVES_TAXON_ID,
    Bird,
    BirdSearchQuery,
    BirdSearchResult,
    BirdSummary,
    ResolvedTaxon,
)
from birds_atlas.domain.services import display_name, taxonomy_rank_weight, to_summary


@dataclass(slots=True)
class SearchBirds:
    catalog: BirdCatalogPort
    taxonomy: TaxonomyProviderPort
    cache: CachePort
    enrichment_parallelism: int = 8
    occurrence_parallelism: int = 12
    max_continent_source_scan: int = 500

    async def execute(self, query: BirdSearchQuery) -> BirdSearchResult:
        taxon_id = await self._resolve_taxonomy(query)
        if taxon_id is None:
            return BirdSearchResult(query.page, query.page_size, 0, False, ())

        if query.continent is None:
            birds, total = await self._page_without_continent(query, taxon_id)
            return BirdSearchResult(
                page=query.page,
                page_size=query.page_size,
                total=total,
                is_estimate=False,
                items=tuple(await self._enrich(birds)),
            )

        matches, exhausted = await self._continent_matches(query, taxon_id)
        items = tuple(matches[query.offset : query.offset + query.page_size])
        if exhausted:
            total = len(matches)
            estimate = False
        else:
            total = max(
                query.offset + len(items) + 1,
                query.page * query.page_size + 1,
            )
            estimate = True
        return BirdSearchResult(
            query.page,
            query.page_size,
            total,
            estimate,
            items,
        )

    async def _resolve_taxonomy(self, query: BirdSearchQuery) -> int | None:
        requested = [
            ("order", query.order),
            ("family", query.family),
            ("genus", query.genus),
        ]
        resolved: list[ResolvedTaxon] = []
        for rank, name in requested:
            if not name:
                continue
            result = await self.catalog.resolve_taxon(rank=rank, name=name)
            if result is None:
                return None
            resolved.append(result)
        if not resolved:
            return AVES_TAXON_ID
        most_specific = max(
            resolved,
            key=lambda item: taxonomy_rank_weight(item.rank),
        )
        for item in resolved:
            if (
                item.id != most_specific.id
                and item.id not in most_specific.ancestor_ids
            ):
                return None
        return most_specific.id

    async def _page_without_continent(
        self, query: BirdSearchQuery, taxon_id: int
    ) -> tuple[tuple[Bird, ...], int]:
        if query.sort.value != "name":
            page = await self.catalog.search_page(
                query=query.query,
                page=query.page,
                page_size=query.page_size,
                taxon_id=taxon_id,
                order_by="observations_count",
            )
            return page.items, page.total

        ordered = await self._all_name_ordered(query.query, taxon_id)
        start = query.offset
        return ordered[start : start + query.page_size], len(ordered)

    async def _all_name_ordered(
        self, search: str, taxon_id: int
    ) -> tuple[Bird, ...]:
        cache_key = f"bird-name-order:{taxon_id}:{search.casefold()}"
        cached = await self.cache.get(cache_key)
        if isinstance(cached, tuple) and all(
            isinstance(item, Bird) for item in cached
        ):
            return cached

        page_number = 1
        expected_total: int | None = None
        birds: list[Bird] = []
        seen: set[int] = set()
        while expected_total is None or len(birds) < expected_total:
            page = await self.catalog.search_page(
                query=search,
                page=page_number,
                page_size=200,
                taxon_id=taxon_id,
                order_by="id",
            )
            expected_total = (
                page.total if expected_total is None else expected_total
            )
            if page.total != expected_total:
                raise UpstreamInvalidResponseError(
                    "iNaturalist",
                    "total changed during name pagination",
                )
            if not page.items:
                break
            for bird in page.items:
                if bird.id in seen:
                    raise UpstreamInvalidResponseError(
                        "iNaturalist",
                        "duplicate bird IDs during pagination",
                    )
                seen.add(bird.id)
                birds.append(bird)
            page_number += 1
        if expected_total is None or len(birds) != expected_total:
            raise UpstreamInvalidResponseError(
                "iNaturalist",
                "name pagination was incomplete",
            )
        ordered = tuple(
            sorted(
                birds,
                key=lambda item: (
                    display_name(item).casefold(),
                    item.scientific_name.casefold(),
                ),
            )
        )
        await self.cache.set(cache_key, ordered, 1800)
        return ordered

    async def _continent_matches(
        self, query: BirdSearchQuery, taxon_id: int
    ) -> tuple[list[BirdSummary], bool]:
        needed = query.offset + query.page_size + 1
        matches: list[BirdSummary] = []
        scanned = 0
        page_number = 1
        total: int | None = None
        seen: set[int] = set()
        order_by = (
            "id" if query.sort.value == "name" else "observations_count"
        )

        while (
            len(matches) < needed
            and scanned < self.max_continent_source_scan
        ):
            source = await self.catalog.search_page(
                query=query.query,
                page=page_number,
                page_size=min(
                    100,
                    self.max_continent_source_scan - scanned,
                ),
                taxon_id=taxon_id,
                order_by=order_by,
            )
            total = source.total if total is None else total
            if source.total != total:
                raise UpstreamInvalidResponseError(
                    "iNaturalist",
                    "total changed during continent pagination",
                )
            if not source.items:
                break
            for bird in source.items:
                if bird.id in seen:
                    raise UpstreamInvalidResponseError(
                        "iNaturalist",
                        "duplicate bird IDs during pagination",
                    )
                seen.add(bird.id)
            scanned += len(source.items)
            enriched = await self._enrich(source.items)
            matches.extend(
                await self._filter_continent(
                    enriched,
                    query.continent.value,
                )
            )
            page_number += 1

        if query.sort.value == "name":
            matches.sort(
                key=lambda item: (
                    (
                        item.common_name
                        or item.english_name
                        or item.scientific_name
                    ).casefold(),
                    item.scientific_name.casefold(),
                )
            )
        exhausted = total is not None and scanned >= total
        return matches, exhausted

    async def _enrich(self, birds: tuple[Bird, ...]) -> list[BirdSummary]:
        semaphore = asyncio.Semaphore(self.enrichment_parallelism)

        async def enrich_one(bird: Bird) -> BirdSummary:
            async with semaphore:
                taxonomy = await self.taxonomy.match_species(
                    bird.scientific_name,
                    required=False,
                )
                return to_summary(bird, taxonomy)

        return list(
            await asyncio.gather(*(enrich_one(bird) for bird in birds))
        )

    async def _filter_continent(
        self,
        birds: list[BirdSummary],
        continent: str,
    ) -> list[BirdSummary]:
        semaphore = asyncio.Semaphore(self.occurrence_parallelism)

        async def check(bird: BirdSummary) -> bool:
            if bird.gbif_key is None:
                return False
            async with semaphore:
                return await self.taxonomy.has_occurrence(
                    bird.gbif_key,
                    continent,
                )

        flags = await asyncio.gather(*(check(bird) for bird in birds))
        return [
            bird
            for bird, found in zip(birds, flags, strict=True)
            if found
        ]
