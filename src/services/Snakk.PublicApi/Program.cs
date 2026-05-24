using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Snakk.ServiceDefaults;
using Snakk.Protos.Discussion;
using Snakk.PublicApi.Endpoints;
using Snakk.PublicApi.Services;
using System.Security.Claims;
using System.Threading.RateLimiting;

// Allow gRPC (HTTP/2) over plain HTTP — needed in Docker where services communicate without TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

ThreadPool.SetMinThreads(50, 50);

var builder = WebApplication.CreateBuilder(args);

builder.AddSnakkDefaults();

// JWT Bearer — validates tokens issued by Snakk.Auth via OIDC discovery + JWKS (no network call per request)
var authBaseUrl = builder.Configuration["Auth:BaseUrl"]
    ?? throw new InvalidOperationException("Auth:BaseUrl not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authBaseUrl;
        options.RequireHttpsMetadata = false;
        options.Audience = "snakk-public-api";
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var denyList = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenDenyListService>();
                var jti = context.Principal?.FindFirstValue("jti");
                if (jti is not null && denyList.IsRevoked(jti))
                    context.Fail("Token has been revoked");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Distributed cache: Valkey on production, in-memory fallback for development
var valkeyConn = builder.Configuration["Valkey:ConnectionString"];
if (!string.IsNullOrEmpty(valkeyConn))
    builder.Services.AddStackExchangeRedisCache(opts => { opts.Configuration = valkeyConn; opts.InstanceName = "snakk:"; });
else
    builder.Services.AddDistributedMemoryCache();

// Token revocation deny list — IDistributedCache backed, shared with Snakk.Api
builder.Services.AddSingleton<ITokenDenyListService, TokenDenyListService>();
builder.Services.AddHttpContextAccessor();

// gRPC channel to internal Snakk.Api
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17101";
var grpcHandler = new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
};
var grpcOptions = new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = grpcHandler };

if (apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
{
    grpcOptions.HttpVersion = new Version(2, 0);
    grpcOptions.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
}

builder.Services.AddSingleton(_ => Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, grpcOptions));
builder.Services.AddSingleton<GrpcRequestInterceptor>();
builder.Services.AddSingleton(sp =>
{
    var channel = sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>();
    var interceptor = sp.GetRequiredService<GrpcRequestInterceptor>();
    return new DiscussionService.DiscussionServiceClient(
        channel.CreateCallInvoker().Intercept(interceptor));
});

// Rate limiting: 100 req/min per client IP
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("public-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

if (!builder.Configuration.GetValue<bool>("DisableRateLimiting"))
    app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

DiscussionEndpoints.Map(app);

app.Run();
