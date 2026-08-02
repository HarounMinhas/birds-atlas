from __future__ import annotations

import logging
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from birds_atlas.adapters.inbound.fastapi.error_handlers import (
    register_error_handlers,
)
from birds_atlas.adapters.inbound.fastapi.routers import birds, health, taxonomy
from birds_atlas.bootstrap import Container, build_container
from birds_atlas.config.settings import Settings


def create_app(container: Container | None = None) -> FastAPI:
    settings = Settings.from_environment()
    logging.basicConfig(level=getattr(logging, settings.log_level, logging.INFO))
    resolved_container = container or build_container(settings)

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        app.state.container = resolved_container
        yield
        if container is None:
            await resolved_container.close()

    app = FastAPI(
        title="Birds Atlas API",
        version="2.0.0",
        docs_url="/api/docs",
        openapi_url="/api/openapi.json",
        lifespan=lifespan,
    )
    app.state.container = resolved_container
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["http://localhost:4200"],
        allow_methods=["GET"],
        allow_headers=["*"],
    )
    app.include_router(health.router, prefix="/api")
    app.include_router(birds.router, prefix="/api")
    app.include_router(taxonomy.router, prefix="/api")
    register_error_handlers(app)
    return app


app = create_app()


def run() -> None:
    uvicorn.run(
        "birds_atlas.adapters.inbound.fastapi.app:app",
        host="127.0.0.1",
        port=5078,
        reload=True,
    )
