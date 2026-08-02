from dataclasses import dataclass
from datetime import datetime

from birds_atlas.application.ports.clock import ClockPort


@dataclass(slots=True)
class CheckHealth:
    clock: ClockPort

    def execute(self) -> tuple[str, datetime]:
        return "ok", self.clock.utcnow()
