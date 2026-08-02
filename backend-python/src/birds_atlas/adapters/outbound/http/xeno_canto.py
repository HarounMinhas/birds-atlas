from __future__ import annotations

from urllib.parse import urlparse

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.adapters.outbound.http.parsing import (
    list_value,
    object_value,
    string_value,
)
from birds_atlas.domain.exceptions import UpstreamError, UpstreamInvalidResponseError
from birds_atlas.domain.models import BirdRecording


class XenoCantoHttpAdapter:
    provider = "Xeno-canto"

    def __init__(self, client: AsyncHttpClient, api_key: str | None) -> None:
        self._client = client
        self._api_key = api_key

    async def recordings(self, scientific_name: str) -> tuple[BirdRecording, ...]:
        if not self._api_key:
            return ()
        parts = scientific_name.split()
        if len(parts) < 2:
            return ()
        try:
            payload = await self._client.get_json(
                "api/3/recordings",
                params={
                    "query": f"gen:{parts[0]} sp:{parts[1]}",
                    "key": self._api_key,
                },
            )
            root = object_value(payload, self.provider, "recordings response")
            raw_recordings = root.get("recordings")
            if raw_recordings is None:
                return ()
            result: list[BirdRecording] = []
            seen: set[str] = set()
            for raw in list_value(raw_recordings, self.provider, "recordings"):
                record = object_value(raw, self.provider, "recording")
                recording_id = string_value(record, "id", self.provider, required=True)
                if recording_id in seen:
                    raise UpstreamInvalidResponseError(self.provider, "duplicate recording ID")
                seen.add(recording_id)
                file_url = string_value(record, "file", self.provider)
                if not file_url:
                    continue
                if file_url.startswith("//"):
                    file_url = "https:" + file_url
                parsed = urlparse(file_url)
                if parsed.scheme not in {"http", "https"} or not parsed.netloc:
                    raise UpstreamInvalidResponseError(self.provider, "invalid audio URL")
                scientific = " ".join(
                    part
                    for part in (
                        string_value(record, "gen", self.provider),
                        string_value(record, "sp", self.provider),
                    )
                    if part
                )
                result.append(
                    BirdRecording(
                        id=recording_id,
                        common_name=string_value(record, "en", self.provider),
                        scientific_name=scientific,
                        recordist=string_value(record, "rec", self.provider),
                        type=string_value(record, "type", self.provider),
                        country=string_value(record, "cnt", self.provider),
                        length=string_value(record, "length", self.provider),
                        file_url=file_url,
                        license_url=string_value(record, "lic", self.provider),
                        source_url=f"https://xeno-canto.org/{recording_id}",
                    )
                )
                if len(result) == 3:
                    break
            return tuple(result)
        except UpstreamError:
            return ()
