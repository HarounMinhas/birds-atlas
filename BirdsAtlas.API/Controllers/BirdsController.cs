using BirdsAtlas.API.DTOs;
using BirdsAtlas.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BirdsAtlas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BirdsController : ControllerBase
{
    private readonly BirdAggregatorService _agg;
    public BirdsController(BirdAggregatorService agg) => _agg = agg;

    /// <summary>Search birds live. All data comes from GBIF + iNaturalist.</summary>
    /// <remarks>GET /api/birds?search=robin&amp;continent=EUROPE&amp;order=Passeriformes&amp;limit=24&amp;offset=0</remarks>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BirdSummaryDto>>> Search([FromQuery] BirdSearchRequest req)
        => Ok(await _agg.SearchAsync(req));

    /// <summary>Full bird detail — aggregates GBIF + iNaturalist + Xeno-canto in parallel.</summary>
    /// <remarks>GET /api/birds/5231190</remarks>
    [HttpGet("{gbifKey:int}")]
    public async Task<ActionResult<BirdDetailDto>> GetDetail(int gbifKey)
    {
        var detail = await _agg.GetDetailAsync(gbifKey);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Returns continent list + bird orders for filter dropdowns.</summary>
    [HttpGet("filters")]
    public async Task<ActionResult<FilterMetaDto>> GetFilters()
        => Ok(await _agg.GetFilterMetaAsync());
}
