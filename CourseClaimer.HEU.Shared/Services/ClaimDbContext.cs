using CourseClaimer.HEU.Shared.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CourseClaimer.HEU.Shared.Services;

public class ClaimDbContext : DbContext
{
    public ClaimDbContext(DbContextOptions<ClaimDbContext> options)
    {

    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //optionsBuilder.UseSqlServer(@"Server=.;Database=ClaimerDb;Trusted_Connection=True;TrustServerCertificate=true");
        optionsBuilder.UseSqlite(@"Data Source=ClaimerDB.db;");
        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<ClaimRecord> ClaimRecords { get; set; }
    public DbSet<EntityRecord> EntityRecords { get; set; }
}