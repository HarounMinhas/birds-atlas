using BirdsAtlas.API.Data;
using BirdsAtlas.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BirdsAtlas.API.Services;

public class BirdQueryService
{
    private readonly BirdsDbContext _db;

    public BirdQueryService(BirdsDbContext db) => _db = db;

    public async Task<PagedResult<BirdListItemDto>> GetBirdsAsync(BirdFilterRequest filter)
    {
        var query = _db.Birds
            .Include(b => b.Continents)
            .Include(b => b.Media)
            .Include(b => b.Characteristics)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Continent))
            query = query.Where(b => b.Continents.Any(c => c.ContinentCode == filter.Continent));

        if (!string.IsNullOrWhiteSpace(filter.Order))
            query = query.Where(b => b.Order == filter.Order);

        if (!string.IsNullOrWhiteSpace(filter.Family))
            query = query.Where(b => b.Family == filter.Family);

        if (!string.IsNullOrWhiteSpace(filter.Genus))
            query = query.Where(b => b.Genus == filter.Genus);

        if (!string.IsNullOrWhiteSpace(filter.HabitatType))
            query = query.Where(b => b.HabitatType == filter.HabitatType);

        if (!string.IsNullOrWhiteSpace(filter.ConservationStatus))
            query = query.Where(b => b.ConservationStatus == filter.ConservationStatus);

        if (!string.IsNullOrWhiteSpace(filter.BeakColor))
            query = query.Where(b => b.Characteristics
                .Any(c => c.Key == "beak_color" && c.Value == filter.BeakColor));

        if (!string.IsNullOrWhiteSpace(filter.BreastColor))
            query = query.Where(b => b.Characteristics
                .Any(c => c.Key == "breast_color" && c.Value == filter.BreastColor));

        if (!string.IsNullOrWhiteSpace(filter.BackColor))
            query = query.Where(b => b.Characteristics
                .Any(c => c.Key == "back_color" && c.Value == filter.BackColor));

        if (!string.IsNullOrWhiteSpace(filter.SizeCategory))
            query = query.Where(b => b.Characteristics
                .Any(c => c.Key == "size_category" && c.Value == filter.SizeCategory));

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(b =>
                b.CommonNameEn.Contains(filter.Search) ||
                b.CommonNameNl.Contains(filter.Search) ||
                b.ScientificName.Contains(filter.Search));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(b => b.CommonNameEn)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(b => new BirdListItemDto(
                b.Id,
                b.CommonNameEn,
                b.ScientificName,
                b.Order,
                b.Family,
                b.ConservationStatus,
                b.Media.Where(m => m.IsPrimary && m.Type == MediaType.Image)
                       .Select(m => m.Url).FirstOrDefault(),
                b.Continents.Select(c => c.ContinentCode).ToList()
            ))
            .ToListAsync();

        return new PagedResult<BirdListItemDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<BirdDto?> GetBirdDetailAsync(int id)
    {
        var bird = await _db.Birds
            .Include(b => b.Characteristics)
            .Include(b => b.Media)
            .Include(b => b.Continents)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bird is null) return null;

        return new BirdDto(
            bird.Id,
            bird.CommonNameEn,
            bird.CommonNameNl,
            bird.ScientificName,
            bird.Order,
            bird.Family,
            bird.Genus,
            bird.ConservationStatus,
            bird.HabitatType,
            bird.Media.Where(m => m.IsPrimary && m.Type == MediaType.Image).Select(m => m.Url).FirstOrDefault(),
            bird.Continents.Select(c => c.ContinentCode).ToList(),
            bird.Characteristics.Select(c => new CharacteristicDto(c.Key, c.Value)).ToList()
        );
    }

    public async Task<BirdFilterOptions> GetFilterOptionsAsync() => new(
        Continents: await _db.BirdContinents.Select(c => c.ContinentCode).Distinct().OrderBy(x => x).ToListAsync(),
        Orders: await _db.Birds.Select(b => b.Order).Where(o => o != "").Distinct().OrderBy(x => x).ToListAsync(),
        Families: await _db.Birds.Select(b => b.Family).Where(f => f != "").Distinct().OrderBy(x => x).ToListAsync(),
        BeakColors: await _db.BirdCharacteristics.Where(c => c.Key == "beak_color").Select(c => c.Value).Distinct().OrderBy(x => x).ToListAsync(),
        BreastColors: await _db.BirdCharacteristics.Where(c => c.Key == "breast_color").Select(c => c.Value).Distinct().OrderBy(x => x).ToListAsync(),
        SizeCategories: await _db.BirdCharacteristics.Where(c => c.Key == "size_category").Select(c => c.Value).Distinct().OrderBy(x => x).ToListAsync(),
        HabitatTypes: await _db.Birds.Select(b => b.HabitatType).Where(h => h != null).Distinct().OrderBy(x => x).ToListAsync()!,
        ConservationStatuses: await _db.Birds.Select(b => b.ConservationStatus).Where(s => s != null).Distinct().OrderBy(x => x).ToListAsync()!
    );

    public async Task<List<string>> GetOrdersAsync() =>
        await _db.Birds.Select(b => b.Order).Where(o => o != "").Distinct().OrderBy(x => x).ToListAsync();

    public async Task<List<string>> GetFamiliesAsync(string? order) =>
        await _db.Birds
            .Where(b => order == null || b.Order == order)
            .Select(b => b.Family).Where(f => f != "").Distinct().OrderBy(x => x)
            .ToListAsync();

    public async Task<List<string>> GetGeneraAsync(string? family) =>
        await _db.Birds
            .Where(b => family == null || b.Family == family)
            .Select(b => b.Genus).Where(g => g != "").Distinct().OrderBy(x => x)
            .ToListAsync();
}
