from __future__ import annotations

import os
from dataclasses import dataclass
from urllib.parse import urlparse


def _positive_float(name: str, default: float) -> float:
    raw = os.getenv(name)
    if raw is None:
        return default
    value = float(raw)
    if value <= 0:
        raise ValueError(f"{name} must be positive")
    return value


def _positive_int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None:
        return default
    value = int(raw)
    if value <= 0:
        raise ValueError(f"{name} must be positive")
    return value


def _base_url(name: str, default: str) -> str:
    value = os.getenv(name, default).strip()
    parsed = urlparse(value)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError(f"{name} must be an absolute HTTP(S) URL")
    return value.rstrip("/") + "/"


@dataclass(frozen=True, slots=True)
class Settings:
    inaturalist_base_url: str
    gbif_base_url: str
    wikipedia_base_url: str
    xeno_canto_base_url: str
    xeno_canto_api_key: str | None
    http_connect_timeout_seconds: float
    http_read_timeout_seconds: float
    cache_default_ttl_seconds: int
    log_level: str

    @classmethod
    def from_environment(cls) -> Settings:
        key = os.getenv("XENO_CANTO_API_KEY", "").strip()
        return cls(
            inaturalist_base_url=_base_url(
                "INATURALIST_BASE_URL", "https://api.inaturalist.org/"
            ),
            gbif_base_url=_base_url("GBIF_BASE_URL", "https://api.gbif.org/"),
            wikipedia_base_url=_base_url(
                "WIKIPEDIA_BASE_URL", "https://en.wikipedia.org/"
            ),
            xeno_canto_base_url=_base_url(
                "XENO_CANTO_BASE_URL", "https://xeno-canto.org/"
            ),
            xeno_canto_api_key=key or None,
            http_connect_timeout_seconds=_positive_float(
                "HTTP_CONNECT_TIMEOUT_SECONDS", 5.0
            ),
            http_read_timeout_seconds=_positive_float(
                "HTTP_READ_TIMEOUT_SECONDS", 20.0
            ),
            cache_default_ttl_seconds=_positive_int(
                "CACHE_DEFAULT_TTL_SECONDS", 1800
            ),
            log_level=os.getenv("LOG_LEVEL", "INFO").upper(),
        )
