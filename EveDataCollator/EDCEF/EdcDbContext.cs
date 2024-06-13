using EveDataCollator.Eve;
using Microsoft.EntityFrameworkCore;

namespace EveDataCollator.EDCEF;

public class EdcDbContext : DbContext
{
    public DbSet<AsteroidBelt> AsteroidBelts { get; set; }
    public DbSet<Constellation> Constellations { get; set; }
    public DbSet<Moon> Moons { get; set; }
    public DbSet<Planet> Planets { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<SolarSystem> SolarSystems { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        string DbPath = $"{System.AppContext.BaseDirectory}edcDb.sqlite";
        optionsBuilder.UseSqlite($"Data Source={DbPath}");
    }
}