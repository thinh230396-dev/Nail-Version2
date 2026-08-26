using NailManagement.Application.DTOs;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Application.Mappings;

/// <summary>Chuyển entity <see cref="Service"/> sang DTO gửi ra ngoài, và ngược lại cho trạng thái.</summary>
public static class ServiceMapper
{
    /// <summary>
    /// Hai chuỗi trạng thái dịch vụ. BR-SVC-004 nói rõ chỉ có hai, nên bảng ánh xạ này
    /// không cần nhánh mặc định nào ngoài lỗi.
    /// </summary>
    public static string ToWireFormat(ServiceStatus status) => status switch
    {
        ServiceStatus.Active => "ACTIVE",
        ServiceStatus.Inactive => "INACTIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái dịch vụ không hợp lệ.")
    };

    /// <summary>
    /// Đọc ngược chuỗi từ client. Đặt ở mapper chứ không viết lại trong use case, để chiều
    /// đi và chiều về không thể lệch nhau sau một lần ai đó sửa một bên.
    /// </summary>
    public static ServiceStatus ParseStatus(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => ServiceStatus.Active,
            "INACTIVE" => ServiceStatus.Inactive,
            _ => throw DomainException.ForField(
                "status", "Trạng thái dịch vụ chỉ nhận ACTIVE hoặc INACTIVE.")
        };

    public static ServiceDto ToDto(Service service) => new(
        service.Id,
        service.TenantId,
        service.Name,
        service.Category,
        service.Price,
        service.DurationMinutes,
        service.BufferMinutes,
        service.Description,
        ToWireFormat(service.Status),
        service.CreatedAt,
        service.UpdatedAt);
}
