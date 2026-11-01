using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StudioCore.Models;

namespace StudioCore.Persistence;

// Design-time commands generate scripts only; never resolve production configuration.
public sealed class StudioDbContextFactory : IDesignTimeDbContextFactory<StudioDbContext>
{
    public StudioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StudioDbContext>();
        if (args.Contains("--postgres")) options.UseNpgsql("Host=localhost;Database=studiocore_design;Username=design");
        else options.UseSqlite("Data Source=studiocore-design.db");
        return new StudioDbContext(options.Options, Studio.MasterId);
    }
}
