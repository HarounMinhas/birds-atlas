using BirdsAtlas.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdsAtlas.API.Data;

public class BirdsDbContext : DbContext
{
    public BirdsDbContext(DbContextOptions<BirdsDbContext> options) : base(options) { }

    public DbSet<Bird> Birds => Set<Bird>();
    public DbSet<BirdCharacteristic> BirdCharacteristics => Set<BirdCharacteristic>();
    public DbSet<BirdMedia> BirdMedia => Set<BirdMedia>();
    public DbSet<BirdContinent> BirdContinents => Set<BirdContinent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bird>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => b.ScientificName).IsUnique();
            entity.HasIndex(b => b.Order);
            entity.HasIndex(b => b.Family);
        });

        modelBuilder.Entity<BirdCharacteristic>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.BirdId, c.Key });
        });

        modelBuilder.Entity<BirdMedia>(entity =>
        {
            entity.HasKey(m => m.Id);
        });

        modelBuilder.Entity<BirdContinent>(entity =>
        {
            entity.HasKey(bc => new { bc.BirdId, bc.ContinentCode });
        });
    }
}
