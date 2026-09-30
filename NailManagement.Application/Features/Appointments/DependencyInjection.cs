using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Features.Appointments.UseCases;

namespace NailManagement.Application.Features.Appointments;

public static class AppointmentsFeatureRegistration
{
    public static IServiceCollection AddAppointmentsFeature(this IServiceCollection services)
    {
        services.AddScoped<AppointmentBookingGuard>();
        services.AddScoped<AppointmentReadService>();
        services.AddScoped<ListAppointmentsUseCase>();
        services.AddScoped<GetAppointmentUseCase>();
        services.AddScoped<CreateAppointmentUseCase>();
        services.AddScoped<UpdateAppointmentUseCase>();
        services.AddScoped<RescheduleAppointmentUseCase>();
        services.AddScoped<ChangeAppointmentStatusUseCase>();

        return services;
    }
}
