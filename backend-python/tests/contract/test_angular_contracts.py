from birds_atlas.adapters.inbound.fastapi.schemas import (
    BirdDetailDto,
    BirdListResponseDto,
    OccurrencePointDto,
    TaxonomyOptionsDto,
)


def test_contract_aliases_match_angular_models() -> None:
    list_properties = BirdListResponseDto.model_json_schema(by_alias=True)[
        "properties"
    ]
    assert set(list_properties) == {
        "page",
        "pageSize",
        "total",
        "isEstimate",
        "items",
    }
    detail = set(BirdDetailDto.model_json_schema(by_alias=True)["properties"])
    assert {
        "wikipediaSummary",
        "wikipediaUrl",
        "inaturalistUrl",
        "gbifUrl",
        "continents",
        "taxonomy",
        "recordings",
    } <= detail
    occurrence = set(
        OccurrencePointDto.model_json_schema(by_alias=True)["properties"]
    )
    assert occurrence == {
        "key",
        "latitude",
        "longitude",
        "country",
        "locality",
        "eventDate",
        "sourceUrl",
    }
    taxonomy = TaxonomyOptionsDto.model_json_schema(by_alias=True)["properties"]
    assert set(taxonomy) == {"orders", "families", "genera"}
