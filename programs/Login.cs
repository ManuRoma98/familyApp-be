using familyApp.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var envPath = Environment.GetEnvironmentVariable("FAMILY_ERP_DB_PATH");
var connectionString = !string.IsNullOrWhiteSpace(envPath)
    ? $"Data Source={envPath}"
    : builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=.\\familyERP.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Registra il servizio CORS PRIMA di `UseCors()`
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Configurazione del JWT
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("c2VncmV0a2V5MTIzNDU2Nzg5MDEyMzQ1Njc4OTA="));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

// Aggiungi autenticazione JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DashboardSeed.EnsureSeedData(db);
}

// API di Login per ottenere un token
app.MapPost("/api/auth/login", ([FromBody] LoginRequest request) =>
{
    if (request.Username == "a" && request.Password == "a")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Results.Json(new { token = tokenString });
    }

    return Results.Unauthorized();
});

app.MapGet("/api/dashboard/summary", [Authorize] async (ClaimsPrincipal principal, AppDbContext db) =>
{
    var username = principal.Identity?.Name ?? "a";

    var account = await db.AppAccounts
        .Include(a => a.Lines)
        .FirstOrDefaultAsync(a => a.Username == username);

    if (account is null)
        return Results.NotFound(new { message = $"Nessun conto app trovato per l'utente {username}" });

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var startOfWeek = today.AddDays(-6);
    var startOfMonth = new DateOnly(today.Year, today.Month, 1);
    var startOfYear = new DateOnly(today.Year, 1, 1);

    var movements = db.AppMovements.Where(m => m.AppAccountId == account.Id);

    decimal weeklySpend = await movements
        .Where(m => m.MovementDate >= startOfWeek && m.IsNegative && !m.IsTransfer)
        .SumAsync(m => (decimal?)m.Amount) ?? 0;

    decimal monthlySpend = await movements
        .Where(m => m.MovementDate >= startOfMonth && m.IsNegative && !m.IsTransfer)
        .SumAsync(m => (decimal?)m.Amount) ?? 0;

    decimal annualIncome = await movements
        .Where(m => m.MovementDate >= startOfYear && !m.IsNegative && !m.IsTransfer)
        .SumAsync(m => (decimal?)m.Amount) ?? 0;

    decimal annualExpense = await movements
        .Where(m => m.MovementDate >= startOfYear && m.IsNegative && !m.IsTransfer)
        .SumAsync(m => (decimal?)m.Amount) ?? 0;

    var lineBalances = await movements
        .GroupBy(m => new { m.AppAccountLineId, m.AppAccountLine.Name })
        .Select(g => new
        {
            g.Key.AppAccountLineId,
            LineName = g.Key.Name,
            Balance = g.Sum(m => m.IsNegative ? -m.Amount : m.Amount)
        })
        .ToListAsync();

    var allLines = account.Lines
        .Select(l =>
        {
            var found = lineBalances.FirstOrDefault(b => b.AppAccountLineId == l.Id);
            return new
            {
                AppAccountLineId = l.Id,
                LineName = l.Name,
                Balance = found?.Balance ?? 0m
            };
        })
        .ToList();

    decimal totalBalance = allLines.Sum(l => l.Balance);

    var latestMovements = await movements
        .OrderByDescending(m => m.MovementDate)
        .ThenByDescending(m => m.Id)
        .Take(6)
        .Select(m => new
        {
            m.Id,
            m.MovementDate,
            m.Amount,
            m.IsNegative,
            m.IsTransfer,
            m.Note,
            LineName = m.AppAccountLine.Name
        })
        .ToListAsync();

    var budgetUsagePercent = account.MonthlyBudget <= 0
        ? 0
        : Math.Clamp((double)(monthlySpend / account.MonthlyBudget * 100), 0, 999);

    var response = new
    {
        account = new
        {
            account.Id,
            account.Username,
            account.DisplayName,
            account.MonthlyBudget,
            account.AnnualSavingsGoal
        },
        metrics = new
        {
            balance = totalBalance,
            weeklySpend,
            monthlySpend,
            monthlyBudgetUsedPercent = budgetUsagePercent,
            annualSavings = annualIncome - annualExpense,
            annualGoal = account.AnnualSavingsGoal
        },
        lines = allLines,
        latestMovements
    };

    return Results.Ok(response);
});

