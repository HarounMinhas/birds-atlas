from dataclasses import dataclass

from birds_atlas.application.ports.bird_catalog import BirdCatalogPort
from birds_atlas.application.ports.occurrences import OccurrenceProviderPort
from birds_atlas.application.ports.taxonomy import TaxonomyProviderPort
from birds_atlas.domain.exceptions import BirdNotFoundError, DomainValidationError
from birds_atlas.domain.models import MAX_OCCURRENCE_LIMIT, OccurrencePoint


@dataclass(slots=True)
class GetBirdOccurrences:
    catalog: BirdCatalogPort
    taxonomy: TaxonomyProviderPort
    occurrences: OccurrenceProviderPort

    async def execute(self, bird_id: int, limit: int) -> tuple[OccurrencePoint, ...]:
        if not 1 <= limit <= MAX_OCCURRENCE_LIMIT:
            raise DomainValidationError(
                f"limit must be between 1 and {MAX_OCCURRENCE_LIMIT}"
            )
        bird = await self.catalog.get_bird(bird_id)
        if bird is None or not bird.is_active_bird_species():
            raise BirdNotFoundError("Bird taxon not found.")
        gbif = await self.taxonomy.match_species(bird.scientific_name, required=True)
        if gbif.usage_key is None:
            return ()
        return await self.occurrences.occurrences(gbif.usage_key, limit)
