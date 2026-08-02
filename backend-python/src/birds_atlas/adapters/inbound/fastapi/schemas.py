from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field

from birds_atlas.domain.models import (
    BirdDetail,
    BirdRecording,
    BirdSearchQuery,
    BirdSearchResult,
    BirdSummary,
    BirdTaxonomy,
    OccurrencePoint,
    TaxonomyOptions,
)


def to_camel(value: str) -> str:
    head, *tail = value.split("_")
    return head + "".join(part.capitalize() for part in tail)


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class BirdSearchRequest(BaseModel):
    q: str | None = None
    page: int = 1
    page_size: int = Field(default=24, alias="pageSize")
    continent: str | None = None
    order: str | None = None
    family: str | None = None
    genus: str | None = None
    sort: str | None = None

    model_config = ConfigDict(populate_by_name=True)

    def to_domain(self) -> BirdSearchQuery:
        return BirdSearchQuery.normalized(
            query=self.q,
            page=max(1, min(self.page, 10_000)),
            page_size=max(1, min(self.page_size, 48)),
            continent=self.continent,
            order=self.order,
            family=self.family,
            genus=self.genus,
            sort=self.sort,
        )


class BirdSummaryDto(ApiModel):
    id: int
    common_name: str
    english_name: str
    scientific_name: str
    family: str
    order: str
    genus: str
    image_url: str | None
    photo_attribution: str | None
    photo_license: str | None
    iucn_status: str
    observation_count: int
    gbif_key: int | None

    @classmethod
    def from_domain(cls, value: BirdSummary) -> BirdSummaryDto:
        return cls(**{field: getattr(value, field) for field in cls.model_fields})


class BirdListResponseDto(ApiModel):
    page: int
    page_size: int
    total: int
    is_estimate: bool
    items: list[BirdSummaryDto]

    @classmethod
    def from_domain(cls, value: BirdSearchResult) -> BirdListResponseDto:
        return cls(
            page=value.page,
            page_size=value.page_size,
            total=value.total,
            is_estimate=value.is_estimate,
            items=[BirdSummaryDto.from_domain(item) for item in value.items],
        )


class BirdTaxonomyDto(ApiModel):
    kingdom: str
    phylum: str
    class_name: str
    order: str
    family: str
    genus: str
    species: str
    gbif_key: int | None

    @classmethod
    def from_domain(cls, value: BirdTaxonomy) -> BirdTaxonomyDto:
        return cls(**{field: getattr(value, field) for field in cls.model_fields})


class AudioRecordingDto(ApiModel):
    id: str
    common_name: str
    scientific_name: str
    recordist: str
    type: str
    country: str
    length: str
    file_url: str
    license_url: str
    source_url: str

    @classmethod
    def from_domain(cls, value: BirdRecording) -> AudioRecordingDto:
        return cls(**{field: getattr(value, field) for field in cls.model_fields})


class BirdDetailDto(BirdSummaryDto):
    wikipedia_summary: str | None
    wikipedia_url: str | None
    inaturalist_url: str
    gbif_url: str | None
    continents: list[str]
    taxonomy: BirdTaxonomyDto
    recordings: list[AudioRecordingDto]

    @classmethod
    def from_detail(cls, value: BirdDetail) -> BirdDetailDto:
        base = BirdSummaryDto.from_domain(value).model_dump()
        return cls(
            **base,
            wikipedia_summary=value.wikipedia_summary,
            wikipedia_url=value.wikipedia_url,
            inaturalist_url=value.inaturalist_url,
            gbif_url=value.gbif_url,
            continents=list(value.continents),
            taxonomy=BirdTaxonomyDto.from_domain(value.taxonomy),
            recordings=[
                AudioRecordingDto.from_domain(item) for item in value.recordings
            ],
        )


class OccurrencePointDto(ApiModel):
    key: int
    latitude: float
    longitude: float
    country: str
    locality: str
    event_date: datetime | None
    source_url: str

    @classmethod
    def from_domain(cls, value: OccurrencePoint) -> OccurrencePointDto:
        return cls(**{field: getattr(value, field) for field in cls.model_fields})


class TaxonomyOptionsDto(ApiModel):
    orders: list[str]
    families: list[str]
    genera: list[str]

    @classmethod
    def from_domain(cls, value: TaxonomyOptions) -> TaxonomyOptionsDto:
        return cls(
            orders=list(value.orders),
            families=list(value.families),
            genera=list(value.genera),
        )


class HealthDto(ApiModel):
    status: str
    utc: datetime


class ErrorDto(BaseModel):
    message: str
