using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Data;
using NotificationService.Health;
using NotificationService.HttpClients;
using NotificationService.Jobs;
using NotificationService.Messaging;
using NotificationService.Services;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Typed HttpClient for HotelService internal capacity-report endpoint
builder.Services.AddHttpClient<IHotelServiceClient, HotelServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HotelService:BaseUrl"] ?? "http://localhost:5001");
})
.AddStandardResilienceHandler();

// Notifications DB
builder.Services.AddDbContext<NotificationsDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));

// Factory Method Pattern — concrete notification types registered as transient
builder.Services.AddTransient<BookingConfirmationNotification>();
builder.Services.AddTransient<LowCapacityAlertNotification>();
builder.Services.AddTransient<INotificationFactory, NotificationFactory>();

// Cron job and service layer
builder.Services.AddTransient<CapacityAlertJob>();
builder.Services.AddTransient<INotificationService, CapacityNotificationService>();

// Queue consumer — always-on IHostedService (BP-07)
builder.Services.AddSingleton<RabbitMqConsumer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqConsumer>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Notification Service", Version = "v1" }));

// ── Health checks — RabbitMQ connectivity ─────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>(
        name: "rabbitmq",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["messaging"]);

var app = builder.Build();

// Create NotificationAlerts table if it doesn't exist (shared Supabase DB — EnsureCreated won't work)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var notifDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await notifDb.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""NotificationAlerts"" (
                ""NotificationId"" uuid NOT NULL DEFAULT gen_random_uuid(),
                ""HotelId"" uuid NOT NULL,
                ""HotelName"" character varying(200) NOT NULL,
                ""RoomTypeName"" character varying(100) NOT NULL,
                ""AvailableCount"" integer NOT NULL,
                ""TotalCount"" integer NOT NULL,
                ""CapacityRatio"" double precision NOT NULL,
                ""StartDate"" date NOT NULL,
                ""EndDate"" date NOT NULL,
                ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
                ""IsRead"" boolean NOT NULL DEFAULT false,
                CONSTRAINT ""PK_NotificationAlerts"" PRIMARY KEY (""NotificationId"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_NotificationAlerts_CreatedAt"" ON ""NotificationAlerts"" (""CreatedAt"");
        ");
    }
    catch (Exception ex)
    {
        var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogWarning(ex, "Could not ensure NotificationAlerts table on startup — DB may not be available yet");
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
    try { await next(); }
    catch (SharedKernel.Exceptions.AppException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(
            SharedKernel.Models.ApiResponse<object>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception");
        await ctx.Response.WriteAsJsonAsync(
            SharedKernel.Models.ApiResponse<object>.Fail("An unexpected error occurred."));
    }
});

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1"));

app.UseHttpsRedirection();
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