app.MapGet("/api/catalog/categories", async (AppDbContext db) =>
{
    var categories = await db.Category
        .Include(c => c.SubCategories)
        .ToListAsync();

    var response = categories.Select(c => new
    {
        c.Id,
        c.Name,
        c.Kind,
        SubCategories = c.SubCategories.Select(sc => new { sc.Id, sc.Name, sc.Description })
    });

    return Results.Ok(response);
});

app.MapGet("/api/analytics/expenses/by-category", [Authorize] async (ClaimsPrincipal principal, AppDbContext db, DateOnly? start, DateOnly? end) =>
{
    var username = principal.Identity?.Name ?? "a";
    var account = await db.AppAccounts.FirstOrDefaultAsync(a => a.Username == username);
    if (account is null)
        return Results.NotFound(new { message = $"Nessun conto app trovato per l'utente {username}" });

    var movements = db.AppMovements
        .Include(m => m.SubCategory)
        .ThenInclude(sc => sc.Category)
        .Where(m => m.AppAccountId == account.Id && m.IsNegative && !m.IsTransfer);

    if (start.HasValue)
        movements = movements.Where(m => m.MovementDate >= start.Value);
    if (end.HasValue)
        movements = movements.Where(m => m.MovementDate <= end.Value);

    var grouped = await movements
        .Select(m => new
        {
            CategoryId = m.SubCategory != null ? m.SubCategory.CategoryId : (int?)null,
            CategoryName = m.SubCategory != null ? m.SubCategory.Category.Name : "Uncategorized",
            Amount = m.Amount
        })
        .GroupBy(x => new { x.CategoryId, x.CategoryName })
        .Select(g => new
        {
            g.Key.CategoryId,
            g.Key.CategoryName,
            Total = g.Sum(x => x.Amount)
        })
        .ToListAsync();

    var total = grouped.Sum(g => g.Total);
    var response = new
    {
        total,
        items = grouped.Select(g => new
        {
            categoryId = g.CategoryId,
            categoryName = g.CategoryName,
            total = g.Total,
            percent = total > 0 ? Math.Round((double)(g.Total / total * 100), 1) : 0
        })
    };

    return Results.Ok(response);
});

app.MapGet("/api/analytics/category-detail/{categoryId:int}", [Authorize] async (ClaimsPrincipal principal, AppDbContext db, int categoryId, DateOnly? start, DateOnly? end) =>
{
    var username = principal.Identity?.Name ?? "a";
    var account = await db.AppAccounts.FirstOrDefaultAsync(a => a.Username == username);
    if (account is null)
        return Results.NotFound(new { message = $"Nessun conto app trovato per l'utente {username}" });

    var category = await db.Category.FirstOrDefaultAsync(c => c.Id == categoryId);
    if (category is null)
        return Results.NotFound(new { message = "Categoria non trovata" });

    var movements = db.AppMovements
        .Include(m => m.SubCategory)
        .Include(m => m.AppAccountLine)
        .Where(m => m.AppAccountId == account.Id && m.IsNegative && !m.IsTransfer && m.SubCategory != null && m.SubCategory.CategoryId == categoryId);

    if (start.HasValue)
        movements = movements.Where(m => m.MovementDate >= start.Value);
    if (end.HasValue)
        movements = movements.Where(m => m.MovementDate <= end.Value);

    var subTotals = await movements
        .GroupBy(m => new { m.SubCategoryId, SubCategoryName = m.SubCategory!.Name })
        .Select(g => new
        {
            g.Key.SubCategoryId,
            g.Key.SubCategoryName,
            Total = g.Sum(m => m.Amount)
        })
        .ToListAsync();

    var movementList = await movements
        .OrderByDescending(m => m.MovementDate)
        .ThenByDescending(m => m.Id)
        .Select(m => new
        {
            m.Id,
            m.MovementDate,
            m.Amount,
            SubCategoryId = m.SubCategoryId,
            SubCategoryName = m.SubCategory != null ? m.SubCategory.Name : null,
            LineName = m.AppAccountLine.Name,
            m.Note
        })
        .ToListAsync();

    // Trend: ultimi 31 giorni mese corrente e precedente
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var startCurrentMonth = new DateOnly(today.Year, today.Month, 1);
    var startPrevMonth = startCurrentMonth.AddMonths(-1);
    var endPrevMonth = startCurrentMonth.AddDays(-1);

    var currentMonthData = await movements
        .Where(m => m.MovementDate >= startCurrentMonth && m.MovementDate <= today)
        .GroupBy(m => m.MovementDate.Day)
        .Select(g => new { Day = g.Key, Total = g.Sum(m => m.Amount) })
        .ToListAsync();

    var prevMonthData = await movements
        .Where(m => m.MovementDate >= startPrevMonth && m.MovementDate <= endPrevMonth)
        .GroupBy(m => m.MovementDate.Day)
        .Select(g => new { Day = g.Key, Total = g.Sum(m => m.Amount) })
        .ToListAsync();

    var response = new
    {
        category = new { category.Id, category.Name, category.Kind },
        subTotals,
        movements = movementList,
        trend = new
        {
            currentMonth = currentMonthData,
            previousMonth = prevMonthData
        }
    };

    return Results.Ok(response);
});

