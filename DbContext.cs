using Microsoft.EntityFrameworkCore;

namespace familyApp.Server
{

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Account
            modelBuilder.Entity<Account>().HasKey(a => a.Id);
            modelBuilder.Entity<Account>().Property(a => a.Name).IsRequired();

            // Category
            modelBuilder.Entity<Category>().HasKey(c => c.Id);
            modelBuilder.Entity<Category>().Property(c => c.Name).IsRequired();
            modelBuilder.Entity<Category>().Property(c => c.Kind).IsRequired().HasMaxLength(16);
            // opzionale: vincolo di check sull'enum "Kind"
            modelBuilder.Entity<Category>()
                .ToTable(tb => tb.HasCheckConstraint("CK_Category_Kind", "Kind IN ('expense','income')"));

            // Transaction
            modelBuilder.Entity<Transaction>().HasKey(t => t.Id);
            modelBuilder.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)").IsRequired();
            modelBuilder.Entity<Transaction>().Property(t => t.Dt).HasColumnType("date").IsRequired();

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // indici utili
            modelBuilder.Entity<Transaction>().HasIndex(t => t.Dt);
            modelBuilder.Entity<Category>().HasIndex(c => new { c.Kind, c.Name }).IsUnique(false);
        }
    }

    public class Account
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<Transaction> Transactions { get; set; } = [];
    }
    
    public class Category
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        // "expense" | "income"
        public required string Kind { get; set; }
        public List<Transaction> Transactions { get; set; } = [];
    }
    
    public class Transaction
    {
        public int Id { get; set; }
        // Se usi .NET 6+ puoi usare DateOnly; altrimenti DateTime con .HasColumnType("date")
        public DateOnly Dt { get; set; }
        public decimal Amount { get; set; }     // sempre positivo; il "segno" lo deduciamo da Category.Kind
        public string? Note { get; set; }
    
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
    
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }

}
