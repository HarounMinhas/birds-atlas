from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime

from .enums import Continent, SortOption
from .exceptions import DomainValidationError

AVES_TAXON_ID = 3
MAX_PAGE = 10_000
MAX_PAGE_SIZE = 48
MAX_OCCURRENCE_LIMIT = 500
MAX_CONTINENT_RESULT_OFFSET = 240


@dataclass(frozen=True, slots=True)
class Bird:
    id: int
    scientific_name: str
    common_name: str
    english_name: str
    image_url: str | None
    photo_attribution: str | None
    photo_license: str | None
    iucn_status: str
    observation_count: int
    wikipedia_url: str | None = None
    rank: str = "species"
    is_active: bool = True
    ancestor_ids: frozenset[int] = field(default_factory=frozenset)

    def is_active_bird_species(self) -> bool:
        return (
            self.id > 0
            and self.is_active
            and self.rank.casefold() == "species"
            and AVES_TAXON_ID in self.ancestor_ids
        )


@dataclass(frozen=True, slots=True)
class GbifTaxonomy:
    usage_key: int | None
    kingdom: str = ""
    phylum: str = ""
    class_name: str = "Aves"
    order: str = ""
    family: str = ""
    genus: str = ""
    species: str = ""


@dataclass(frozen=True, slots=True)
class BirdSummary:
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


@dataclass(frozen=True, slots=True)
class BirdTaxonomy:
    kingdom: str
    phylum: str
    class_name: str
    order: str
    family: str
    genus: str
    species: str
    gbif_key: int | None


@dataclass(frozen=True, slots=True)
class BirdRecording:
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


@dataclass(frozen=True, slots=True)
class EncyclopediaEntry:
    summary: str | None
    url: str | None


@dataclass(frozen=True, slots=True)
class BirdDetail(BirdSummary):
    wikipedia_summary: str | None
    wikipedia_url: str | None
    inaturalist_url: str
    gbif_url: str | None
    continents: tuple[str, ...]
    taxonomy: BirdTaxonomy
    recordings: tuple[BirdRecording, ...]


@dataclass(frozen=True, slots=True)
class OccurrencePoint:
    key: int
    latitude: float
    longitude: float
    country: str
    locality: str
    event_date: datetime | None
    source_url: str


@dataclass(frozen=True, slots=True)
class TaxonomyOptions:
    orders: tuple[str, ...]
    families: tuple[str, ...]
    genera: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class BirdSearchQuery:
    query: str = ""
    page: int = 1
    page_size: int = 24
    continent: Continent | None = None
    order: str = ""
    family: str = ""
    genus: str = ""
    sort: SortOption = SortOption.OBSERVATIONS

    def __post_init__(self) -> None:
        if not 1 <= self.page <= MAX_PAGE:
            raise DomainValidationError(f"page must be between 1 and {MAX_PAGE}")
        if not 1 <= self.page_size <= MAX_PAGE_SIZE:
            raise DomainValidationError(f"pageSize must be between 1 and {MAX_PAGE_SIZE}")
        if self.continent is not None and self.offset > MAX_CONTINENT_RESULT_OFFSET:
            raise DomainValidationError(
                "Continent-filtered pagination is limited to an offset of "
                f"{MAX_CONTINENT_RESULT_OFFSET} results."
            )

    @property
    def offset(self) -> int:
        return (self.page - 1) * self.page_size

    @classmethod
    def normalized(
        cls,
        *,
        query: str | None,
        page: int,
        page_size: int,
        continent: str | None,
        order: str | None,
        family: str | None,
        genus: str | None,
        sort: str | None,
    ) -> BirdSearchQuery:
        return cls(
            query=(query or "").strip(),
            page=page,
            page_size=page_size,
            continent=Continent.parse_optional(continent),
            order=(order or "").strip(),
            family=(family or "").strip(),
            genus=(genus or "").strip(),
            sort=SortOption.parse(sort),
        )


@dataclass(frozen=True, slots=True)
class BirdSearchResult:
    page: int
    page_size: int
    total: int
    is_estimate: bool
    has_next_page: bool
    items: tuple[BirdSummary, ...]


@dataclass(frozen=True, slots=True)
class TaxonPage:
    total: int
    items: tuple[Bird, ...]


@dataclass(frozen=True, slots=True)
class ResolvedTaxon:
    id: int
    rank: str
    ancestor_ids: frozenset[int]
