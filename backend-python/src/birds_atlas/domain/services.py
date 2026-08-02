from __future__ import annotations

from .models import Bird, BirdSummary, GbifTaxonomy


def display_name(bird: Bird) -> str:
    return bird.common_name or bird.english_name or bird.scientific_name


def to_summary(bird: Bird, taxonomy: GbifTaxonomy) -> BirdSummary:
    return BirdSummary(
        id=bird.id,
        common_name=bird.common_name,
        english_name=bird.english_name,
        scientific_name=bird.scientific_name,
        family=taxonomy.family,
        order=taxonomy.order,
        genus=taxonomy.genus,
        image_url=bird.image_url,
        photo_attribution=bird.photo_attribution,
        photo_license=bird.photo_license,
        iucn_status=bird.iucn_status,
        observation_count=bird.observation_count,
        gbif_key=taxonomy.usage_key,
    )


def taxonomy_rank_weight(rank: str) -> int:
    return {"order": 1, "family": 2, "genus": 3}.get(rank.casefold(), 0)
