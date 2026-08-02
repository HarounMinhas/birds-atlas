from __future__ import annotations

import asyncio
from dataclasses import dataclass

from birds_atlas.application.ports.bird_catalog import BirdCatalogPort
from birds_atlas.application.ports.encyclopedia import EncyclopediaProviderPort
from birds_atlas.application.ports.recordings import RecordingProviderPort
from birds_atlas.application.ports.taxonomy import TaxonomyProviderPort
from birds_atlas.domain.exceptions import BirdNotFoundError
from birds_atlas.domain.models import BirdDetail, BirdTaxonomy
from birds_atlas.domain.services import to_summary


@dataclass(slots=True)
class GetBirdDetail:
    catalog: BirdCatalogPort
    taxonomy: TaxonomyProviderPort
    encyclopedia: EncyclopediaProviderPort
    recordings: RecordingProviderPort

    async def execute(self, bird_id: int) -> BirdDetail:
        bird = await self.catalog.get_bird(bird_id)
        if bird is None or not bird.is_active_bird_species():
            raise BirdNotFoundError("Bird taxon not found.")

        gbif = await self.taxonomy.match_species(bird.scientific_name, required=False)
        encyclopedia_task = self.encyclopedia.summary(bird.wikipedia_url, bird.scientific_name)
        continent_task = (
            self.taxonomy.continents(gbif.usage_key) if gbif.usage_key else _empty_strings()
        )
        recordings_task = self.recordings.recordings(bird.scientific_name)
        entry, continents, recordings = await asyncio.gather(
            encyclopedia_task, continent_task, recordings_task
        )
        summary = to_summary(bird, gbif)
        return BirdDetail(
            id=summary.id,
            common_name=summary.common_name,
            english_name=summary.english_name,
            scientific_name=summary.scientific_name,
            family=summary.family,
            order=summary.order,
            genus=summary.genus,
            image_url=summary.image_url,
            photo_attribution=summary.photo_attribution,
            photo_license=summary.photo_license,
            iucn_status=summary.iucn_status,
            observation_count=summary.observation_count,
            gbif_key=summary.gbif_key,
            wikipedia_summary=entry.summary,
            wikipedia_url=entry.url,
            inaturalist_url=f"https://www.inaturalist.org/taxa/{bird.id}",
            gbif_url=(f"https://www.gbif.org/species/{gbif.usage_key}" if gbif.usage_key else None),
            continents=continents,
            taxonomy=BirdTaxonomy(
                kingdom=gbif.kingdom,
                phylum=gbif.phylum,
                class_name=gbif.class_name,
                order=gbif.order,
                family=gbif.family,
                genus=gbif.genus,
                species=gbif.species or bird.scientific_name,
                gbif_key=gbif.usage_key,
            ),
            recordings=recordings,
        )


async def _empty_strings() -> tuple[str, ...]:
    return ()
