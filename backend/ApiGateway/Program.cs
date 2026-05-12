using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

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

builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddSwaggerForOcelot(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Unwrap X-Forwarded-For so RemoteIpAddress reflects the real client IP
app.UseForwardedHeaders();

app.UseSerilogRequestLogging();

// Copy real client IP into X-Client-IP — Ocelot rate limiter reads this header
app.Use((context, next) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    context.Request.Headers["X-Client-IP"] = clientIp;
    return next();
});

// Authentication and authorization must run before Ocelot so that routes
// with AuthenticationOptions can call context.AuthenticateAsync("Bearer").
app.UseAuthentication();
app.UseAuthorization();

// Aggregated Swagger UI at /swagger/index.html — merges all downstream service docs
app.UseSwaggerForOcelotUI(opt =>
{
    opt.PathToSwaggerGenerator = "/swagger/docs";
});

app.MapHealthChecks("/health");

await app.UseOcelot();

app.Run();
