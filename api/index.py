from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "backend-python" / "src"
if str(SRC) not in sys.path:
    sys.path.insert(0, str(SRC))

from birds_atlas.adapters.inbound.fastapi.app import app  # noqa: E402,F401
