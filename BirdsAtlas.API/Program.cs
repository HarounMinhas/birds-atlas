using BirdsAtlas.API.Data;
using BirdsAtlas.API.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQL Server
builder.Services.AddDbContext<BirdsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Hangfire
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

// HTTP Clients
builder.Services.AddHttpClient<GbifSyncService>(client =>
{
    client.BaseAddress = new Uri("https://api.gbif.org/v1/");
    client.DefaultRequestHeaders.Add("User-Agent", "BirdsAtlas/1.0");
});
builder.Services.AddHttpClient<InatMediaService>(client =>
{
    client.BaseAddress = new Uri("https://api.inaturalist.org/v1/");
});
builder.Services.AddHttpClient<XenoCantoService>(client =>
{
    client.BaseAddress = new Uri("https://xeno-canto.org/api/2/");
});

builder.Services.AddScoped<GbifSyncService>();
builder.Services.AddScoped<InatMediaService>();
builder.Services.AddScoped<XenoCantoService>();
builder.Services.AddScoped<BirdQueryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

// Hangfire dashboard
app.UseHangfireDashboard("/hangfire");

// Schedule daily sync at 03:00
RecurringJob.AddOrUpdate<GbifSyncService>(
    "gbif-daily-sync",
    service => service.SyncAllBirdsAsync(),
    "0 3 * * *");

app.Run();
