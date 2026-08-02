from fastapi import APIRouter, Depends, Response

from birds_atlas.adapters.inbound.fastapi.dependencies import get_check_health
from birds_atlas.adapters.inbound.fastapi.schemas import HealthDto
from birds_atlas.application.use_cases.check_health import CheckHealth

router = APIRouter(tags=["health"])


@router.get("/health", response_model=HealthDto)
async def health(
    response: Response,
    use_case: CheckHealth = Depends(get_check_health),
) -> HealthDto:
    response.headers["Cache-Control"] = "no-store"
    status, utc = use_case.execute()
    return HealthDto(status=status, utc=utc)
