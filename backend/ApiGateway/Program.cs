using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Load Ocelot route configuration
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Trust X-Forwarded-For headers from any upstream proxy (Azure LB, nginx, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// JWT Bearer — gateway validates tokens before forwarding to downstream services.
// The authentication provider key "Bearer" is referenced by AuthenticationOptions in ocelot.json.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Unwrap X-Forwarded-For so RemoteIpAddress reflects the real client IP
app.UseForwardedHeaders();

// Copy real client IP into X-Client-IP — Ocelot rate limiter reads this header
app.Use((context, next) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    context.Request.Headers["X-Client-IP"] = clientIp;
    return next();
});

// Correlation ID — generate if missing, forward to downstream services via request header.
// Must come BEFORE UseSerilogRequestLogging so the request log line includes CorrelationId.
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Correlation-Id"] = correlationId;
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

app.UseSerilogRequestLogging();
app.UseCors();

// Authentication and authorization must run before Ocelot so that routes
// with AuthenticationOptions can call context.AuthenticateAsync("Bearer").
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI aggregates all downstream service docs via direct URLs
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint(
        "https://hotel-hotelservice-aje8f7f7dqb5f0a5.italynorth-01.azurewebsites.net/swagger/v1/swagger.json",
        "Hotel Service v1");
    c.SwaggerEndpoint(
        "https://hotel-comments-c9ejhwftbch5eqey.italynorth-01.azurewebsites.net/swagger/v1/swagger.json",
        "Comments Service v1");
    c.SwaggerEndpoint(
        "https://hotel-aiagent-g2avhjfcfyhqcsfd.italynorth-01.azurewebsites.net/swagger/v1/swagger.json",
        "AI Agent Service v1");
    c.RoutePrefix = "swagger";
});

// Health check must be handled before Ocelot intercepts the request
app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/health"), healthApp =>
{
    healthApp.UseRouting();
    healthApp.UseEndpoints(endpoints =>
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
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
    });
});

await app.UseOcelot();

app.Run();
