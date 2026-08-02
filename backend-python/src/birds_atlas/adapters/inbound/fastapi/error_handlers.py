from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from birds_atlas.domain.exceptions import (
    BirdNotFoundError,
    DomainValidationError,
    UpstreamError,
    UpstreamRateLimitError,
    UpstreamTimeoutError,
)


def register_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(BirdNotFoundError)
    async def not_found(_: Request, exc: BirdNotFoundError) -> JSONResponse:
        return JSONResponse(status_code=404, content={"message": str(exc)})

    @app.exception_handler(DomainValidationError)
    async def validation(_: Request, exc: DomainValidationError) -> JSONResponse:
        return JSONResponse(status_code=400, content={"message": str(exc)})

    @app.exception_handler(UpstreamRateLimitError)
    async def rate_limit(_: Request, exc: UpstreamRateLimitError) -> JSONResponse:
        return JSONResponse(
            status_code=503,
            content={"message": f"External data source {exc.provider} is rate-limited."},
            headers={"Retry-After": "60"},
        )

    @app.exception_handler(UpstreamTimeoutError)
    async def timeout(_: Request, exc: UpstreamTimeoutError) -> JSONResponse:
        return JSONResponse(
            status_code=504,
            content={"message": f"External data source {exc.provider} timed out."},
        )

    @app.exception_handler(UpstreamError)
    async def upstream(_: Request, exc: UpstreamError) -> JSONResponse:
        return JSONResponse(
            status_code=502,
            content={"message": f"External data source {exc.provider} failed."},
        )
