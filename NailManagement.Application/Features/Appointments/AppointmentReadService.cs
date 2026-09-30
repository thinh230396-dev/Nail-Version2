using NailManagement.Application.Common;
using NailManagement.Domain.Salon.Appointments;

namespace NailManagement.Application.Features.Appointments;

/// <summary>
/// Dựng <see cref="AppointmentDto"/> từ lịch hẹn: đọc danh bạ khách và kỹ thuật viên, rồi ghép
/// lại qua mapper. Cùng khuôn với <c>SalesInvoiceReadService</c>.
/// </summary>
public sealed class AppointmentReadService(ISalonDirectoryReader directory)
{
    public async Task<AppointmentDto> DescribeAsync(
        Appointment appointment, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var names = await directory.ForAppointmentsAsync([appointment], cancellationToken);

        return AppointmentMapper.ToDto(appointment, names, now);
    }

    /// <param name="now">
    /// Một mốc cho cả danh sách, để mọi dòng được so nhãn quá hạn với cùng một thời điểm.
    /// </param>
    public async Task<IReadOnlyList<AppointmentDto>> DescribeManyAsync(
        IReadOnlyList<Appointment> appointments, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (appointments.Count == 0) return [];

        var names = await directory.ForAppointmentsAsync(appointments, cancellationToken);

        return [.. appointments.Select(appointment => AppointmentMapper.ToDto(appointment, names, now))];
    }
}
