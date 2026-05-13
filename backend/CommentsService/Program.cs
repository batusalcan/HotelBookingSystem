using CommentsService.Data;
using CommentsService.Health;
using CommentsService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Polly;
using Polly.CircuitBreaker;
using Serilog;
using Serilog.Context;
using SharedKernel.Exceptions;
using SharedKernel.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// MongoDB — Singleton client, Scoped context
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(config.GetConnectionString("MongoDB")));
builder.Services.AddScoped<MongoDbContext>();

// Application services
builder.Services.AddScoped<IHotelCommentsService, HotelCommentsService>();

// ── Polly resilience pipeline for MongoDB calls ───────────────────────────────
var mongoPipeline = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(30),
        FailureRatio = 0.5,
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30)
    })
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();
builder.Services.AddKeyedSingleton("mongo", mongoPipeline);

// JWT Bearer (same IAM as HotelService)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = config["Jwt:Authority"];
        o.Audience = config["Jwt:Audience"];
        o.TokenValidationParameters.NameClaimType = "sub";
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Hotel Comments Service", Version = "v1" }));

// ── Health checks — real dependency verification ──────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", HealthStatus.Unhealthy, ["db", "nosql"]);

var app = builder.Build();

// Seed MongoDB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    try
    {
        await CommentsSeeder.SeedAsync(db);
        Log.Information("CommentsSeeder completed");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "CommentsSeeder failed — MongoDB may be unavailable; continuing startup");
    }
}

// ── Correlation ID middleware — must come BEFORE UseSerilogRequestLogging ────────
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

app.UseSerilogRequestLogging();

// Global exception handler
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (BrokenCircuitException)
    {
        ctx.Response.StatusCode = 503;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Database is temporarily unavailable. Please try again later."));
    }
    catch (AppException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception");
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("An unexpected error occurred."));
    }
});

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Comments Service v1"));

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
