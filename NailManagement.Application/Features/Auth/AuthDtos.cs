using NailManagement.Application.Features.Accounts;
using NailManagement.Application.Features.Tenants;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Application.Features.Auth;

/// <summary>Dữ liệu vào của <c>LoginUseCase</c>.</summary>
/// <param name="Identifier">Email hoặc username.</param>
/// <param name="Ip">Lấy từ request, ghi vào phiên để nhận ra phiên lạ.</param>
public sealed record LoginCommand(
    string Identifier,
    string Password,
    bool Remember,
    string? Ip,
    string? UserAgent);

/// <summary>
/// Phiên vừa cấp. Use case chỉ nói "phiên này sống bao lâu" — việc biến nó thành cookie
/// HttpOnly là chuyện của tầng API. Tầng Application không biết HTTP có cookie.
/// </summary>
public sealed record IssuedSessionDto(string Id, DateTimeOffset ExpiresAt, int MaxAgeSeconds);

/// <param name="MustSelectTenant">
/// BR-AUTH-025 — chủ tiệm luôn phải qua màn chọn tiệm, kể cả khi chỉ quản lý một tiệm.
/// Lễ tân thì không: hồ sơ nhân viên của họ chỉ thuộc đúng một tiệm nên máy chủ tự đặt
/// ngay lúc đăng nhập, đỡ cho người dùng thường xuyên nhất của hệ thống một lần bấm thừa
/// mỗi ca làm.
/// </param>
public sealed record LoginResult(
    AccountDto Account,
    IssuedSessionDto Session,
    bool MustSelectTenant);

/// <param name="Role">
/// Vai trò ở dạng enum, dành cho tầng phân quyền. <c>Account.Role</c> là cùng thông tin
/// nhưng ở dạng chuỗi cho frontend; tầng trong không nên phải so sánh chuỗi để quyết định
/// quyền hạn.
/// </param>
/// <param name="ActiveTenantId">BR-AUTH-024 — tiệm đang làm việc, lấy từ phiên.</param>
/// <param name="Tenant">
/// Rỗng khi phiên chưa gắn tiệm nào: Superadmin (không bao giờ có), hoặc chủ tiệm vừa đăng
/// nhập mà chưa chọn tiệm.
/// </param>
/// <param name="Branch">
/// Chi nhánh của tài khoản lễ tân, đọc qua hồ sơ nhân viên (BR-EMP-004). Rỗng với Superadmin
/// và chủ tiệm — hai vai trò đó không gắn hồ sơ nhân viên nên không thuộc chi nhánh nào.
/// </param>
public sealed record CurrentAccountResult(
    AccountDto Account,
    UserRole Role,
    string? ActiveTenantId,
    TenantScopeDto? Tenant,
    BranchScopeDto? Branch,
    bool MustSelectTenant);

/// <summary>Dữ liệu vào của <c>SelectActiveTenantUseCase</c> — BR-AUTH-025.</summary>
public sealed record SelectTenantCommand(string? SessionId, string TenantId);
