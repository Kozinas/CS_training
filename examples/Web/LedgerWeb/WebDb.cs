using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Course.Web;

public sealed class WebUser : IdentityUser { public long Revision { get; set; } }
public sealed class WebExpense
{
    public long Sequence { get; set; }
    public string Owner { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public int Day { get; set; }
    public string Category { get; set; } = "";
    public long AmountMinor { get; set; }
    public long Version { get; set; }
    public string? Note { get; set; }
}
public sealed class WebReceipt
{
    public string Owner { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public int Added { get; set; }
    public int Duplicates { get; set; }
}
public sealed class OutboxRow
{
    public long Sequence { get; set; }
    public string MessageId { get; set; } = "";
    public string Owner { get; set; } = "";
    public int CountDelta { get; set; }
    public long AmountDelta { get; set; }
    public bool Delivered { get; set; }
}
public sealed class InboxRow { public string MessageId { get; set; } = ""; }
public sealed class ProjectionRow
{
    public string Owner { get; set; } = "";
    public long Count { get; set; }
    public long AmountMinor { get; set; }
}

public sealed class WebDb(DbContextOptions<WebDb> options) : IdentityDbContext<WebUser>(options)
{
    public DbSet<WebExpense> Expenses => Set<WebExpense>();
    public DbSet<WebReceipt> Receipts => Set<WebReceipt>();
    public DbSet<OutboxRow> Outbox => Set<OutboxRow>();
    public DbSet<InboxRow> Inbox => Set<InboxRow>();
    public DbSet<ProjectionRow> Projections => Set<ProjectionRow>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<WebExpense>(e =>
        {
            e.HasKey(x => x.Sequence);
            e.HasIndex(x => new { x.Owner, x.ExternalId }).IsUnique();
            e.HasIndex(x => new { x.Owner, x.Sequence });
            e.HasOne<WebUser>().WithMany().HasForeignKey(x => x.Owner).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.ToTable("Expenses", t =>
            {
                t.HasCheckConstraint("CK_Amount", "typeof(AmountMinor) = 'integer' AND AmountMinor BETWEEN 1 AND 99999999");
                t.HasCheckConstraint("CK_Version", "Version >= 0");
                t.HasCheckConstraint("CK_Day", "Day BETWEEN 0 AND 3652058");
            });
        });
        b.Entity<WebReceipt>().HasKey(x => new { x.Owner, x.BatchId });
        b.Entity<WebReceipt>().HasOne<WebUser>().WithMany().HasForeignKey(x => x.Owner).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OutboxRow>().HasKey(x => x.Sequence);
        b.Entity<OutboxRow>().HasIndex(x => x.MessageId).IsUnique();
        b.Entity<OutboxRow>().HasIndex(x => new { x.Delivered, x.Sequence });
        b.Entity<InboxRow>().HasKey(x => x.MessageId);
        b.Entity<ProjectionRow>().HasKey(x => x.Owner);
    }
}
public sealed class WebDesignFactory : IDesignTimeDbContextFactory<WebDb>
{
    public WebDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WebDb>()
        .UseSqlite("Data Source=:memory:").Options);
}
