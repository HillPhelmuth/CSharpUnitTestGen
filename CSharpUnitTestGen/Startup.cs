using ChatComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestGenCore;

namespace CSharpUnitTestGen;

public class Startup
{
    public static IServiceProvider? Services { get; private set; }

    public static void Init()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(WireupServices)
            .ConfigureHostConfiguration(builder =>
            {
                builder.AddUserSecrets<Startup>();
            })
            .Build();
        Services = host.Services;
    }

    private static void WireupServices(IServiceCollection services)
    {
        services.AddWindowsFormsBlazorWebView();
        services.AddScoped<UnitTestGeneratorService>();
        services.AddChat();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
    }
}