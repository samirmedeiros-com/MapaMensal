using MapaMensal.Data;
using MapaMensal.Helpers;
using MapaMensal.Models;
using MapaMensal.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Oracle.ManagedDataAccess.Client;
using System.Text;

// Configurar Oracle Wallet (deve ser antes de qualquer ligação)
// A pasta Oracle é copiada para o directório do executável pelo .csproj
string walletPath = Path.Combine(AppContext.BaseDirectory, "Oracle");
OracleConfiguration.WalletLocation = walletPath;
OracleConfiguration.TnsAdmin = walletPath;

// Azure App Service define PORT; localmente usa 5016 via launchSettings
var port = Environment.GetEnvironmentVariable("PORT") ?? "5016";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseOracle(connectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddTransient<MapaMensal.Helpers.MacOsCurlHandler>();
builder.Services.AddHttpClient("simplysend", c => c.Timeout = TimeSpan.FromSeconds(30))
    .AddHttpMessageHandler<MapaMensal.Helpers.MacOsCurlHandler>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Migration falhou no startup: {Msg} | Inner: {Inner}",
            ex.Message, ex.InnerException?.Message);
        throw;
    }

    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            Email = "admin@mapaemsal.pt",
            PasswordHash = PasswordHelper.Hash("Admin123!"),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
