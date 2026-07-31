using BirdsAtlas.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BirdsAtlas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaxonomyController : ControllerBase
{
    private readonly BirdQueryService _queryService;

    public TaxonomyController(BirdQueryService queryService)
    {
        _queryService = queryService;
    }

    // GET /api/taxonomy/orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders() =>
        Ok(await _queryService.GetOrdersAsync());

    // GET /api/taxonomy/families?order=Passeriformes
    [HttpGet("families")]
    public async Task<IActionResult> GetFamilies([FromQuery] string? order) =>
        Ok(await _queryService.GetFamiliesAsync(order));

    // GET /api/taxonomy/genera?family=Turdidae
    [HttpGet("genera")]
    public async Task<IActionResult> GetGenera([FromQuery] string? family) =>
        Ok(await _queryService.GetGeneraAsync(family));
}
