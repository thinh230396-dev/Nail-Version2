using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Accounts;
using NailManagement.Application.Features.Auth;
using NailManagement.Application.Features.Tenants;
using NailManagement.Domain.Access;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

namespace NailManagement.Application.Features.Auth.UseCases;

/// <summary>
/// Đọc tài khoản và phạm vi làm việc của phiên hiện tại.
/// <para>
/// <b>BR-AUTH-022 nằm ở đây.</b> Trạng thái tài khoản được kiểm tra lại ở mỗi lần đọc phiên,
/// không chỉ lúc đăng nhập. Nhờ vậy tài khoản vừa bị chuyển sang Suspended mất quyền ngay ở
/// request kế tiếp, thay vì dùng tiếp tới khi phiên hết hạn.
/// </para>
/// <para>
/// Từ ngày 3, middleware xác thực gọi chính use case này ở mỗi request — <b>đừng nhân bản
/// phép kiểm tra ở chỗ khác</b>. Đó cũng là lý do nó trả về kèm gói tính năng của tiệm:
/// tầng phân quyền cần dữ liệu đó ngay, và tách ra thành một lượt truy vấn riêng nghĩa là
/// mỗi lần bấm chuột lại thêm một vòng xuống database.
/// </para>
/// </summary>
public sealed class GetCurrentAccountUseCase(
    IUserRepository users,
    ISessionRepository sessions,
    IUserTenantRepository userTenants,
    ITenantRepository tenants,
    IStaffRepository staffMembers,
    IClock clock)
{
    public async Task<CurrentAccountResult> ExecuteAsync(
        string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new UnauthenticatedException();

        var now = clock.UtcNow;

        var session = await sessions.FindByIdAsync(sessionId, cancellationToken);
        if (session is null || !session.IsValidAt(now))
            throw new UnauthenticatedException();

        var user = await users.FindByIdAsync(session.UserId, cancellationToken);
        if (user is null)
            throw new UnauthenticatedException();

        // BR-AUTH-021 + BR-AUTH-022 — tài khoản không còn Active thì phiên hết giá trị.
        //
        // Ném ACCOUNT_NOT_ACTIVE chứ KHÔNG phải UNAUTHENTICATED, và khác biệt ấy quan trọng
        // với người dùng chứ không chỉ với mã: theo chú thích ở ErrorCode, frontend đưa người
        // dùng về màn đăng nhập khi gặp UNAUTHENTICATED và cố ý KHÔNG làm vậy với 403. Gộp hai
        // thứ lại thì người vừa bị khóa tài khoản bị đá về màn đăng nhập, đăng nhập lại, nhận
        // đúng lỗi ấy, và lặp mãi mà không đọc được lý do thật.
        //
        // Đây cũng là điều LoginUseCase đã làm cho cùng một tình huống, nên hai đường vào hệ
        // thống nay trả lời giống nhau thay vì mâu thuẫn.
        if (!user.IsActive())
            throw new AccountNotActiveException();

        var (activeTenantId, scope) =
            await ResolveTenantScopeAsync(user.Id, session.ActiveTenantId, now, cancellationToken);

        // Phiên trỏ tới một tiệm mà tài khoản không còn quyền: gỡ khỏi phiên ngay, đừng để
        // lần đọc sau lại đi kiểm tra một mã tiệm đã hết giá trị.
        if (activeTenantId != session.ActiveTenantId)
            session.SetActiveTenant(activeTenantId, now);

        session.Touch(now);
        await sessions.UpdateAsync(session, cancellationToken);

        var branch = await ResolveBranchAsync(user.StaffId, activeTenantId, cancellationToken);

        return new CurrentAccountResult(
            AccountMapper.ToDto(user),
            user.Role,
            activeTenantId,
            scope,
            branch,
            MustSelectTenant(user.Role, activeTenantId));
    }

    /// <summary>
    /// BR-EMP-004 — chi nhánh của tài khoản đọc qua hồ sơ nhân viên, không phải qua một cột
    /// chép sẵn trên bảng tài khoản. Hai bản sao của cùng một thông tin là hai chỗ để lệch
    /// nhau khi nhân viên chuyển chi nhánh.
    /// </summary>
    private async Task<BranchScopeDto?> ResolveBranchAsync(
        string? staffId, string? activeTenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffId) || activeTenantId is null) return null;

        var staff = await staffMembers.FindForSessionAsync(staffId, cancellationToken);
        if (staff?.Branch is null) return null;

        // Phép đối chiếu BẮT BUỘC: hàm trên cố ý bỏ qua bộ lọc theo tiệm vì nó chạy trong
        // lúc phiên còn đang dựng. Không có dòng này thì một hồ sơ nhân viên của tiệm khác
        // sẽ lọt vào phiên, và tên chi nhánh của tiệm khác hiện lên đầu màn hình.
        if (staff.TenantId != activeTenantId) return null;

        return new BranchScopeDto(staff.Branch.Id, staff.Branch.Code, staff.Branch.Name);
    }

    /// <summary>
    /// BR-ISO-003 — ba bước, không được rút gọn: lấy tiệm từ phiên, xác nhận tài khoản có
    /// quyền với tiệm đó qua bảng nối, rồi mới đọc dữ liệu tiệm.
    /// <para>
    /// Bước giữa là bước hay bị bỏ nhất, và bỏ nó thì chỉ cần một mã tiệm khác nằm trong
    /// phiên là đọc được dữ liệu của tiệm không phải của mình.
    /// </para>
    /// </summary>
    private async Task<(string? ActiveTenantId, TenantScopeDto? Scope)> ResolveTenantScopeAsync(
        string userId, string? sessionTenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionTenantId)) return (null, null);

        var hasAccess = await userTenants.HasAccessAsync(userId, sessionTenantId, cancellationToken);
        if (!hasAccess) return (null, null);

        // Tiệm đã bị xóa mềm cũng rơi vào nhánh này: tầng lưu trữ không trả về nó nữa
        // (BR-DEL-002), nên phiên tự động mất tiệm đang làm việc.
        var tenant = await tenants.FindByIdAsync(sessionTenantId, cancellationToken);
        if (tenant is null) return (null, null);

        if (tenant.Package is null)
            throw new InvalidOperationException(
                $"Tiệm {tenant.Id} không đọc được gói đăng ký. Kho dữ liệu phải trả về tiệm kèm gói.");

        return (tenant.Id, TenantMapper.ToScope(tenant, tenant.Package, now));
    }

    /// <summary>
    /// BR-AUTH-025 — chủ tiệm luôn phải qua màn chọn tiệm. Lễ tân được máy chủ đặt sẵn từ
    /// lúc đăng nhập nên chỉ rơi vào đây khi hồ sơ thiếu liên kết tiệm, và khi đó màn chọn
    /// tiệm là chỗ duy nhất nói cho họ biết có gì đó chưa đúng.
    /// </summary>
    private static bool MustSelectTenant(UserRole role, string? activeTenantId)
        => role != UserRole.SuperAdmin && activeTenantId is null;
}
