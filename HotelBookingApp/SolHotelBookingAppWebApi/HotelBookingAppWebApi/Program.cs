using AspNetCoreRateLimit;
using Azure.Identity;
using HotelBookingAppWebApi.Contexts;
using HotelBookingAppWebApi.Exceptions.Middleware;
using HotelBookingAppWebApi.Interfaces;
using HotelBookingAppWebApi.Interfaces.RepositoryInterface;
using HotelBookingAppWebApi.Interfaces.UnitOfWorkInterface;
using HotelBookingAppWebApi.Repository;
using HotelBookingAppWebApi.Services;
using HotelBookingAppWebApi.Services.BackgroundServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── KEY VAULT (Production only — requires Managed Identity + KeyVaultUri app setting)
// If KeyVaultUri is set, load secrets from Key Vault. Otherwise fall back to environment variables.
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = builder.Configuration["KeyVaultUri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }
}

// ── CONTROLLERS ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── RATE LIMITING ──────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ── SWAGGER + JWT ──────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel Booking API",
        Version = "v1",
        Description = "Complete Hotel Booking System — Guest, Admin, SuperAdmin roles"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── DATABASE ───────────────────────────────────────────────────────────────────
var connectionStringName = builder.Environment.IsDevelopment() ? "Developer" : "Production";
builder.Services.AddDbContext<HotelBookingContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(connectionStringName),
        sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    ));

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            var frontendUrl = builder.Configuration["FrontendUrl"]
                ?? "https://stayhub-frontend-bbash3frhnfnaxd6.westus3-01.azurewebsites.net";
            policy.WithOrigins(frontendUrl).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

// ── GENERIC REPOSITORY ─────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

// ── UNIT OF WORK ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── APPLICATION SERVICES ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<IRoomTypeService, RoomTypeService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAmenityService, AmenityService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IPromoCodeService, PromoCodeService>();
builder.Services.AddScoped<IAmenityRequestService, AmenityRequestService>();
builder.Services.AddScoped<ISuperAdminRevenueService, SuperAdminRevenueService>();
builder.Services.AddScoped<ISupportRequestService, SupportRequestService>();
// BlobStorage: Uncomment when storage account is ready
// builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddHttpClient<IChatbotService, ChatbotService>();

// ── BACKGROUND SERVICES ────────────────────────────────────────────────────────
builder.Services.AddHostedService<ReservationCleanupService>();
builder.Services.AddHostedService<HotelDeactivationRefundService>();
builder.Services.AddHostedService<NoShowAutoCancelService>();

// ── JWT AUTHENTICATION ─────────────────────────────────────────────────────────
// Reads from: Key Vault secret "Keys--Jwt", OR app setting "Keys__Jwt", OR appsettings.json "Keys:Jwt"
string jwtKey = builder.Configuration["Keys--Jwt"]
    ?? builder.Configuration["Keys__Jwt"]
    ?? builder.Configuration["Keys:Jwt"]
    ?? throw new InvalidOperationException("JWT Key not found in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "unique_name"
        };
    });

builder.Services.AddAuthorization();

// ── BUILD ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseIpRateLimiting();
app.UseRouting();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
