using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Course.Data;

public sealed class CategoryRow
{
    public string Name { get; set; } = "";
    public List<ExpenseRow> Expenses { get; set; } = [];
}

public sealed class ExpenseRow
{
    public long Sequence { get; set; }
    public string ExternalId { get; set; } = "";
    public int Day { get; set; }
    public string CategoryName { get; set; } = "";
    public long AmountMinor { get; set; }
    public long Version { get; set; }
    public string? Note { get; set; }
    public CategoryRow Category { get; set; } = null!;
}

public sealed class ImportReceiptRow
{
    public string BatchId { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public int Added { get; set; }
    public int Duplicates { get; set; }
}

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<CategoryRow> Categories => Set<CategoryRow>();
    public DbSet<ExpenseRow> Expenses => Set<ExpenseRow>();
    public DbSet<ImportReceiptRow> ImportReceipts => Set<ImportReceiptRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CategoryRow>(b =>
        {
            b.ToTable("Categories", t => t.HasCheckConstraint("CK_Category_Name",
                "length(Name) BETWEEN 1 AND 32 AND Name GLOB '[a-z]*' AND Name NOT GLOB '*[^a-z0-9_-]*'"));
            b.HasKey(e => e.Name);
            b.Property(e => e.Name).ValueGeneratedNever();
        });
        model.Entity<ExpenseRow>(b =>
        {
            b.ToTable("Expenses", t =>
            {
                t.HasCheckConstraint("CK_Expense_Amount", "typeof(AmountMinor) = 'integer' AND AmountMinor BETWEEN 1 AND 99999999");
                t.HasCheckConstraint("CK_Expense_Day", "typeof(Day) = 'integer' AND Day BETWEEN 0 AND 3652058");
                t.HasCheckConstraint("CK_Expense_Version", "typeof(Version) = 'integer' AND Version >= 0");
                t.HasCheckConstraint("CK_Expense_Id", "length(ExternalId) = 36");
            });
            b.HasKey(e => e.Sequence);
            b.HasIndex(e => e.ExternalId).IsUnique();
            b.HasIndex(e => new { e.Day, e.Sequence });
            b.Property(e => e.ExternalId).IsRequired();
            b.Property(e => e.Version).IsConcurrencyToken();
            b.HasOne(e => e.Category).WithMany(e => e.Expenses)
                .HasForeignKey(e => e.CategoryName).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ImportReceiptRow>(b =>
        {
            b.ToTable("ImportReceipts");
            b.HasKey(e => e.BatchId);
            b.Property(e => e.BatchId).ValueGeneratedNever();
            b.Property(e => e.Fingerprint).IsRequired();
        });
    }
}

// Tooling creates the model without opening or changing any persistent database.
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite("Data Source=:memory:").Options);
}
