class BirdsAtlasError(Exception):
    """Base class for application-safe failures."""


class DomainValidationError(BirdsAtlasError):
    pass


class BirdNotFoundError(BirdsAtlasError):
    pass


class UpstreamError(BirdsAtlasError):
    def __init__(self, provider: str, message: str, *, status_code: int | None = None) -> None:
        super().__init__(message)
        self.provider = provider
        self.status_code = status_code


class UpstreamTimeoutError(UpstreamError):
    pass


class UpstreamRateLimitError(UpstreamError):
    pass


class UpstreamInvalidResponseError(UpstreamError):
    pass


class UpstreamUnavailableError(UpstreamError):
    pass
