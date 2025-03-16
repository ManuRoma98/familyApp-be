using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// API Protetta: Richiede il Token JWT
app.MapGet("/api/protected", [Authorize] () =>
{
    return Results.Json(new { message = "Accesso autorizzato!" });
});

app.Run();

record LoginRequest(string Username, string Password);