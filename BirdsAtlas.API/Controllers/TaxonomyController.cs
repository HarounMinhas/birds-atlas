using BirdsAtlas.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BirdsAtlas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaxonomyController : ControllerBase
{
    private readonly GbifService _gbif;
    public TaxonomyController(GbifService gbif) => _gbif = gbif;

    /// <summary>All bird orders from GBIF (cached 6h).</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders() => Ok(await _gbif.GetOrdersAsync());
}
