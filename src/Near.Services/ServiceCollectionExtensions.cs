using Microsoft.Extensions.DependencyInjection;
using Near.Core.State;
using Near.Services.State;
using Near.Services.Tasks;

namespace Near.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNearServices(this IServiceCollection services, string initialDirectory)
    {
        services.AddSingleton<IStateStore<AppState>>(_ =>
            new StateStore<AppState>(AppState.CreateDefault(initialDirectory), AppState.Reduce));
        services.AddSingleton<ITaskRunner>(_ => new TaskRunner());

        return services;
    }
}
