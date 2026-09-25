using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Accounts;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories.Auth;

namespace NailManagement.Application.Features.Accounts.UseCases;

/// <summary>
/// Khóa hoặc mở khóa một tài khoản chủ tiệm — BR-AUTH-020.
/// <para>
/// Lấp chỗ trống treo từ ngày 7: <c>AccountStatus.Suspended</c> có mặt trong enum và được
/// BR-AUTH-020 nhắc tới, nhưng trước lát cắt này <b>không đường nào sinh ra nó</b> —
/// <c>AppUser</c> chỉ có <c>Deactivate()</c> đưa thẳng về <c>Inactive</c> vĩnh viễn.
/// </para>
/// <para>
/// Không phải làm gì cho phần cưỡng chế, và đó là cùng một món quà mà lát cắt thu hồi phiên đã
/// nhận: <c>AppUser.IsActive()</c> chỉ đúng khi trạng thái là <c>Active</c>, còn BR-AUTH-022
/// bắt mọi request phải đọc lại tài khoản. Đặt một cột là người bị khóa mất quyền ngay ở
/// request kế tiếp, kể cả khi họ đang mở sẵn màn hình.
/// </para>
/// </summary>
public sealed class ChangeAccountStatusUseCase(
    IUserRepository users,
    IUserTenantRepository userTenants,
    IAuditLogger audit,
    IClock clock)
{
    public async Task<TenantAdminAccountDto> ExecuteAsync(
        ChangeAccountStatusCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Chỉ hai giá trị, và cố ý KHÔNG nhận INACTIVE. Vô hiệu vĩnh viễn là một quyết định
        // khác hẳn về hậu quả — không hoàn tác được, mà BR-DEL-001 lại không cho xóa để tạo
        // lại — nên nó phải có đường đi và nút bấm riêng, không nấp sau cùng một tham số với
        // thao tác khóa tạm.
        var status = (command.Status ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => AccountStatus.Active,
            "SUSPENDED" => AccountStatus.Suspended,
            _ => throw DomainException.ForField(
                "status", "Trạng thái tài khoản chỉ nhận ACTIVE hoặc SUSPENDED.")
        };

        var account = await users.FindByIdAsync(command.AccountId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản này.");

        // Endpoint nằm sau ô quyền TenantAdminAccounts, và ô ấy nói về tài khoản CHỦ TIỆM.
        // Không chặn ở đây thì cùng một đường dẫn khóa được cả tài khoản lễ tân của một tiệm
        // bất kỳ — tức Superadmin với tay vào nhân sự trong tiệm, thứ mà BR-AUTH-030 dựng ra
        // để ngăn. Lễ tân bị vô hiệu theo hồ sơ nhân viên của họ, ở màn nhân sự của chủ tiệm.
        if (account.Role != UserRole.TenantAdmin)
            throw new ForbiddenException(
                "Đường này chỉ khóa được tài khoản chủ tiệm. Tài khoản lễ tân do chủ tiệm quản ở màn nhân sự.");

        // Tự khóa chính mình thì mất luôn đường vào để mở ra. Superadmin không phải chủ tiệm
        // nên phép kiểm này gần như không bao giờ chạm tới — nhưng "gần như không bao giờ" là
        // lý do tồi để bỏ một hàng rào rẻ tiền, và ngày nào đó vai trò có thể đổi.
        if (account.Id == actor.UserId)
            throw new ForbiddenException("Không khóa được tài khoản bạn đang dùng.");

        // Vô hiệu là vĩnh viễn, theo đúng chú thích ở AppUser.Deactivate. Mở lại một tài khoản
        // Inactive bằng đường này là lặng lẽ gỡ bỏ tính vĩnh viễn ấy, nên chặn thành một câu
        // nói rõ thay vì âm thầm làm.
        if (account.Status == AccountStatus.Inactive)
            throw DomainException.ForField(
                "status",
                "Tài khoản đã bị vô hiệu vĩnh viễn (BR-AUTH-020) nên không đổi trạng thái được nữa.");

        // Đặt lại đúng trạng thái đang có không phải lỗi: màn hình đã ẩn nút tương ứng, nên
        // lần gọi lặp gần như chắc chắn là cú bấm đúp hoặc một tab mở từ trước. Trả về nguyên
        // trạng và KHÔNG ghi nhật ký — một dòng "đã khóa" cho lần bấm không đổi gì là làm nhiễu
        // đúng cái sổ sinh ra để đọc.
        if (account.Status != status)
        {
            if (status == AccountStatus.Suspended) account.Suspend(now);
            else account.Restore(now);

            await users.UpdateAsync(account, cancellationToken);
            await RecordAsync(account, status, actor, cancellationToken);
        }

        var tenantIds = await userTenants.ListTenantIdsByUserAsync([account.Id], cancellationToken);

        return AccountMapper.ToTenantAdminAccount(
            account,
            tenantIds.TryGetValue(account.Id, out var ids) ? ids : []);
    }

    /// <summary>
    /// Ghi vết bằng sự kiện riêng, không dùng lại <see cref="AuditEvent.AccountLocked"/>.
    /// <para>
    /// Sự kiện kia là hệ quả của việc ai đó gõ sai mật khẩu năm lần — không có người nào bấm.
    /// Dòng ở đây thì có người chịu trách nhiệm, và một sổ bảo mật phải phân biệt được hai
    /// chuyện đó.
    /// </para>
    /// <para>
    /// <c>TenantId</c> để trống vì một tài khoản chủ tiệm có thể giữ nhiều tiệm (BR-AUTH-023) —
    /// chọn lấy một tiệm để điền vào là bịa ra một phạm vi mà thao tác này không có. Danh sách
    /// tiệm bị ảnh hưởng đi vào phần metadata, nơi nó là dữ liệu chứ không phải khóa lọc.
    /// </para>
    /// </summary>
    private async Task RecordAsync(
        AppUser account, AccountStatus status, ActorContext actor, CancellationToken cancellationToken)
        => await audit.RecordAsync(
            new AuditEntry(
                status == AccountStatus.Suspended ? AuditEvent.AccountSuspended : AuditEvent.AccountRestored,
                actor.UserId,
                actor.Role,
                null,
                nameof(AppUser),
                account.Id,
                actor.Ip,
                new Dictionary<string, string>
                {
                    ["account"] = account.Email.Value,
                    ["status"] = status.ToString().ToUpperInvariant()
                }),
            cancellationToken);
}
