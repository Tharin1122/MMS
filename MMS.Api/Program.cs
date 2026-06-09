using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Auth;
using MMS.Infrastructure.Persistence.Interceptors;
using MMS.Infrastructure.Persistence.Services;
using Hangfire;
using Hangfire.PostgreSql;
using MMS.Api.Hubs;
using MMS.Api.Services;

// PostgreSQL: รองรับ DateTime ที่มี Kind=Unspecified/Local (เลี่ยง error timestamptz)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Convert postgresql:// URL → Npgsql key-value format if needed
static string NormalizeConnectionString(string? cs)
{
    if (string.IsNullOrEmpty(cs)) return cs ?? "";
    if (!cs.StartsWith("postgresql://") && !cs.StartsWith("postgres://")) return cs;
    var uri = new Uri(cs);
    var userInfo = uri.UserInfo.Split(':');
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var db = uri.AbsolutePath.TrimStart('/');
    var sslMode = "Require";
    if (uri.Query.Contains("sslmode=disable")) sslMode = "Disable";
    else if (uri.Query.Contains("sslmode=prefer")) sslMode = "Prefer";
    return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode={sslMode};Trust Server Certificate=true";
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();

// Swagger + JWT Auth Header
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// DbContext — PostgreSQL
var connectionString = NormalizeConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL"));

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.CommandTimeout(60);
        npgsql.EnableRetryOnFailure(3);
    });
    var interceptor = sp.GetService<AuditInterceptor>();
    if (interceptor != null) options.AddInterceptors(interceptor);
});

// JWT
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Hangfire — PostgreSQL
builder.Services.AddHangfire(c => c
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();
builder.Services.AddHttpContextAccessor();

// Services
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<NotificationSenderService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();
builder.Services.AddScoped<ActivityTimelineService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<WalkInService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<RoomCleaningService>();

builder.Services.AddHostedService<CleaningCheckBackgroundService>();
builder.Services.AddSignalR();
builder.Services.AddHttpClient<NotificationSenderService>();
builder.Services.AddHttpClient<LineService>();

// CORS — รองรับทั้ง local dev และ production (Vercel)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Auto-migrate on startup (production)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migration completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed — app will continue without migration.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AppPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

app.MapHub<MmsHub>("/hubs/mms");

try
{
    var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<NotificationSenderService>(
        "send-notifications",
        svc => svc.ProcessPendingAsync(),
        Cron.Minutely);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Hangfire recurring job registration failed — skipping.");
}

app.Run();
