from __future__ import annotations

from typing import Any

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.adapters.outbound.http.parsing import (
    int_value,
    list_value,
    object_value,
    string_value,
)
from birds_atlas.application.ports.cache import CachePort
from birds_atlas.domain.exceptions import UpstreamInvalidResponseError
from birds_atlas.domain.models import AVES_TAXON_ID, Bird, ResolvedTaxon, TaxonPage


class INaturalistHttpAdapter:
    provider = "iNaturalist"

    def __init__(self, client: AsyncHttpClient, cache: CachePort) -> None:
        self._client = client
        self._cache = cache

    async def search_page(
        self,
        *,
        query: str,
        page: int,
        page_size: int,
        taxon_id: int,
        order_by: str,
    ) -> TaxonPage:
        payload = await self._client.get_json(
            "v1/taxa",
            params={
                "taxon_id": taxon_id,
                "rank": "species",
                "is_active": "true",
                "all_names": "true",
                "locale": "nl",
                "per_page": page_size,
                "page": page,
                "order_by": order_by,
                "order": "asc" if order_by == "id" else "desc",
                **({"q": query} if query else {}),
            },
        )
        root = object_value(payload, self.provider, "taxa response")
        total = int_value(root, "total_results", self.provider, required=True)
        assert total is not None
        if total < 0:
            raise UpstreamInvalidResponseError(self.provider, "total_results was negative")
        records = list_value(root.get("results"), self.provider, "results")
        birds = tuple(self._parse_bird(item) for item in records)
        if len({bird.id for bird in birds}) != len(birds):
            raise UpstreamInvalidResponseError(
                self.provider, "species page contained duplicate IDs"
            )
        return TaxonPage(total=total, items=birds)

    async def get_bird(self, bird_id: int) -> Bird | None:
        if bird_id <= 0:
            return None
        key = f"inat:{bird_id}"
        cached = await self._cache.get(key)
        if isinstance(cached, Bird):
            return cached
        payload = await self._client.get_json(
            f"v1/taxa/{bird_id}",
            params={"locale": "nl", "all_names": "true"},
            allow_not_found=True,
        )
        if payload is None:
            return None
        root = object_value(payload, self.provider, "taxon response")
        records = list_value(root.get("results"), self.provider, "results")
        if not records:
            return None
        if len(records) != 1:
            raise UpstreamInvalidResponseError(self.provider, "taxon response was not singular")
        bird = self._parse_bird(records[0], include_identity=True)
        if bird.id != bird_id:
            raise UpstreamInvalidResponseError(self.provider, "taxon ID did not match request")
        await self._cache.set(key, bird, 43_200)
        return bird

    async def resolve_taxon(self, *, rank: str, name: str) -> ResolvedTaxon | None:
        key = f"bird-filter:{rank}:{name.casefold()}"
        cached = await self._cache.get(key)
        if isinstance(cached, ResolvedTaxon):
            return cached
        page = 1
        seen: set[int] = set()
        expected_total: int | None = None
        while expected_total is None or len(seen) < expected_total:
            payload = await self._client.get_json(
                "v1/taxa",
                params={
                    "taxon_id": AVES_TAXON_ID,
                    "rank": rank,
                    "is_active": "true",
                    "per_page": 100,
                    "page": page,
                    "order_by": "id",
                    "order": "asc",
                    "locale": "en",
                    "q": name,
                },
            )
            root = object_value(payload, self.provider, "filter response")
            total = int_value(root, "total_results", self.provider, required=True)
            assert total is not None
            expected_total = total if expected_total is None else expected_total
            if total != expected_total:
                raise UpstreamInvalidResponseError(self.provider, "filter total changed")
            records = list_value(root.get("results"), self.provider, "results")
            if not records and len(seen) < expected_total:
                raise UpstreamInvalidResponseError(self.provider, "filter pagination stopped early")
            for raw in records:
                record = object_value(raw, self.provider, "filter record")
                taxon_id = int_value(record, "id", self.provider, required=True)
                assert taxon_id is not None
                if taxon_id in seen:
                    raise UpstreamInvalidResponseError(self.provider, "duplicate filter taxon")
                seen.add(taxon_id)
                result_name = string_value(record, "name", self.provider, required=True)
                result_rank = string_value(record, "rank", self.provider, required=True)
                if result_name.casefold() == name.casefold() and result_rank.casefold() == rank:
                    resolved = ResolvedTaxon(taxon_id, result_rank, self._ancestor_ids(record))
                    await self._cache.set(key, resolved, 43_200)
                    return resolved
            page += 1
        return None

    async def taxonomy_names(self, *, rank: str, page_size: int) -> tuple[str, ...]:
        names: set[str] = set()
        seen: set[int] = set()
        expected_total: int | None = None
        page = 1
        while expected_total is None or len(seen) < expected_total:
            payload = await self._client.get_json(
                "v1/taxa",
                params={
                    "taxon_id": AVES_TAXON_ID,
                    "rank": rank,
                    "is_active": "true",
                    "per_page": page_size,
                    "page": page,
                    "order_by": "id",
                    "order": "asc",
                    "locale": "en",
                },
            )
            root = object_value(payload, self.provider, "taxonomy response")
            total = int_value(root, "total_results", self.provider, required=True)
            assert total is not None
            expected_total = total if expected_total is None else expected_total
            if total != expected_total:
                raise UpstreamInvalidResponseError(self.provider, "taxonomy total changed")
            records = list_value(root.get("results"), self.provider, "results")
            if not records and len(seen) < expected_total:
                raise UpstreamInvalidResponseError(
                    self.provider, "taxonomy pagination stopped early"
                )
            for raw in records:
                record = object_value(raw, self.provider, "taxonomy record")
                taxon_id = int_value(record, "id", self.provider, required=True)
                assert taxon_id is not None
                if taxon_id in seen:
                    raise UpstreamInvalidResponseError(self.provider, "duplicate taxonomy taxon")
                seen.add(taxon_id)
                result_rank = string_value(record, "rank", self.provider, required=True)
                if result_rank.casefold() != rank:
                    raise UpstreamInvalidResponseError(self.provider, "taxonomy rank mismatch")
                names.add(string_value(record, "name", self.provider, required=True))
            page += 1
        if expected_total is None or len(seen) != expected_total:
            raise UpstreamInvalidResponseError(self.provider, "taxonomy pagination incomplete")
        return tuple(sorted(names, key=str.casefold))

    def _parse_bird(self, value: Any, *, include_identity: bool = False) -> Bird:
        record = object_value(value, self.provider, "taxon record")
        bird_id = int_value(record, "id", self.provider, required=True)
        observations = int_value(record, "observations_count", self.provider, required=True)
        assert bird_id is not None and observations is not None
        if observations < 0:
            raise UpstreamInvalidResponseError(self.provider, "negative observations_count")
        scientific = string_value(record, "name", self.provider, required=True)
        preferred = string_value(record, "preferred_common_name", self.provider)
        dutch = self._localized_name(record, "nl", "Dutch")
        english = self._localized_name(record, "en", "English")
        image = attribution = license_code = None
        photo = record.get("default_photo")
        if photo is not None:
            photo_obj = object_value(photo, self.provider, "default_photo")
            image = string_value(photo_obj, "medium_url", self.provider) or None
            attribution = string_value(photo_obj, "attribution", self.provider) or None
            license_code = string_value(photo_obj, "license_code", self.provider) or None
        status = "NE"
        conservation = record.get("conservation_status")
        if conservation is not None:
            status = (
                string_value(
                    object_value(conservation, self.provider, "conservation_status"),
                    "status",
                    self.provider,
                ).upper()
                or "NE"
            )
        rank = string_value(record, "rank", self.provider) or "species"
        is_active_raw = record.get("is_active", True)
        if not isinstance(is_active_raw, bool):
            raise UpstreamInvalidResponseError(self.provider, "is_active was not boolean")
        ancestors = self._ancestor_ids(record) if include_identity else frozenset({AVES_TAXON_ID})
        return Bird(
            id=bird_id,
            scientific_name=scientific,
            common_name=dutch or preferred,
            english_name=english,
            image_url=image,
            photo_attribution=attribution,
            photo_license=license_code,
            iucn_status=status,
            observation_count=observations,
            wikipedia_url=(string_value(record, "wikipedia_url", self.provider) or None),
            rank=rank,
            is_active=is_active_raw,
            ancestor_ids=ancestors,
        )

    def _localized_name(self, record: dict[str, Any], locale: str, lexicon: str) -> str:
        raw_names = record.get("names")
        if raw_names is None:
            return ""
        for raw in list_value(raw_names, self.provider, "names"):
            name = object_value(raw, self.provider, "name")
            if (
                string_value(name, "locale", self.provider).casefold() == locale.casefold()
                or string_value(name, "lexicon", self.provider).casefold() == lexicon.casefold()
            ):
                return string_value(name, "name", self.provider, required=True)
        return ""

    def _ancestor_ids(self, record: dict[str, Any]) -> frozenset[int]:
        raw = record.get("ancestor_ids")
        if raw is None:
            return frozenset()
        result: set[int] = set()
        for value in list_value(raw, self.provider, "ancestor_ids"):
            if not isinstance(value, int) or isinstance(value, bool):
                raise UpstreamInvalidResponseError(self.provider, "ancestor_ids was invalid")
            result.add(value)
        return frozenset(result)
