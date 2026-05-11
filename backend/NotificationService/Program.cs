using NotificationService.HttpClients;
using NotificationService.Jobs;
using NotificationService.Messaging;
using NotificationService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Typed HttpClient for HotelService internal capacity-report endpoint
builder.Services.AddHttpClient<IHotelServiceClient, HotelServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HotelService:BaseUrl"] ?? "http://localhost:5001");
});

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

builder.Services.AddHealthChecks();

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1"));
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
