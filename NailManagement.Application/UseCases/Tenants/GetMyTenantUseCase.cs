using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs.Platform;
using NailManagement.Domain.Repositories.Platform;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Hồ sơ của chính tiệm đang làm việc, dành cho cổng chủ tiệm.
/// <para>
/// Không nhận mã tiệm, kể cả trong đường dẫn: tiệm đến từ phiên đăng nhập (BR-AUTH-024) và
/// đã đi qua phép kiểm tra bảng <c>UserTenants</c> ở bước 2 của BR-ISO-003. Nhận mã tiệm từ
/// client là mở đúng cánh cửa mà <c>GET /api/tenants/{id}</c> đang đóng — endpoint đó chỉ
/// Superadmin gọi được, và lý do là nó đọc được hồ sơ của <i>bất kỳ</i> tiệm nào.
/// </para>
/// <para>
/// Trả về cùng hình dạng <see cref="TenantDetailDto"/> mà màn quản lý tiệm của Superadmin
/// dùng. Dựng một DTO thứ hai gọn hơn cho chủ tiệm nghe hợp lý, nhưng khi đó frontend sẽ có
/// hai phép chuyển đổi cho cùng một khái niệm, và hai màn hình sẽ hiểu khác nhau về cùng
/// một tiệm — đúng thứ mà hook <c>useTenants</c> ở ngày 6 sinh ra để ngăn.
/// </para>
/// <para>
/// Ai gọi được thì do ma trận quyền quyết định: chỉ chủ tiệm có ô
/// <c>OwnTenantProfile</c>. Lễ tân không có, vì hồ sơ này mang theo danh sách chủ tiệm kèm
/// email, giá gói và hạn dùng.
/// </para>
/// </summary>
public sealed class GetMyTenantUseCase(
    ITenantRepository tenants,
    ITenantContext tenantContext,
    TenantReadService reader,
    IClock clock)
{
    public async Task<TenantDetailDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Tới đây thì RequirePermission đã bảo đảm phiên có tiệm; phép kiểm này chỉ để lỗi
        // lập trình lộ ra sớm thay vì thành một truy vấn không có phạm vi.
        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm đang làm việc.");

        return await reader.DescribeAsync(tenant, clock.UtcNow, cancellationToken);
    }
}
