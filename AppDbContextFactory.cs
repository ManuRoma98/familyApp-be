using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace familyApp.Server
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // 1) ENV first
            var envPath = Environment.GetEnvironmentVariable("FAMILY_ERP_DB_PATH");

            // 2) appsettings fallback
            var cs = config.GetConnectionString("DefaultConnection");

            // 3) ultimate fallback
            if (string.IsNullOrWhiteSpace(envPath) && string.IsNullOrWhiteSpace(cs))
                cs = "Data Source=.\\familyERP.db";

            // Se c'è ENV, usiamo quello e ignoriamo la CS
            if (!string.IsNullOrWhiteSpace(envPath))
                cs = $"Data Source={envPath}";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(cs)
                .Options;

            return new AppDbContext(options);
        }
    }
}
