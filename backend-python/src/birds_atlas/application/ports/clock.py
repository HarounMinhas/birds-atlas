from datetime import datetime
from typing import Protocol


class ClockPort(Protocol):
    def utcnow(self) -> datetime: ...
