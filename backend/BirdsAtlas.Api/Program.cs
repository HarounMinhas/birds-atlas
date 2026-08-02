using System.Net.Mime;
using System.Text.Json.Serialization;
using BirdsAtlas.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("inat", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:INaturalist"] ?? "https://api.inaturalist.org/");
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BirdsAtlas/1.0 (+https://github.com/HarounMinhas/birds-atlas)");
});
builder.Services.AddHttpClient("gbif", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:Gbif"] ?? "https://api.gbif.org/");
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BirdsAtlas/1.0 (+https://github.com/HarounMinhas/birds-atlas)");
});
builder.Services.AddHttpClient("wiki", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BirdsAtlas/1.0 (+https://github.com/HarounMinhas/birds-atlas)");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});
builder.Services.AddHttpClient("xeno", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:XenoCanto"] ?? "https://xeno-canto.org/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BirdsAtlas/1.0 (+https://github.com/HarounMinhas/birds-atlas)");
});
builder.Services.AddSingleton<BirdDataService>();
builder.Services.AddSingleton<BirdSearchService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
    .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

app.MapGet("/api/birds", async (
    string? q,
    int? page,
    int? pageSize,
    string? continent,
    string? order,
    string? family,
    string? genus,
    string? sort,
    BirdSearchService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.SearchAsync(
        q,
        Math.Clamp(page ?? 1, 1, 10_000),
        Math.Clamp(pageSize ?? 24, 1, 48),
        continent,
        order,
        family,
        genus,
        sort,
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/birds/{id:long}", async (long id, BirdDataService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetBirdAsync(id, cancellationToken);
    return result is null ? Results.NotFound(new { message = "Bird taxon not found." }) : Results.Ok(result);
});

app.MapGet("/api/birds/{id:long}/occurrences", async (
    long id,
    int? limit,
    BirdDataService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetOccurrencesAsync(id, Math.Clamp(limit ?? 300, 1, 500), cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/taxonomy/options", async (BirdDataService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetTaxonomyOptionsAsync(cancellationToken)));

app.Run();
