using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Features.Appointments;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Appointments.UseCases;

/// <summary>
/// Bảng lịch hẹn của tiệm đang làm việc trong một khoảng ngày.
/// <para>
/// <b>Là endpoint đọc đầu tiên có khoảng ngày</b>, khác bốn module trước vốn trả trọn danh
/// sách rồi để trình duyệt lọc (quyết định 48). Lý do khác: danh bạ khách, bảng giá dịch vụ
/// và danh sách nhân viên của một tiệm đều có trần tự nhiên, còn lịch hẹn thì cộng dồn mãi —
/// sau nửa năm là hàng nghìn dòng cho một màn hình chỉ bao giờ hiện một ngày.
/// </para>
/// <para>
/// Phạm vi chi nhánh giống <c>ListStaffUseCase</c>: chủ tiệm xem cả tiệm, lễ tân chỉ xem chi
/// nhánh mình (BR-APT-002), và chi nhánh lấy từ phiên đăng nhập chứ không từ chuỗi truy vấn.
/// </para>
/// </summary>
public sealed class ListAppointmentsUseCase(
    IAppointmentRepository appointments,
    AppointmentReadService reader,
    IClock clock)
{
    /// <summary>
    /// Trần độ dài khoảng ngày. Không có nó thì <c>?from=2000-01-01&amp;to=2100-01-01</c> đưa
    /// nguyên vấn đề "trả toàn bộ" quay lại qua cửa chuỗi truy vấn.
    /// <para>
    /// Chọn 92 ngày vì đó là quý dài nhất — đủ cho mọi cách xem lịch mà giao diện có, và vẫn
    /// là một con số giải thích được khi bị hỏi.
    /// </para>
    /// </summary>
    private const int MaxRangeDays = 92;

    /// <param name="from">
    /// Rỗng thì lấy 0 giờ hôm nay theo giờ tiệm (<see cref="SalonTime"/>). Lịch hẹn được chọn theo
    /// <b>giờ bắt đầu</b> nằm trong khoảng, không theo giờ kết thúc: một buổi làm kéo sang quá nửa
    /// đêm vẫn thuộc về ngày mà khách đến.
    /// </param>
    /// <param name="to">Rỗng thì lấy hết ngày của <paramref name="from"/>.</param>
    public async Task<IReadOnlyList<AppointmentDto>> ExecuteAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var start = from ?? SalonTime.StartOfToday(now);
        var end = to ?? start.AddDays(1);

        if (end < start)
            throw DomainException.ForField("to", "Ngày kết thúc phải sau ngày bắt đầu.");

        if ((end - start).TotalDays > MaxRangeDays)
        {
            throw DomainException.ForField(
                "to", $"Khoảng ngày không được dài quá {MaxRangeDays} ngày. Hãy xem theo từng tháng.");
        }

        var branchId = BranchScope.Resolve(actor, "lịch hẹn");
        var found = await appointments.ListAsync(start, end, branchId, cancellationToken);

        // Cùng một mốc "bây giờ" cho cả danh sách, để nhãn quá hạn không thể đúng ở dòng này
        // và sai ở dòng kia chỉ vì câu lệnh chạy vắt qua một phút.
        return await reader.DescribeManyAsync(found, now, cancellationToken);
    }

}
