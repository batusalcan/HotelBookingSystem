using HotelService.Cache;
using HotelService.Data;
using HotelService.Health;
using HotelService.Messaging;
using HotelService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.CircuitBreaker;
using Serilog;
using Serilog.Context;
using SharedKernel.Exceptions;
using SharedKernel.Models;
using StackExchange.Redis;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ── DbContexts ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BookingDb")));

// ── Redis (Singleton) ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connStr = builder.Configuration.GetConnectionString("Redis")!;
    var options = ConfigurationOptions.Parse(connStr);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// ── RabbitMQ (Singleton — one connection pool per instance) ───────────────────
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IHotelSearchService, HotelSearchService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// ── Polly resilience pipeline for SQL calls ───────────────────────────────────
var sqlPipeline = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(30),
        FailureRatio = 0.5,
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30)
    })
    .AddTimeout(TimeSpan.FromSeconds(15))
    .Build();
builder.Services.AddKeyedSingleton("sql", sqlPipeline);

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization();

// ── Swagger / API Explorer ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Hotel Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

// ── Health checks — real dependency verification ──────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("catalog-db",
        new PostgreSqlHealthCheck(builder.Configuration.GetConnectionString("CatalogDb")!),
        HealthStatus.Unhealthy, ["db", "sql"])
    .AddCheck("booking-db",
        new PostgreSqlHealthCheck(builder.Configuration.GetConnectionString("BookingDb")!),
        HealthStatus.Unhealthy, ["db", "sql"])
    .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, ["cache"]);

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Correlation ID middleware — must come BEFORE UseSerilogRequestLogging so that
//    the request log entry is written while CorrelationId is still in LogContext ──
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

app.UseSerilogRequestLogging();

// ── Global exception handler ──────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BrokenCircuitException)
    {
        context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<string>.Fail("Database is temporarily unavailable. Please try again later."));
    }
    catch (AppException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<string>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<string>.Fail("An unexpected error occurred."));
    }
});

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Service v1"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
    }
});

app.Run();

public partial class Program { }
