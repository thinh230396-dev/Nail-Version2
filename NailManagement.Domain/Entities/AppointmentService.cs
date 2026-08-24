using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities;

/// <summary>
/// Một dịch vụ trong lịch hẹn — BR-APT-003, mỗi lịch hẹn bắt buộc có ít nhất một dòng.
/// <para>
/// Dòng này chép lại tên, thời lượng và thời gian dọn dẹp của dịch vụ tại thời điểm đặt
/// lịch. Nếu chỉ giữ khóa ngoại rồi tra ngược sang bảng dịch vụ, thì hôm nào chủ tiệm sửa
/// thời lượng của một dịch vụ, toàn bộ lịch hẹn cũ sẽ tự đổi giờ kết thúc và phép chống
/// trùng lịch ở BR-APT-011 sẽ cho ra kết quả khác với lúc đặt.
/// </para>
/// <para>
/// Cố ý KHÔNG chép giá vào đây: BR-SVC-007 quy định lịch hẹn chưa hoàn tất lấy giá HIỆN
/// TẠI của dịch vụ khi lập hóa đơn, và giá chỉ được chốt trên dòng hóa đơn (BR-SVC-006).
/// </para>
/// </summary>
public class AppointmentService : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private AppointmentService()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        AppointmentId = string.Empty;
        ServiceId = string.Empty;
        ServiceName = string.Empty;
    }

    private AppointmentService(
        string id,
        string tenantId,
        string appointmentId,
        string serviceId,
        string serviceName,
        int durationMinutes,
        int bufferMinutes)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        ServiceId = serviceId;
        ServiceName = serviceName;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string AppointmentId { get; private set; }
    public string ServiceId { get; private set; }

    /// <summary>BR-DEL-003 — dịch vụ đã ngừng bán vẫn phải hiện đúng tên trong lịch hẹn cũ.</summary>
    public string ServiceName { get; private set; }

    public int DurationMinutes { get; private set; }
    public int BufferMinutes { get; private set; }

    public Appointment? Appointment { get; private set; }
    public Service? Service { get; private set; }

    public static AppointmentService Create(
        string id,
        string tenantId,
        string appointmentId,
        string serviceId,
        string serviceName,
        int durationMinutes,
        int bufferMinutes)
        => new(
            Guard.Reference(id, "id", "Mã dòng dịch vụ"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(appointmentId, "appointmentId", "Lịch hẹn"),
            Guard.Reference(serviceId, "serviceId", "Dịch vụ"),
            Guard.NotEmpty(serviceName, "serviceName", "Tên dịch vụ", ValidationPolicy.NameMaxLength),
            Guard.Between(durationMinutes,
                ValidationPolicy.ServiceMinDurationMinutes,
                ValidationPolicy.ServiceMaxDurationMinutes,
                "durationMinutes", "Thời lượng dịch vụ (phút)"),
            Guard.Between(bufferMinutes, 0, ValidationPolicy.ServiceMaxBufferMinutes,
                "bufferMinutes", "Thời gian dọn dẹp (phút)"));
}
