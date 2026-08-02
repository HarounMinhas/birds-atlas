from __future__ import annotations

import json
import logging
from typing import Any

import httpx

from birds_atlas.domain.exceptions import (
    UpstreamInvalidResponseError,
    UpstreamRateLimitError,
    UpstreamTimeoutError,
    UpstreamUnavailableError,
)

logger = logging.getLogger(__name__)


class AsyncHttpClient:
    def __init__(
        self,
        *,
        provider: str,
        base_url: str,
        connect_timeout: float,
        read_timeout: float,
        follow_redirects: bool = True,
    ) -> None:
        timeout = httpx.Timeout(
            timeout=max(connect_timeout + read_timeout, read_timeout),
            connect=connect_timeout,
            read=read_timeout,
            write=read_timeout,
            pool=connect_timeout,
        )
        self.provider = provider
        self.client = httpx.AsyncClient(
            base_url=base_url,
            timeout=timeout,
            follow_redirects=follow_redirects,
            headers={
                "User-Agent": (
                    "BirdsAtlas/2.0 (+https://github.com/HarounMinhas/birds-atlas)"
                )
            },
            limits=httpx.Limits(max_connections=24, max_keepalive_connections=12),
        )

    async def get_json(
        self,
        path: str,
        *,
        params: dict[str, str | int | bool] | None = None,
        allow_not_found: bool = False,
    ) -> Any:
        try:
            response = await self.client.get(path, params=params)
        except httpx.TimeoutException as exc:
            logger.warning("upstream_timeout provider=%s path=%s", self.provider, path)
            raise UpstreamTimeoutError(
                self.provider, f"{self.provider} timed out"
            ) from exc
        except httpx.RequestError as exc:
            logger.warning(
                "upstream_request_error provider=%s path=%s", self.provider, path
            )
            raise UpstreamUnavailableError(
                self.provider, f"{self.provider} request failed"
            ) from exc
        if allow_not_found and response.status_code == 404:
            return None
        if response.status_code == 429:
            logger.warning("upstream_rate_limited provider=%s path=%s", self.provider, path)
            raise UpstreamRateLimitError(
                self.provider,
                f"{self.provider} rate limit exceeded",
                status_code=429,
            )
        if response.status_code >= 500:
            logger.warning(
                "upstream_server_error provider=%s path=%s status=%s",
                self.provider,
                path,
                response.status_code,
            )
            raise UpstreamUnavailableError(
                self.provider,
                f"{self.provider} returned {response.status_code}",
                status_code=response.status_code,
            )
        try:
            response.raise_for_status()
        except httpx.HTTPStatusError as exc:
            raise UpstreamUnavailableError(
                self.provider,
                f"{self.provider} returned {response.status_code}",
                status_code=response.status_code,
            ) from exc
        try:
            return response.json()
        except (json.JSONDecodeError, ValueError) as exc:
            raise UpstreamInvalidResponseError(
                self.provider, f"{self.provider} returned invalid JSON"
            ) from exc

    async def aclose(self) -> None:
        await self.client.aclose()
