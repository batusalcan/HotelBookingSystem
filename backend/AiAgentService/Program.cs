using AiAgentService.Facade;
using AiAgentService.Providers;
using AiAgentService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;
using SharedKernel.Exceptions;
using SharedKernel.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ── AI Provider (swap here to change LLM — zero business logic change required) ──
builder.Services.AddHttpClient<IAiProvider, GeminiAiProvider>();

// ── Hotel System Facade (typed HttpClient → HotelService) ────────────────────
builder.Services.AddHttpClient<IHotelSystemFacade, HotelSystemFacade>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:HotelService"] ?? "http://localhost:5001");
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAiChatService, AiChatService>();

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AI Agent Service", Version = "v1" });
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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();

// ── Global exception handler ──────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (AppException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception");
        await ctx.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("An unexpected error occurred."));
    }
});

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Agent Service v1"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
