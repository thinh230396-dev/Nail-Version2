using Microsoft.AspNetCore.Mvc.Filters;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Domain.Access;

namespace NailManagement.API.Security;

/// <summary>
/// Gắn một endpoint vào một nhóm chức năng trong ma trận phân quyền, rồi cưỡng chế
/// <b>bước 2 và bước 3</b> của BR-TENANT-013.
/// <para>
/// Toàn bộ thứ tự bốn bước được ráp ở <c>Program.cs</c>:
/// </para>
/// <list type="number">
///   <item>Tiệm còn hạn không — <c>TenantWriteGuardMiddleware</c>, chạy trước bộ lọc này</item>
///   <item>Gói có mở tính năng không — ngay tại đây</item>
///   <item>Vai trò có quyền không — ngay tại đây, sau bước 2</item>
///   <item>Dữ liệu có thuộc tiệm không — bộ lọc theo tiệm ở <c>NailDbContext</c></item>
/// </list>
/// <para>
/// Thứ tự đó không được đảo. Kiểm tra vai trò trước hạn dùng sẽ khiến tiệm hết hạn nhận
/// thông báo "không có quyền" thay vì "cần gia hạn"; kiểm tra vai trò trước gói sẽ nói với
/// chủ tiệm rằng họ thiếu quyền, trong khi thứ họ thiếu là gói cao hơn.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute(Feature feature) : Attribute, IAuthorizationFilter
{
    public Feature Feature { get; } = feature;

    /// <summary>Thao tác ghi. Ô "chỉ xem" trong ma trận sẽ từ chối, ô "toàn quyền" thì cho qua.</summary>
    public bool Write { get; init; }

    /// <summary>
    /// Endpoint có bắt buộc phải đang làm việc trong một tiệm hay không.
    /// <para>
    /// Đặt false cho các chức năng ở tầng nền tảng mà Superadmin dùng — quản lý tiệm, bảng
    /// giá, nhật ký toàn hệ thống — vì tài khoản Superadmin không thuộc tiệm nào.
    /// </para>
    /// </summary>
    public bool RequiresTenant { get; init; } = true;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var scope = context.HttpContext.RequestServices.GetRequiredService<RequestScope>();

        if (!scope.IsAuthenticated) throw scope.Rejection ?? new UnauthenticatedException();

        var current = scope.Require();

        // Vai trò không có Ô NÀO trong ma trận cho nhóm chức năng này thì từ chối ngay, kể
        // cả trước khi hỏi tới tiệm. Đây KHÔNG phải đảo thứ tự bước 2 và bước 3: với những
        // vai trò có ô — chủ tiệm, lễ tân — phép kiểm này không đúng, nên luồng của họ vẫn
        // đi qua hạn dùng rồi tới gói rồi mới tới quyền, y như cũ.
        //
        // Nó có mặt để chữa một câu chữ sai với Superadmin: họ không thuộc tiệm nào và không
        // bao giờ chọn được tiệm (BR-AUTH-031), nên nếu gọi một endpoint thuộc phạm vi tiệm
        // thì trước đây nhận về "Chưa chọn tiệm để làm việc" — một lời mời làm việc bất khả
        // thi. Sự thật là họ không có quyền với nhóm chức năng đó, và giờ hệ thống nói đúng
        // điều đó.
        if (PermissionMatrix.Resolve(current.Role, Feature) == AccessLevel.None)
            throw new ForbiddenException();

        if (RequiresTenant)
        {
            // Chưa chọn tiệm thì chưa trả lời được câu hỏi "gói nào" và "dữ liệu của ai".
            // Trả mã lỗi riêng để frontend đưa về màn chọn tiệm, không phải màn đăng nhập.
            if (current.Tenant is null) throw new TenantNotSelectedException();

            // Bước 2 — BR-SUB-007.
            if (!FeatureCapabilityPolicy.IsUnlocked(Feature, current.Tenant.Capabilities))
                throw new FeatureLockedException(FeatureCapabilityPolicy.CapabilityFor(Feature)!);
        }

        // Bước 3 — ma trận mục 3.4.
        if (!PermissionMatrix.Allows(current.Role, Feature, Write))
            throw new ForbiddenException();
    }
}
