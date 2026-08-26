using Microsoft.AspNetCore.Mvc;
using NailManagement.API.Security;
using NailManagement.Application.DTOs;
using NailManagement.Application.UseCases.Tenants;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Enums.Platform;

namespace NailManagement.API.Controllers;

/// <summary>Thân request tạo tài khoản chủ tiệm kèm theo tiệm mới — BR-TENANT-004.</summary>
public sealed record TenantOwnerRequest(
    string? Mode,
    string? ExistingUserId,
    string? Email,
    string? Username,
    string? DisplayName,
    string? Password);

public sealed record CreateTenantRequest(
    string? Code,
    string? Name,
    string? PackageId,
    DateTimeOffset? ExpiresAt,
    bool IsTrial,
    string? BillingCycle,
    string? Address,
    string? Phone,
    string? ContactEmail,
    string? Timezone,
    string? PrimaryBranchName,
    string? PrimaryBranchCode,
    TenantOwnerRequest? Owner);

public sealed record UpdateTenantRequest(
    string? Name,
    string? Address,
    string? Phone,
    string? ContactEmail);

public sealed record RenewTenantRequest(DateTimeOffset? ExpiresAt);

public sealed record ChangeTenantStatusRequest(string? Status);

/// <summary>
/// Quản lý tiệm — tầng nền tảng, chỉ Superadmin.
/// <para>
/// Cả controller gắn <c>RequiresTenant = false</c> vì tài khoản Superadmin không thuộc tiệm
/// nào; bắt họ chọn tiệm trước khi quản lý danh sách tiệm là một vòng lặp không có lối ra.
/// Phép cách ly ở đây không đến từ bộ lọc theo tiệm mà đến từ ma trận quyền: chỉ vai trò
/// Superadmin có ô <c>Tenants</c>, hai vai trò còn lại vắng mặt nên bị từ chối.
/// </para>
/// <para>
/// Không endpoint nào cần <c>AllowWhenTenantReadonly</c>: lệnh chặn ghi chỉ kích hoạt khi
/// phiên đang gắn một tiệm hết hạn, mà phiên của Superadmin thì không gắn tiệm nào.
/// </para>
/// </summary>
[ApiController]
[Route("api/tenants")]
public sealed class TenantsController(
    ListTenantsUseCase listTenants,
    GetTenantUseCase getTenant,
    CreateTenantUseCase createTenant,
    UpdateTenantUseCase updateTenant,
    RenewTenantUseCase renewTenant,
    ChangeTenantStatusUseCase changeTenantStatus,
    DeleteTenantUseCase deleteTenant,
    RequestScope requestScope) : ControllerBase
{
    [HttpGet]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(new { tenants = await listTenants.ExecuteAsync(cancellationToken) });

    [HttpGet("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        => Ok(new { tenant = await getTenant.ExecuteAsync(id, cancellationToken) });

    /// <summary>
    /// BR-TENANT-004/005 — tạo tiệm, chi nhánh chính, tài khoản chủ tiệm và hóa đơn đăng ký
    /// trong một giao dịch.
    /// <para>
    /// Trả 201 kèm <c>generatedPassword</c> khi máy chủ vừa tự sinh mật khẩu. Đó là lần duy
    /// nhất chuỗi ấy rời khỏi máy chủ, nên màn hình phải hiện nó ngay — hệ thống không gửi
    /// email và cũng không có đường đọc lại.
    /// </para>
    /// </summary>
    [HttpPost]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRequest? request, CancellationToken cancellationToken)
    {
        var owner = request?.Owner;

        var result = await createTenant.ExecuteAsync(
            new CreateTenantCommand(
                request?.Code ?? string.Empty,
                request?.Name ?? string.Empty,
                request?.PackageId ?? string.Empty,
                // Hạn dùng thiếu thì để giá trị nhỏ nhất và Tenant.Create sẽ từ chối kèm tên
                // ô nhập. Tự chọn một ngày thay người dùng là âm thầm tạo ra hợp đồng khác
                // với thứ họ định ký (BR-TENANT-006).
                request?.ExpiresAt ?? DateTimeOffset.MinValue,
                request?.IsTrial ?? false,
                request?.BillingCycle,
                request?.Address,
                request?.Phone,
                request?.ContactEmail,
                request?.Timezone,
                request?.PrimaryBranchName,
                request?.PrimaryBranchCode,
                new TenantOwnerCommand(
                    owner?.Mode ?? string.Empty,
                    owner?.ExistingUserId,
                    owner?.Email,
                    owner?.Username,
                    owner?.DisplayName,
                    owner?.Password)),
            Actor(),
            cancellationToken);

        return Created(
            $"/api/tenants/{result.Tenant.Id}",
            new { tenant = result.Tenant, generatedPassword = result.GeneratedPassword });
    }

    [HttpPut("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> Update(
        string id, [FromBody] UpdateTenantRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await updateTenant.ExecuteAsync(
            new UpdateTenantCommand(
                id,
                request?.Name ?? string.Empty,
                request?.Address,
                request?.Phone,
                request?.ContactEmail),
            Actor(),
            cancellationToken);

        return Ok(new { tenant });
    }

    /// <summary>BR-TENANT-006 — gia hạn bằng ngày hết hạn mới do Superadmin nhập tay.</summary>
    [HttpPost("{id}/renew")]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> Renew(
        string id, [FromBody] RenewTenantRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await renewTenant.ExecuteAsync(
            new RenewTenantCommand(id, request?.ExpiresAt ?? DateTimeOffset.MinValue),
            Actor(),
            cancellationToken);

        return Ok(new { tenant });
    }

    /// <summary>
    /// Khóa hoặc mở khóa tiệm — BR-TENANT-002.
    /// <para>
    /// Là <c>PATCH</c> chứ không phải <c>PUT</c> vì nó chỉ đụng tới một trường, và nó tách
    /// khỏi lệnh sửa hồ sơ để việc khóa một tiệm không bao giờ xảy ra như tác dụng phụ của
    /// một lần sửa tên.
    /// </para>
    /// </summary>
    [HttpPatch("{id}/status")]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> ChangeStatus(
        string id, [FromBody] ChangeTenantStatusRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await changeTenantStatus.ExecuteAsync(
            new ChangeTenantStatusCommand(id, request?.Status ?? string.Empty),
            Actor(),
            cancellationToken);

        return Ok(new { tenant });
    }

    /// <summary>
    /// BR-TENANT-020 — xóa mềm. Động từ <c>DELETE</c> giữ nguyên vì dưới mắt người dùng đây
    /// vẫn là "xóa tiệm"; chỉ có cách cài đặt là mềm, và đó là chuyện của tầng trong.
    /// </summary>
    [HttpDelete("{id}")]
    [RequireAuth]
    [RequirePermission(Feature.Tenants, RequiresTenant = false, Write = true)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await deleteTenant.ExecuteAsync(id, Actor(), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Người thực hiện, dựng từ phiên đăng nhập chứ không từ thân request — BR-AUD-003.
    /// Nhận từ thân request thì bản ghi nhật ký chỉ là lời khai của trình duyệt.
    /// </summary>
    private ActorContext Actor()
        => requestScope.ToActor(HttpContext.Connection.RemoteIpAddress?.ToString());
}
