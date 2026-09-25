using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Customers.UseCases;

namespace NailManagement.Application.Features.Customers;

public static class CustomersFeatureRegistration
{
    public static IServiceCollection AddCustomersFeature(this IServiceCollection services)
    {
        services.AddScoped<ListCustomersUseCase>();
        services.AddScoped<GetCustomerUseCase>();
        services.AddScoped<CreateCustomerUseCase>();
        services.AddScoped<UpdateCustomerUseCase>();
        services.AddScoped<ChangeCustomerStatusUseCase>();

        return services;
    }
}
