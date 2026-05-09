using FxRatesApi.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace FxRatesApi.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.Bid)
            .HasPrecision(18, 8);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.Ask)
            .HasPrecision(18, 8);
    }
}
