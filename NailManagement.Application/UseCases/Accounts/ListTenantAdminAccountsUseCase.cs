using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Accounts;

/// <summary>
/// Danh sách tài khoản chủ tiệm — chỉ đọc.
/// <para>
/// Phục vụ hai chỗ cùng lúc: màn quản lý tài khoản chủ tiệm của Superadmin, và ô chọn chủ
/// tiệm ở form tạo tiệm khi Superadmin giao tiệm mới cho một người đã có tài khoản
/// (BR-TENANT-004, <c>owner.mode = "existing"</c>).
/// </para>
/// <para>
/// Use case này <b>chỉ liệt kê</b>. Cấp, khóa và vô hiệu tài khoản thuộc lát cắt cấp tài
/// khoản, nằm ở phần sau của lộ trình. Tách như vậy để màn hình có dữ liệu thật ngay, thay
/// vì phải chờ đủ cả cụm thao tác ghi mới được nối.
/// </para>
/// </summary>
public sealed class ListTenantAdminAccountsUseCase(
    IUserRepository users,
    IUserTenantRepository userTenants)
{
    public async Task<IReadOnlyList<TenantAdminAccountDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = await users.ListByRoleAsync(UserRole.TenantAdmin, cancellationToken);

        if (accounts.Count == 0) return [];

        // Một lượt truy vấn cho cả danh sách. Hỏi từng tài khoản một thì mở màn hình có 20
        // chủ tiệm là 21 lượt đi database, và con số đó lớn dần theo lượng người dùng.
        var assignments = await userTenants.ListTenantIdsByUserAsync(
            [.. accounts.Select(account => account.Id)], cancellationToken);

        return
        [
            .. accounts.Select(account => AccountMapper.ToTenantAdminAccount(
                account,
                assignments.TryGetValue(account.Id, out var tenantIds) ? tenantIds : []))
        ];
    }
}
