using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.UseCases.Auth;

namespace NailManagement.Application;

/// <summary>
/// Đăng ký các use case.
/// <para>
/// Tầng Application chỉ khai báo use case; nó không đăng ký bất kỳ bản cài đặt hạ tầng nào —
/// đó là việc của <c>NailManagement.Infrastructure.DependencyInjection</c>.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginUseCase>();
        services.AddScoped<GetCurrentAccountUseCase>();
        services.AddScoped<LogoutUseCase>();

        return services;
    }
}
