using Microsoft.Extensions.DependencyInjection;

namespace PetroTrans.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
