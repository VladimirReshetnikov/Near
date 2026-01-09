using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Near.Infrastructure;
using Near.Services;
using Near.UI;

namespace Near.App;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var initialDirectory = Environment.CurrentDirectory;

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddNearServices(initialDirectory);
                services.AddNearInfrastructure();
                services.AddNearUi();
            })
            .Build();

        await host.StartAsync();

        var appHost = host.Services.GetRequiredService<IAppHost>();
        await appHost.RunAsync(default);

        await host.StopAsync();
    }
}
