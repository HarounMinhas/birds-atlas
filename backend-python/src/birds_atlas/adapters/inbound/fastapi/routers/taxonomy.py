from fastapi import APIRouter, Depends, Response

from birds_atlas.adapters.inbound.fastapi.dependencies import get_taxonomy_options
from birds_atlas.adapters.inbound.fastapi.schemas import TaxonomyOptionsDto
from birds_atlas.application.use_cases.get_taxonomy_options import GetTaxonomyOptions

router = APIRouter(prefix="/taxonomy", tags=["taxonomy"])


@router.get("/options", response_model=TaxonomyOptionsDto)
async def taxonomy_options(
    response: Response,
    use_case: GetTaxonomyOptions = Depends(get_taxonomy_options),
) -> TaxonomyOptionsDto:
    response.headers["Cache-Control"] = "public, max-age=300, s-maxage=3600"
    return TaxonomyOptionsDto.from_domain(await use_case.execute())
