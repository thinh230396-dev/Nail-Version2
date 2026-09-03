using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Cửa sau vào database, dùng cho <b>đúng hai trạng thái mà API không dựng ra được</b>.
/// <para>
/// Mọi phép thử khác đi qua HTTP như người dùng thật, và đó là điều đáng giữ: một bộ kiểm thử
/// tự sửa database rồi tự đọc lại chỉ chứng minh được rằng EF Core hoạt động. Hai ngoại lệ dưới
/// đây tồn tại vì hệ thống <b>cố ý</b> không có đường đi tới chúng:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Tiệm quá hạn.</b> <c>Tenant.Renew</c> từ chối mọi ngày ở quá khứ (BR-TENANT-006 — gia hạn
/// là đẩy hạn dùng ra xa), nên không lệnh nào đẩy một tiệm vào trạng thái <c>OVERDUE</c> được.
/// Đúng như thiết kế: BR-TENANT-002 nói trạng thái ấy <b>tính lúc đọc</b> từ hạn dùng, không ai
/// đặt nó.
/// </item>
/// <item>
/// <b>Tài khoản bị khóa.</b> Endpoint khóa tài khoản chủ tiệm chưa tồn tại — đây là việc còn
/// treo từ lát cắt nhân viên. Nhưng BR-AUTH-022 thì đã được cưỡng chế ở <c>SessionMiddleware</c>
/// ngay từ lát cắt phiên, và nó cần được kiểm ngay bây giờ chứ không phải chờ tới lúc có màn hình.
/// </item>
/// </list>
/// </summary>
public static class TestDatabase
{
    /// <summary>
    /// Đẩy hạn dùng của một tiệm về quá khứ, để <c>resolveTenantStatus</c> đọc ra <c>OVERDUE</c>.
    /// </summary>
    public static Task ExpireTenantAsync(SalonSysFactory factory, string tenantId)
        => UpdateTenantExpiryAsync(factory, tenantId, DateTimeOffset.UtcNow.AddDays(-3));

    /// <summary>Trả hạn dùng về tương lai, để các phép thử sau không thấy một tiệm chỉ đọc.</summary>
    public static Task RestoreTenantAsync(SalonSysFactory factory, string tenantId)
        => UpdateTenantExpiryAsync(factory, tenantId, DateTimeOffset.UtcNow.AddDays(60));

    public static Task SetAccountStatusAsync(SalonSysFactory factory, string userId, AccountStatus status)
        => WithDbAsync(factory, db => db.AppUsers
            .IgnoreQueryFilters()
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setter => setter.SetProperty(user => user.Status, status)));

    private static Task UpdateTenantExpiryAsync(
        SalonSysFactory factory, string tenantId, DateTimeOffset expiresAt)
        => WithDbAsync(factory, db => db.Tenants
            .IgnoreQueryFilters()
            .Where(tenant => tenant.Id == tenantId)
            .ExecuteUpdateAsync(setter => setter.SetProperty(tenant => tenant.ExpiresAt, expiresAt)));

    /// <summary>
    /// Mượn một <c>DbContext</c> theo đúng vòng đời mà máy chủ dùng.
    /// <para>
    /// Bỏ qua bộ lọc toàn cục ở mọi lời gọi bên trên là bắt buộc: ngoài một request HTTP thì
    /// không có tiệm đang làm việc nào, và bộ lọc ở <c>NailDbContext</c> cố ý <b>đóng lại</b>
    /// khi không rõ phạm vi — không dòng nào đọc được.
    /// </para>
    /// </summary>
    private static async Task WithDbAsync(SalonSysFactory factory, Func<NailDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();

        await action(scope.ServiceProvider.GetRequiredService<NailDbContext>());
    }
}
