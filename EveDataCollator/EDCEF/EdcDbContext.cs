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
    public DbSet<Star> Stars { get; set; }
    public DbSet<SolarSystem> SolarSystems { get; set; }
    public DbSet<Station> Stations { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        string DbPath = $"{System.AppContext.BaseDirectory}edcDb.sqlite";
        optionsBuilder.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Region>().Property(r => r.Center).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<Region>().Property(r => r.Min).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<Region>().Property(r => r.Max).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<Constellation>().Property(c => c.Center).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<Constellation>().Property(c => c.Min).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<Constellation>().Property(c => c.Max).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<AsteroidBelt>().Property(a => a.Position).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<Moon>().Property(m => m.Position).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<Planet>().Property(p => p.Position).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<SolarSystem>().Property(s => s.Center).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<SolarSystem>().Property(s => s.Min).HasConversion(new DecVector3Converter());
        modelBuilder.Entity<SolarSystem>().Property(s => s.Max).HasConversion(new DecVector3Converter());
        
        modelBuilder.Entity<Stargate>().Property(s => s.Position).HasConversion(new DecVector3Converter());
    }

    public void DropAllTables()
    {
        // Query to generate drop table statements
        var dropTableSql = @"
            PRAGMA foreign_keys=OFF;
            BEGIN TRANSACTION;
            WITH DropStatements AS (
                SELECT 'DROP TABLE IF EXISTS ""' || name || '"";' AS sql
                FROM sqlite_master 
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            )
            SELECT sql FROM DropStatements;
            COMMIT;";

        // Execute drop table statements
        var dropStatements = Database.ExecuteSqlRaw(dropTableSql);
    }
}