using Microsoft.EntityFrameworkCore;

namespace familyApp.Server
{

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Users> Users { get; set; }
        public DbSet<Courses> Courses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tabella Users
            modelBuilder.Entity<Users>().HasKey(u => u.Id);
            modelBuilder.Entity<Users>().Property(u => u.Name).IsRequired();
            modelBuilder.Entity<Users>().Property(u => u.Email).IsRequired();
            modelBuilder.Entity<Users>().Property(u => u.BirthDate).HasColumnType("DATE");
            modelBuilder.Entity<Users>().Property(u => u.Status).IsRequired().HasDefaultValue(1);

            // Tabella Courses
            modelBuilder.Entity<Courses>().HasKey(u => u.Id);
            modelBuilder.Entity<Courses>().Property(u => u.CourseName).IsRequired();

            // Tabella UserCourses
            modelBuilder.Entity<Users>()
                        .HasMany(u => u.Courses)
                        .WithMany(c => c.Users)
                        .UsingEntity<Dictionary<string, object>>(
                            "UserCourses",
                            j => j.HasOne<Courses>().WithMany().HasForeignKey("CourseId"),
                            j => j.HasOne<Users>().WithMany().HasForeignKey("UserId")
                        );
        }
    }

    public class Users
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public required string Surname { get; set; }
        public required string Email { get; set; }
        public DateTime BirthDate { get; set; }
        public int Status { get; set; }
        public List<Courses> Courses { get; set; } = [];
    }

    public class Courses
    {
        public int Id { get; set; }
        public int CourseName { get; set; }
        public List<Users> Users { get; set; } = [];
    }
}
