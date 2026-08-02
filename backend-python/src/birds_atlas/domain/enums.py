from __future__ import annotations

from enum import StrEnum


class Continent(StrEnum):
    AFRICA = "AFRICA"
    ASIA = "ASIA"
    EUROPE = "EUROPE"
    NORTH_AMERICA = "NORTH_AMERICA"
    SOUTH_AMERICA = "SOUTH_AMERICA"
    OCEANIA = "OCEANIA"
    ANTARCTICA = "ANTARCTICA"

    @classmethod
    def parse_optional(cls, value: str | None) -> Continent | None:
        if value is None or not value.strip():
            return None
        normalized = value.strip().upper().replace("-", "_").replace(" ", "_")
        try:
            return cls(normalized)
        except ValueError:
            return None


class SortOption(StrEnum):
    OBSERVATIONS = "observations"
    NAME = "name"

    @classmethod
    def parse(cls, value: str | None) -> SortOption:
        return cls.NAME if value and value.casefold() == "name" else cls.OBSERVATIONS
