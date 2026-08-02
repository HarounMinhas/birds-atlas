from typing import Protocol

from birds_atlas.domain.models import BirdRecording


class RecordingProviderPort(Protocol):
    async def recordings(self, scientific_name: str) -> tuple[BirdRecording, ...]: ...
