from __future__ import annotations

from urllib.parse import quote, urlparse

from birds_atlas.adapters.outbound.http.client import AsyncHttpClient
from birds_atlas.adapters.outbound.http.parsing import object_value, string_value
from birds_atlas.domain.exceptions import UpstreamError, UpstreamInvalidResponseError
from birds_atlas.domain.models import EncyclopediaEntry


class WikipediaHttpAdapter:
    provider = "Wikipedia"

    def __init__(self, client: AsyncHttpClient) -> None:
        self._client = client

    async def summary(self, wikipedia_url: str | None, scientific_name: str) -> EncyclopediaEntry:
        candidates: list[str] = []
        fallback: str | None = None
        if wikipedia_url and self._safe_wikipedia_url(wikipedia_url):
            candidates.append(wikipedia_url)
            fallback = wikipedia_url
        title = quote(scientific_name.replace(" ", "_"))
        candidates.append(f"https://en.wikipedia.org/wiki/{title}")
        for candidate in dict.fromkeys(candidates):
            parsed = urlparse(candidate)
            title = parsed.path.rstrip("/").split("/")[-1]
            if not title:
                continue
            try:
                payload = await self._client.get_json(f"api/rest_v1/page/summary/{title}")
                root = object_value(payload, self.provider, "summary response")
                summary = string_value(root, "extract", self.provider)
                if not summary:
                    continue
                page_url = candidate
                content_urls = root.get("content_urls")
                if content_urls is not None:
                    urls = object_value(content_urls, self.provider, "content_urls")
                    desktop = object_value(urls.get("desktop"), self.provider, "desktop")
                    returned = string_value(desktop, "page", self.provider)
                    if returned:
                        if not self._safe_wikipedia_url(returned):
                            raise UpstreamInvalidResponseError(
                                self.provider, "unsafe Wikipedia page URL"
                            )
                        page_url = returned
                return EncyclopediaEntry(summary, page_url)
            except UpstreamError:
                continue
        return EncyclopediaEntry(None, fallback)

    @staticmethod
    def _safe_wikipedia_url(value: str) -> bool:
        parsed = urlparse(value)
        try:
            port = parsed.port
        except ValueError:
            return False
        host = (parsed.hostname or "").rstrip(".").casefold()
        return (
            parsed.scheme.casefold() == "https"
            and parsed.username is None
            and parsed.password is None
            and port is None
            and (host == "wikipedia.org" or host.endswith(".wikipedia.org"))
        )
