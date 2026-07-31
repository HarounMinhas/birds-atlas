using BirdsAtlas.API.DTOs;
using BirdsAtlas.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BirdsAtlas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BirdsController : ControllerBase
{
    private readonly BirdQueryService _queryService;
    private readonly GbifSyncService _syncService;

    public BirdsController(BirdQueryService queryService, GbifSyncService syncService)
    {
        _queryService = queryService;
        _syncService = syncService;
    }

    // GET /api/birds?continent=EUROPE&order=Passeriformes&beakColor=red&page=1
    [HttpGet]
    public async Task<ActionResult<PagedResult<BirdListItemDto>>> GetBirds([FromQuery] BirdFilterRequest filter)
    {
        var result = await _queryService.GetBirdsAsync(filter);
        return Ok(result);
    }

    // GET /api/birds/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BirdDto>> GetBird(int id)
    {
        var bird = await _queryService.GetBirdDetailAsync(id);
        return bird is null ? NotFound() : Ok(bird);
    }

    // GET /api/birds/filters — returns all available filter options
    [HttpGet("filters")]
    public async Task<ActionResult<BirdFilterOptions>> GetFilterOptions()
    {
        var options = await _queryService.GetFilterOptionsAsync();
        return Ok(options);
    }

    // POST /api/birds/sync — manual trigger (admin/dev)
    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync()
    {
        await _syncService.SyncAllBirdsAsync();
        return Accepted(new { message = "Sync started in background" });
    }
}
