using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Larp.Web;

public static class LarpWeb
{
    public const string TokenHeader = "X-Larp-Token";

    /// <summary>Build (not start) the local MVC backend: API, event stream, screenshot backdrop and the two React host pages.</summary>
    public static WebApplication Create(LarpWebOptions o, Action<ILoggingBuilder>? logging = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = o.WebRoot,
        });

        builder.Logging.ClearProviders();
        logging?.Invoke(builder.Logging);

        // Loopback only: this server can launch programs, it must never be reachable from the network.
        builder.WebHost.UseUrls($"http://127.0.0.1:{o.Port}");

        builder.Services.AddSingleton(o);
        builder.Services.AddSingleton(o.Store);
        builder.Services.AddSingleton(o.Desktop);
        builder.Services.AddSingleton<EventBus>();
        builder.Services.AddSingleton<ShotStore>();
        // nothing here uses cookies/antiforgery, so never persist data-protection keys to disk
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(LarpWeb).Assembly);

        var app = builder.Build();
        app.UseMiddleware<LocalOnlyMiddleware>();
        Directory.CreateDirectory(Path.Combine(o.WebRoot, "ui"));
        app.UseStaticFiles();
        app.MapControllers();
        return app;
    }

    /// <summary>The loopback port Kestrel actually bound to (call after Start).</summary>
    public static int PortOf(WebApplication app) =>
        new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

    public static string PageUrl(WebApplication app, string page, string token) =>
        $"http://127.0.0.1:{PortOf(app)}/{page}?t={token}";
}

/// <summary>
/// Defence for a localhost server that can start programs:
/// (1) the Host header must be 127.0.0.1 — blocks DNS-rebinding from a web page;
/// (2) every non-static request needs the per-launch secret token — blocks any other local page or process.
/// </summary>
public sealed class LocalOnlyMiddleware(RequestDelegate next, LarpWebOptions options)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Host.Host.Equals("127.0.0.1", StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!IsPublicAsset(ctx.Request.Path))
        {
            string? supplied = ctx.Request.Headers[LarpWeb.TokenHeader];
            if (string.IsNullOrEmpty(supplied)) supplied = ctx.Request.Query["t"];
            if (!TokenOk(supplied))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next(ctx);
    }

    private static bool IsPublicAsset(PathString p) => p.StartsWithSegments("/ui");

    private bool TokenOk(string? supplied) =>
        supplied is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(options.Token));
}