app.MapPost("/api/dashboard/movements", [Authorize] async (ClaimsPrincipal principal, AppDbContext db, [FromBody] NewMovementDto dto) =>
{
    var username = principal.Identity?.Name ?? "a";
    var account = await db.AppAccounts.FirstOrDefaultAsync(a => a.Username == username);
    if (account is null)
        return Results.NotFound(new { message = $"Nessun conto app trovato per l'utente {username}" });

    var sourceLine = await db.AppAccountLines.FirstOrDefaultAsync(l => l.Id == dto.AppAccountLineId && l.AppAccountId == account.Id);
    if (sourceLine is null)
        return Results.BadRequest(new { message = "Conto selezionato non valido per l'utente" });

    SubCategory? subCategory = null;
    if (!dto.IsTransfer && dto.SubCategoryId.HasValue)
    {
        subCategory = await db.SubCategories.FirstOrDefaultAsync(sc => sc.Id == dto.SubCategoryId.Value);
        if (subCategory is null)
            return Results.BadRequest(new { message = "Sottocategoria non valida" });
    }

    var now = DateTime.UtcNow;
    var amount = Math.Abs(dto.Amount);

    if (dto.IsTransfer)
    {
        if (dto.TargetAppAccountLineId is null)
            return Results.BadRequest(new { message = "Seleziona un conto di destinazione" });

        if (dto.TargetAppAccountLineId == dto.AppAccountLineId)
            return Results.BadRequest(new { message = "I conti di origine e destinazione devono essere diversi" });

        var targetLine = await db.AppAccountLines.FirstOrDefaultAsync(l => l.Id == dto.TargetAppAccountLineId && l.AppAccountId == account.Id);
        if (targetLine is null)
            return Results.BadRequest(new { message = "Conto di destinazione non valido per l'utente" });

        var movementOut = new AppMovement
        {
            AppAccountId = account.Id,
            AppAccountLineId = sourceLine.Id,
            Amount = amount,
            IsNegative = true,
            IsTransfer = true,
            MovementDate = dto.MovementDate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? "Giroconto uscita" : dto.Note,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = username,
            UpdatedBy = username
        };

        var movementIn = new AppMovement
        {
            AppAccountId = account.Id,
            AppAccountLineId = targetLine.Id,
            Amount = amount,
            IsNegative = false,
            IsTransfer = true,
            MovementDate = dto.MovementDate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? "Giroconto entrata" : dto.Note,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = username,
            UpdatedBy = username
        };

        db.AppMovements.AddRange(movementOut, movementIn);
        await db.SaveChangesAsync();

        return Results.Created($"/api/dashboard/movements/{movementOut.Id}", new { debitId = movementOut.Id, creditId = movementIn.Id });
    }
    else
    {
        var movement = new AppMovement
        {
            AppAccountId = account.Id,
            AppAccountLineId = sourceLine.Id,
            Amount = amount,
            IsNegative = dto.IsNegative,
            IsTransfer = false,
            MovementDate = dto.MovementDate,
            Note = dto.Note,
            SubCategory = subCategory,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = username,
            UpdatedBy = username
        };

        db.AppMovements.Add(movement);
        await db.SaveChangesAsync();

        return Results.Created($"/api/dashboard/movements/{movement.Id}", new { movement.Id });
    }
});

// API Protetta: Richiede il Token JWT
app.MapGet("/api/protected", [Authorize] () =>
{
    return Results.Json(new { message = "Accesso autorizzato!" });
});

app.Run();

