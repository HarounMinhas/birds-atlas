from __future__ import annotations

from typing import Any

from birds_atlas.domain.exceptions import UpstreamInvalidResponseError


def object_value(value: Any, provider: str, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise UpstreamInvalidResponseError(provider, f"{context} was not an object")
    return value


def list_value(value: Any, provider: str, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise UpstreamInvalidResponseError(provider, f"{context} was not an array")
    return value


def string_value(
    obj: dict[str, Any], key: str, provider: str, *, required: bool = False
) -> str:
    value = obj.get(key)
    if value is None:
        if required:
            raise UpstreamInvalidResponseError(provider, f"{key} was missing")
        return ""
    if not isinstance(value, str):
        raise UpstreamInvalidResponseError(provider, f"{key} was not a string")
    if required and not value:
        raise UpstreamInvalidResponseError(provider, f"{key} was empty")
    return value


def int_value(
    obj: dict[str, Any], key: str, provider: str, *, required: bool = False
) -> int | None:
    value = obj.get(key)
    if value is None:
        if required:
            raise UpstreamInvalidResponseError(provider, f"{key} was missing")
        return None
    if isinstance(value, bool):
        raise UpstreamInvalidResponseError(provider, f"{key} was not an integer")
    if isinstance(value, int):
        return value
    if isinstance(value, str):
        try:
            return int(value)
        except ValueError as exc:
            raise UpstreamInvalidResponseError(
                provider, f"{key} was not an integer"
            ) from exc
    raise UpstreamInvalidResponseError(provider, f"{key} was not an integer")


def float_value(obj: dict[str, Any], key: str, provider: str) -> float | None:
    value = obj.get(key)
    if value is None:
        return None
    if isinstance(value, bool):
        raise UpstreamInvalidResponseError(provider, f"{key} was not numeric")
    if isinstance(value, (int, float, str)):
        try:
            return float(value)
        except ValueError as exc:
            raise UpstreamInvalidResponseError(
                provider, f"{key} was not numeric"
            ) from exc
    raise UpstreamInvalidResponseError(provider, f"{key} was not numeric")
