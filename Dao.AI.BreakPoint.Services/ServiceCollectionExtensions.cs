using Dao.AI.BreakPoint.Services.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Dao.AI.BreakPoint.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBreakPointServices(this IServiceCollection services)
    {
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        return services;
    }

    public static IServiceCollection AddBreakPointIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        return services;
    }
}
