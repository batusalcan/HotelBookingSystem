using CommentsService.Data;
using CommentsService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Driver;
using Serilog;
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

builder.Services.AddHealthChecks();

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

// Global exception handler
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (AppException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        ctx.Response.ContentType = "application/json";
        var body = System.Text.Json.JsonSerializer.Serialize(ApiResponse<object>.Fail(ex.Message));
        await ctx.Response.WriteAsync(body);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception");
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var body = System.Text.Json.JsonSerializer.Serialize(ApiResponse<object>.Fail("An unexpected error occurred."));
        await ctx.Response.WriteAsync(body);
    }
});

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Comments Service v1"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
