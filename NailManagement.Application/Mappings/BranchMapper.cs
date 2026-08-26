using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Application.Mappings;

/// <summary>Chuyển entity <see cref="Branch"/> sang DTO gửi ra ngoài.</summary>
public static class BranchMapper
{
    /// <summary>
    /// Hai chuỗi trạng thái chi nhánh. BR-BRANCH-003 nói rõ chỉ có hai, nên bảng ánh xạ này
    /// không cần nhánh mặc định nào ngoài lỗi.
    /// </summary>
    public static string ToWireFormat(BranchStatus status) => status switch
    {
        BranchStatus.Active => "ACTIVE",
        BranchStatus.Inactive => "INACTIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái chi nhánh không hợp lệ.")
    };

    public static BranchDto ToDto(Branch branch) => new(
        branch.Id,
        branch.TenantId,
        branch.Name,
        branch.Code,
        branch.Address,
        branch.Phone,
        branch.IsPrimary,
        ToWireFormat(branch.Status),
        branch.CreatedAt,
        branch.UpdatedAt);
}
