using Microsoft.Extensions.DependencyInjection;

namespace Near.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNearUi(this IServiceCollection services)
    {
        services.AddSingleton<IAppHost, TerminalGuiAppHost>();

        return services;
    }
}
