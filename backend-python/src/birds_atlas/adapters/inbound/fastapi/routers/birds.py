from fastapi import APIRouter, Depends, Query, Response

from birds_atlas.adapters.inbound.fastapi.dependencies import (
    get_bird_detail,
    get_occurrences,
    get_search_birds,
)
from birds_atlas.adapters.inbound.fastapi.schemas import (
    BirdDetailDto,
    BirdListResponseDto,
    BirdSearchRequest,
    OccurrencePointDto,
)
from birds_atlas.application.use_cases.get_bird_detail import GetBirdDetail
from birds_atlas.application.use_cases.get_occurrences import GetBirdOccurrences
from birds_atlas.application.use_cases.search_birds import SearchBirds

router = APIRouter(prefix="/birds", tags=["birds"])
PUBLIC_CACHE = "public, max-age=60, s-maxage=300, stale-while-revalidate=600"


@router.get("", response_model=BirdListResponseDto)
async def search_birds(
    response: Response,
    request: BirdSearchRequest = Depends(),
    use_case: SearchBirds = Depends(get_search_birds),
) -> BirdListResponseDto:
    response.headers["Cache-Control"] = PUBLIC_CACHE
    result = await use_case.execute(request.to_domain())
    return BirdListResponseDto.from_domain(result)


@router.get("/{bird_id}", response_model=BirdDetailDto)
async def get_bird(
    bird_id: int,
    response: Response,
    use_case: GetBirdDetail = Depends(get_bird_detail),
) -> BirdDetailDto:
    response.headers["Cache-Control"] = PUBLIC_CACHE
    return BirdDetailDto.from_domain(await use_case.execute(bird_id))


@router.get("/{bird_id}/occurrences", response_model=list[OccurrencePointDto])
async def get_bird_occurrences(
    bird_id: int,
    response: Response,
    limit: int = Query(default=300, ge=1, le=500),
    use_case: GetBirdOccurrences = Depends(get_occurrences),
) -> list[OccurrencePointDto]:
    response.headers["Cache-Control"] = "public, max-age=30, s-maxage=120"
    points = await use_case.execute(bird_id, limit)
    return [OccurrencePointDto.from_domain(point) for point in points]
