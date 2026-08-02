from fastapi import Request

from birds_atlas.application.use_cases.check_health import CheckHealth
from birds_atlas.application.use_cases.get_bird_detail import GetBirdDetail
from birds_atlas.application.use_cases.get_occurrences import GetBirdOccurrences
from birds_atlas.application.use_cases.get_taxonomy_options import GetTaxonomyOptions
from birds_atlas.application.use_cases.search_birds import SearchBirds
from birds_atlas.bootstrap import Container


def _container(request: Request) -> Container:
    return request.app.state.container


def get_search_birds(request: Request) -> SearchBirds:
    return _container(request).search_birds


def get_bird_detail(request: Request) -> GetBirdDetail:
    return _container(request).get_bird_detail


def get_occurrences(request: Request) -> GetBirdOccurrences:
    return _container(request).get_occurrences


def get_taxonomy_options(request: Request) -> GetTaxonomyOptions:
    return _container(request).get_taxonomy_options


def get_check_health(request: Request) -> CheckHealth:
    return _container(request).check_health
