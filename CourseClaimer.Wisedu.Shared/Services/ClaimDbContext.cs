using CourseClaimer.Wisedu.Shared.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace CourseClaimer.Wisedu.Shared.Services;

public class ClaimDbContext : DbContext
{

    public ClaimDbContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<ClaimRecord> ClaimRecords { get; set; }
    public DbSet<EntityRecord> EntityRecords { get; set; }
    public DbSet<JobRecord> JobRecords { get; set; }
}