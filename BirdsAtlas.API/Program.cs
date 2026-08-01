using BirdsAtlas.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Short-lived in-memory cache to avoid hammering external APIs
builder.Services.AddMemoryCache();

// GBIF — free, no auth required
builder.Services.AddHttpClient<GbifService>(client =>
{
    client.BaseAddress = new Uri("https://api.gbif.org/v1/");
    client.DefaultRequestHeaders.Add("User-Agent", "BirdsAtlas/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// iNaturalist — free, no auth required
builder.Services.AddHttpClient<InatService>(client =>
{
    client.BaseAddress = new Uri("https://api.inaturalist.org/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Xeno-canto — free, no auth required
builder.Services.AddHttpClient<XenoCantoService>(client =>
{
    client.BaseAddress = new Uri("https://xeno-canto.org/api/2/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<GbifService>();
builder.Services.AddScoped<InatService>();
builder.Services.AddScoped<XenoCantoService>();
builder.Services.AddScoped<BirdAggregatorService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Birds Atlas API", Version = "v1",
        Description = "Live bird data — no local database. Aggregates GBIF, iNaturalist & Xeno-canto on-the-fly." }));

builder.Services.AddCors(o =>
    o.AddPolicy("AllowAngular", p =>
        p.WithOrigins("http://localhost:4200")
         .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();
app.Run();
