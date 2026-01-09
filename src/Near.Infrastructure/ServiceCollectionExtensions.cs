using Microsoft.Extensions.DependencyInjection;
using Near.Infrastructure.GitCli;
using Near.Services.Git;

namespace Near.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNearInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();

        return services;
    }
}
