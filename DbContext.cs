using Microsoft.EntityFrameworkCore;

namespace familyApp.Server
{

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Account> Account { get; set; }
        public DbSet<Category> Category { get; set; }
        public DbSet<Transaction> Transaction { get; set; }

        public DbSet<AppAccount> AppAccounts { get; set; }
        public DbSet<AppAccountLine> AppAccountLines { get; set; }
        public DbSet<AppMovement> AppMovements { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }

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
            modelBuilder.Entity<Category>()
                .HasMany(c => c.SubCategories)
                .WithOne(sc => sc.Category)
                .HasForeignKey(sc => sc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubCategory>().HasKey(sc => sc.Id);
            modelBuilder.Entity<SubCategory>().Property(sc => sc.Name).IsRequired().HasMaxLength(128);
            modelBuilder.Entity<SubCategory>().Property(sc => sc.Description).HasMaxLength(256);

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

            // AppAccount (testata per utente)
            modelBuilder.Entity<AppAccount>().HasKey(a => a.Id);
            modelBuilder.Entity<AppAccount>().Property(a => a.Username).IsRequired().HasMaxLength(64);
            modelBuilder.Entity<AppAccount>().HasIndex(a => a.Username).IsUnique();
            modelBuilder.Entity<AppAccount>().Property(a => a.DisplayName).HasMaxLength(128);
            modelBuilder.Entity<AppAccount>().Property(a => a.MonthlyBudget).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            modelBuilder.Entity<AppAccount>().Property(a => a.AnnualSavingsGoal).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            modelBuilder.Entity<AppAccount>().Property(a => a.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<AppAccount>().Property(a => a.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // AppAccountLine (conti collegati al conto app)
            modelBuilder.Entity<AppAccountLine>().HasKey(l => l.Id);
            modelBuilder.Entity<AppAccountLine>().Property(l => l.Name).IsRequired().HasMaxLength(128);
            modelBuilder.Entity<AppAccountLine>().Property(l => l.Institution).HasMaxLength(128);
            modelBuilder.Entity<AppAccountLine>().Property(l => l.Kind).HasMaxLength(64);
            modelBuilder.Entity<AppAccountLine>().Property(l => l.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<AppAccountLine>().Property(l => l.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<AppAccountLine>()
                .HasOne(l => l.AppAccount)
                .WithMany(a => a.Lines)
                .HasForeignKey(l => l.AppAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // AppMovement (movimentazioni)
            modelBuilder.Entity<AppMovement>().HasKey(m => m.Id);
            modelBuilder.Entity<AppMovement>().Property(m => m.Amount).HasColumnType("decimal(18,2)").IsRequired();
            modelBuilder.Entity<AppMovement>().Property(m => m.IsNegative).IsRequired();
            modelBuilder.Entity<AppMovement>().Property(m => m.IsTransfer).HasDefaultValue(false).IsRequired();
            modelBuilder.Entity<AppMovement>().Property(m => m.MovementDate).HasColumnType("date").IsRequired();
            modelBuilder.Entity<AppMovement>().Property(m => m.Note).HasMaxLength(512);
            modelBuilder.Entity<AppMovement>().Property(m => m.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<AppMovement>().Property(m => m.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<AppMovement>()
                .HasOne(m => m.SubCategory)
                .WithMany(sc => sc.Movements)
                .HasForeignKey(m => m.SubCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<AppMovement>()
                .HasOne(m => m.AppAccount)
                .WithMany(a => a.Movements)
                .HasForeignKey(m => m.AppAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AppMovement>()
                .HasOne(m => m.AppAccountLine)
                .WithMany(l => l.Movements)
                .HasForeignKey(m => m.AppAccountLineId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AppMovement>().HasIndex(m => m.MovementDate);
            modelBuilder.Entity<AppMovement>().HasIndex(m => new { m.AppAccountId, m.IsNegative });
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
        public List<SubCategory> SubCategories { get; set; } = [];
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

    public class AppAccount
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? DisplayName { get; set; }
        public decimal MonthlyBudget { get; set; }
        public decimal AnnualSavingsGoal { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<AppAccountLine> Lines { get; set; } = [];
        public List<AppMovement> Movements { get; set; } = [];
    }

    public class AppAccountLine
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Institution { get; set; }
        public string? Kind { get; set; }

        public int AppAccountId { get; set; }
        public AppAccount AppAccount { get; set; } = null!;

        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<AppMovement> Movements { get; set; } = [];
    }

    public class AppMovement
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        // false = positivo, true = negativo
        public bool IsNegative { get; set; }
        public bool IsTransfer { get; set; }
        public DateOnly MovementDate { get; set; }
        public string? Note { get; set; }

        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        public int AppAccountId { get; set; }
        public AppAccount AppAccount { get; set; } = null!;

        public int AppAccountLineId { get; set; }
        public AppAccountLine AppAccountLine { get; set; } = null!;

        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SubCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
        public required string Name { get; set; }
        public string? Description { get; set; }

        public List<AppMovement> Movements { get; set; } = [];
    }

}
