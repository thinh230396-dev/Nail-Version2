using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Auth;
using NailManagement.Application.Features.Tenants;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Auth.UseCases;

/// <summary>
/// Đặt tiệm đang làm việc cho phiên — BR-AUTH-025.
/// <para>
/// Đây là <b>một thao tác trên phiên</b>, không phải một tham số gửi kèm từng request. Nếu
/// mỗi request tự khai mình thuộc tiệm nào thì lời khai đó đến từ trình duyệt, và ai cũng
/// sửa được. Để trong phiên thì chỉ máy chủ ghi được.
/// </para>
/// <para>
/// Đánh đổi đã chấp nhận từ đầu: mở hai tab xem hai tiệm khác nhau sẽ đá nhau, vì cả hai
/// tab dùng chung một phiên.
/// </para>
/// </summary>
public sealed class SelectActiveTenantUseCase(
    ISessionRepository sessions,
    IUserTenantRepository userTenants,
    ITenantRepository tenants,
    IClock clock)
{
    public async Task<TenantScopeDto> ExecuteAsync(
        SelectTenantCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = (command.TenantId ?? string.Empty).Trim();
        if (tenantId.Length == 0)
            throw DomainException.ForField("tenantId", "Chưa chọn tiệm.");

        var now = clock.UtcNow;

        var session = await sessions.FindByIdAsync(command.SessionId ?? string.Empty, cancellationToken);
        if (session is null || !session.IsValidAt(now))
            throw new UnauthenticatedException();

        // BR-AUTH-026 — phép kiểm tra QUAN TRỌNG NHẤT của use case này. Thiếu nó thì bất kỳ
        // tài khoản nào cũng tự trỏ phiên của mình sang tiệm bất kỳ, và toàn bộ hàng rào
        // cách ly dữ liệu phía sau trở thành vô nghĩa vì nó tin vào giá trị trong phiên.
        var hasAccess = await userTenants.HasAccessAsync(session.UserId, tenantId, cancellationToken);
        if (!hasAccess)
            throw new ForbiddenException("Tài khoản không được giao quản lý tiệm này.");

        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            throw new NotFoundException("Không tìm thấy tiệm.");

        if (tenant.Package is null)
            throw new InvalidOperationException(
                $"Tiệm {tenant.Id} không đọc được gói đăng ký. Kho dữ liệu phải trả về tiệm kèm gói.");

        session.SetActiveTenant(tenant.Id, now);
        await sessions.UpdateAsync(session, cancellationToken);

        // Tiệm quá hạn vẫn CHỌN ĐƯỢC: BR-TENANT-010 cho phép xem toàn bộ dữ liệu, chỉ chặn
        // ghi. Chặn luôn ở đây thì chủ tiệm không vào nổi trang gia hạn để tự mở khóa.
        return TenantMapper.ToScope(tenant, tenant.Package, now);
    }
}
