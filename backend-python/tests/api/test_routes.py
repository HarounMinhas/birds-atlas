def test_health(client) -> None:
    response = client.get("/api/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
    assert response.headers["cache-control"] == "no-store"


def test_search_defaults_and_shape(client) -> None:
    response = client.get("/api/birds")
    assert response.status_code == 200
    body = response.json()
    assert body["page"] == 1
    assert body["pageSize"] == 24
    assert body["items"][0]["scientificName"] == "Turdus merula"
    assert "s-maxage=300" in response.headers["cache-control"]


def test_detail_and_occurrences(client) -> None:
    detail = client.get("/api/birds/1")
    assert detail.status_code == 200
    assert detail.json()["taxonomy"]["className"] == "Aves"
    occurrences = client.get("/api/birds/1/occurrences")
    assert occurrences.status_code == 200
    assert occurrences.json()[0]["eventDate"] is None


def test_not_found_and_validation(client) -> None:
    assert client.get("/api/birds/999").status_code == 404
    assert client.get("/api/birds?pageSize=49").status_code == 422


def test_taxonomy_options(client) -> None:
    response = client.get("/api/taxonomy/options")
    assert response.status_code == 200
    assert response.json() == {
        "orders": ["Passeriformes"],
        "families": ["Turdidae"],
        "genera": ["Turdus"],
    }
