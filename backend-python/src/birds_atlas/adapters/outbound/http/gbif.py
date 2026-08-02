from __future__ import annotations

from datetime import UTC, datetime
from typing import Any

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.adapters.outbound.http.parsing import (
    float_value,
    int_value,
    list_value,
    object_value,
    string_value,
)
from birds_atlas.application.ports.cache import CachePort
from birds_atlas.domain.exceptions import UpstreamError, UpstreamInvalidResponseError
from birds_atlas.domain.models import GbifTaxonomy, OccurrencePoint


class GbifHttpAdapter:
    provider = "GBIF"

    def __init__(self, client: AsyncHttpClient, cache: CachePort) -> None:
        self._client = client
        self._cache = cache

    async def match_species(self, scientific_name: str, *, required: bool) -> GbifTaxonomy:
        key = f"gbif-match:{scientific_name.casefold()}"
        cached = await self._cache.get(key)
        if isinstance(cached, GbifTaxonomy):
            return cached
        try:
            payload = await self._client.get_json(
                "v1/species/match",
                params={"name": scientific_name, "strict": "false"},
            )
            result = self._parse_match(payload, scientific_name)
            await self._cache.set(key, result, 259_200)
            return result
        except UpstreamError:
            if required:
                raise
            return self._empty(scientific_name)

    async def continents(self, gbif_key: int) -> tuple[str, ...]:
        key = f"gbif-continents:{gbif_key}"
        cached = await self._cache.get(key)
        if isinstance(cached, tuple) and all(isinstance(item, str) for item in cached):
            return cached
        try:
            payload = await self._client.get_json(
                "v1/occurrence/search",
                params={
                    "taxon_key": gbif_key,
                    "limit": 0,
                    "facet": "continent",
                    "facet_limit": 20,
                },
            )
            root = object_value(payload, self.provider, "continent response")
            count = int_value(root, "count", self.provider, required=True)
            assert count is not None
            if count == 0:
                return ()
            facets = list_value(root.get("facets"), self.provider, "facets")
            values: set[str] = set()
            found = False
            for raw_facet in facets:
                facet = object_value(raw_facet, self.provider, "facet")
                if string_value(facet, "field", self.provider).upper() != "CONTINENT":
                    continue
                found = True
                counts = list_value(facet.get("counts"), self.provider, "counts")
                for raw_count in counts:
                    facet_count = object_value(raw_count, self.provider, "facet count")
                    name = string_value(facet_count, "name", self.provider, required=True)
                    value = int_value(facet_count, "count", self.provider, required=True)
                    if value is not None and value > 0:
                        values.add(name)
            if not found:
                raise UpstreamInvalidResponseError(self.provider, "continent facet missing")
            result = tuple(sorted(values))
            await self._cache.set(key, result, 64_800)
            return result
        except UpstreamError:
            return ()

    async def has_occurrence(self, gbif_key: int, continent: str) -> bool:
        key = f"bird-continent:{gbif_key}:{continent}"
        cached = await self._cache.get(key)
        if isinstance(cached, bool):
            return cached
        try:
            payload = await self._client.get_json(
                "v1/occurrence/search",
                params={
                    "taxon_key": gbif_key,
                    "continent": continent,
                    "limit": 0,
                },
            )
            root = object_value(payload, self.provider, "occurrence count response")
            count = int_value(root, "count", self.provider, required=True)
            assert count is not None
            if count < 0:
                raise UpstreamInvalidResponseError(self.provider, "occurrence count was negative")
            found = count > 0
            await self._cache.set(key, found, 64_800)
            return found
        except UpstreamError:
            return False

    async def occurrences(self, gbif_key: int, limit: int) -> tuple[OccurrencePoint, ...]:
        payload = await self._client.get_json(
            "v1/occurrence/search",
            params={
                "taxon_key": gbif_key,
                "has_coordinate": "true",
                "limit": limit,
            },
        )
        root = object_value(payload, self.provider, "occurrence response")
        records = list_value(root.get("results"), self.provider, "results")
        points: list[OccurrencePoint] = []
        seen: set[int] = set()
        for raw in records:
            record = object_value(raw, self.provider, "occurrence")
            latitude = float_value(record, "decimalLatitude", self.provider)
            longitude = float_value(record, "decimalLongitude", self.provider)
            if latitude is None or longitude is None:
                continue
            if not -90 <= latitude <= 90 or not -180 <= longitude <= 180:
                raise UpstreamInvalidResponseError(self.provider, "invalid occurrence coordinates")
            occurrence_key = int_value(record, "key", self.provider) or 0
            if occurrence_key > 0 and occurrence_key in seen:
                raise UpstreamInvalidResponseError(self.provider, "duplicate occurrence key")
            if occurrence_key > 0:
                seen.add(occurrence_key)
            points.append(
                OccurrencePoint(
                    key=occurrence_key,
                    latitude=latitude,
                    longitude=longitude,
                    country=string_value(record, "country", self.provider),
                    locality=string_value(record, "locality", self.provider),
                    event_date=self._event_date(record.get("eventDate")),
                    source_url=(
                        f"https://www.gbif.org/occurrence/{occurrence_key}"
                        if occurrence_key > 0
                        else f"https://www.gbif.org/species/{gbif_key}"
                    ),
                )
            )
        return tuple(points)

    def _parse_match(self, payload: Any, scientific_name: str) -> GbifTaxonomy:
        root = object_value(payload, self.provider, "species-match response")
        match_type = string_value(root, "matchType", self.provider, required=True).upper()
        if match_type == "NONE":
            return self._empty(scientific_name)
        rank = string_value(root, "rank", self.provider, required=True).upper()
        if rank != "SPECIES" or match_type not in {"EXACT", "FUZZY"}:
            return self._empty(scientific_name)
        usage_key = int_value(root, "usageKey", self.provider, required=True)
        return GbifTaxonomy(
            usage_key=usage_key,
            kingdom=string_value(root, "kingdom", self.provider),
            phylum=string_value(root, "phylum", self.provider),
            class_name=string_value(root, "class", self.provider) or "Aves",
            order=string_value(root, "order", self.provider),
            family=string_value(root, "family", self.provider),
            genus=string_value(root, "genus", self.provider),
            species=(string_value(root, "species", self.provider) or scientific_name),
        )

    @staticmethod
    def _empty(scientific_name: str) -> GbifTaxonomy:
        parts = scientific_name.split()
        genus = parts[0] if parts else ""
        return GbifTaxonomy(
            None,
            class_name="Aves",
            genus=genus,
            species=scientific_name,
        )

    @staticmethod
    def _event_date(value: Any) -> datetime | None:
        if not isinstance(value, str):
            return None
        text = value.strip()
        if not text or "/" in text:
            return None
        try:
            if len(text) == 10:
                return datetime.fromisoformat(text).replace(tzinfo=UTC)
            if len(text) > 10 and text[10] == "T":
                return datetime.fromisoformat(text.replace("Z", "+00:00")).astimezone(UTC)
        except ValueError:
            return None
        return None