record LoginRequest(string Username, string Password);
record NewMovementDto(decimal Amount, bool IsNegative, DateOnly MovementDate, int AppAccountLineId, int? TargetAppAccountLineId, bool IsTransfer, int? SubCategoryId, string? Note);

static class DashboardSeed
{
    public static async Task EnsureSeedData(AppDbContext db)
    {
        const string seedUsername = "a";
        if (await db.AppAccounts.AnyAsync(a => a.Username == seedUsername))
            return;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var account = new AppAccount
        {
            Username = seedUsername,
            DisplayName = "Admin",
            MonthlyBudget = 2500,
            AnnualSavingsGoal = 10000,
            CreatedAt = now,
            UpdatedAt = now
        };

        // categorie e sottocategorie base
        var catCasa = new Category { Name = "Home", Kind = "expense" };
        var catVita = new Category { Name = "Living", Kind = "expense" };
        var catEntrate = new Category { Name = "Income", Kind = "income" };

        var scBolletta = new SubCategory { Name = "Utilities", Description = "Bollette casa", Category = catCasa };
        var scSpesa = new SubCategory { Name = "Groceries", Description = "Spesa alimentare", Category = catVita };
        var scAffitto = new SubCategory { Name = "Rent", Description = "Affitto", Category = catCasa };
        var scStipendio = new SubCategory { Name = "Salary", Description = "Stipendio", Category = catEntrate };
        var scCashback = new SubCategory { Name = "Cashback", Description = "Cashback carta", Category = catEntrate };

        var contoCorrente = new AppAccountLine
        {
            Name = "Conto corrente",
            Institution = "Banca Solidale",
            Kind = "bank",
            CreatedAt = now,
            UpdatedAt = now,
            AppAccount = account
        };

        var cartaCredito = new AppAccountLine
        {
            Name = "Carta di credito",
            Institution = "Circuito Oro",
            Kind = "card",
            CreatedAt = now,
            UpdatedAt = now,
            AppAccount = account
        };

        var movements = new List<AppMovement>
        {
            new()
            {
                AppAccount = account,
                AppAccountLine = contoCorrente,
                Amount = 1800,
            IsNegative = false,
            IsTransfer = false,
            SubCategory = scStipendio,
            MovementDate = today.AddDays(-20),
            Note = "Stipendio mensile",
            CreatedAt = now,
            UpdatedAt = now
        },
            new()
            {
                AppAccount = account,
                AppAccountLine = contoCorrente,
                Amount = 320,
            IsNegative = true,
            IsTransfer = false,
            SubCategory = scSpesa,
            MovementDate = today.AddDays(-6),
            Note = "Spesa settimanale",
            CreatedAt = now,
            UpdatedAt = now
        },
            new()
            {
                AppAccount = account,
                AppAccountLine = contoCorrente,
                Amount = 85,
            IsNegative = true,
            IsTransfer = false,
            SubCategory = scBolletta,
            MovementDate = today.AddDays(-3),
            Note = "Bolletta luce",
            CreatedAt = now,
            UpdatedAt = now
        },
            new()
            {
                AppAccount = account,
                AppAccountLine = cartaCredito,
                Amount = 450,
            IsNegative = true,
            IsTransfer = false,
            SubCategory = scAffitto,
            MovementDate = today.AddDays(-2),
            Note = "Assicurazione auto",
            CreatedAt = now,
            UpdatedAt = now
        },
            new()
            {
                AppAccount = account,
                AppAccountLine = cartaCredito,
                Amount = 150,
            IsNegative = false,
            IsTransfer = false,
            SubCategory = scCashback,
            MovementDate = today.AddDays(-1),
            Note = "Cashback mensile",
            CreatedAt = now,
            UpdatedAt = now
        },
            new()
            {
                AppAccount = account,
                AppAccountLine = contoCorrente,
                Amount = 90,
            IsNegative = true,
            IsTransfer = false,
            MovementDate = today,
            Note = "Cena fuori",
            CreatedAt = now,
            UpdatedAt = now
            }
        };

        db.Category.AddRange(catCasa, catVita, catEntrate);
        db.SubCategories.AddRange(scBolletta, scSpesa, scAffitto, scStipendio, scCashback);
        db.AppAccounts.Add(account);
        db.AppAccountLines.AddRange(contoCorrente, cartaCredito);
        db.AppMovements.AddRange(movements);

        await db.SaveChangesAsync();
    }
}
