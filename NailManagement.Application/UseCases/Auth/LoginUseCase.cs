using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Auth;

/// <summary>
/// Đăng nhập bằng email hoặc username.
/// <para><b>Thứ tự kiểm tra không được đảo:</b></para>
/// <list type="number">
///   <item>Tìm được tài khoản không?</item>
///   <item>Có đang bị khóa tạm không? — khóa tạm thắng cả mật khẩu đúng</item>
///   <item>Tài khoản có Active không? — BR-AUTH-021</item>
///   <item>Mật khẩu có đúng không? — sai thì cộng dồn số lần sai</item>
/// </list>
/// <para>
/// Bước 2 phải đứng trước bước 4, nếu không thì việc khóa tạm trở nên vô nghĩa: người tấn
/// công cứ thử tiếp và vẫn biết được lúc nào đoán trúng.
/// </para>
/// </summary>
public sealed class LoginUseCase(
    IUserRepository users,
    ISessionRepository sessions,
    IUserTenantRepository userTenants,
    IPasswordHasher hasher,
    IAuditLogger audit,
    IClock clock,
    IIdGenerator ids)
{
    public async Task<LoginResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var identifier = (command.Identifier ?? string.Empty).Trim().ToLowerInvariant();
        var password = command.Password ?? string.Empty;

        var missing = new List<FieldError>();
        if (identifier.Length == 0)
            missing.Add(new FieldError("identifier", "Nhập email hoặc tên đăng nhập."));
        if (password.Length == 0)
            missing.Add(new FieldError("password", "Nhập mật khẩu."));
        if (missing.Count > 0)
            throw new DomainException("Thiếu thông tin đăng nhập.", missing);

        var now = clock.UtcNow;
        var user = await users.FindByIdentifierAsync(identifier, cancellationToken);

        // Không phân biệt "không có tài khoản" với "sai mật khẩu" — nói rõ là để lộ email
        // nào có thật trong hệ thống.
        if (user is null)
        {
            // Và phép giấu ấy phải kín cả về THỜI GIAN, không chỉ về câu chữ.
            //
            // Thoát ra ngay tại đây là bỏ qua phép băm — mà phép băm tốn hàng chục mili-giây vì
            // PBKDF2 chạy 210.000 vòng. Người bấm đồng hồ sẽ thấy "email không tồn tại" trả lời
            // nhanh gấp hàng chục lần "sai mật khẩu", và dò ra ngay email nào có thật: đúng thứ
            // mà dòng chú thích ngay trên tưởng đã chặn được.
            //
            // Nên ở đây vẫn so mật khẩu, với một cặp hash mồi không thuộc về ai. Kết quả chắc
            // chắn là `false` và bị bỏ đi — thứ cần là thời gian đã tiêu, không phải câu trả lời.
            _ = hasher.Verify(password, hasher.Decoy);

            await RecordFailureAsync(null, identifier, "Không tìm thấy tài khoản", command.Ip, cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (user.IsLockedAt(now))
        {
            await RecordFailureAsync(user, identifier, "Tài khoản đang bị khóa tạm", command.Ip, cancellationToken);
            throw new AccountLockedException(user.LockedUntil!.Value);
        }

        if (!user.IsActive())
        {
            await RecordFailureAsync(user, identifier, $"Tài khoản ở trạng thái {user.Status}", command.Ip, cancellationToken);
            throw new AccountNotActiveException();
        }

        var matches = hasher.Verify(password, new HashedPassword(user.PasswordHash, user.PasswordSalt));
        if (!matches)
        {
            user.RegisterFailedAttempt(now);
            await users.UpdateAsync(user, cancellationToken);

            await RecordFailureAsync(user, identifier, "Sai mật khẩu", command.Ip, cancellationToken);

            // BR-AUD-002 — lần sai vừa rồi làm tài khoản bị khóa tạm thì đó là một sự kiện
            // riêng, đáng ghi riêng: đây là dấu hiệu dò mật khẩu, không phải gõ nhầm.
            if (user.IsLockedAt(now))
            {
                await audit.RecordAsync(
                    new AuditEntry(
                        AuditEvent.AccountLocked,
                        ActorUserId: user.Id,
                        ActorRole: user.Role,
                        TargetType: nameof(AppUser),
                        TargetId: user.Id,
                        Ip: command.Ip,
                        Metadata: new Dictionary<string, string>
                        {
                            ["lockedUntil"] = user.LockedUntil!.Value.ToString("O"),
                            ["reason"] = "Đăng nhập sai quá số lần cho phép"
                        }),
                    cancellationToken);
            }

            throw new InvalidCredentialsException();
        }

        user.RegisterSuccessfulLogin(now);
        await users.UpdateAsync(user, cancellationToken);

        var lifetime = AuthPolicy.LifetimeFor(command.Remember);
        var session = AppSession.Issue(
            ids.NewId(),
            user.Id,
            now,
            lifetime,
            command.Ip,
            command.UserAgent);

        var activeTenantId = await ResolveInitialTenantAsync(user, now, session, cancellationToken);

        await sessions.AddAsync(session, cancellationToken);

        await audit.RecordAsync(
            new AuditEntry(
                AuditEvent.Login,
                ActorUserId: user.Id,
                ActorRole: user.Role,
                TenantId: activeTenantId,
                TargetType: nameof(AppSession),
                TargetId: session.Id,
                Ip: command.Ip),
            cancellationToken);

        return new LoginResult(
            AccountMapper.ToDto(user),
            new IssuedSessionDto(session.Id, session.ExpiresAt, (int)lifetime.TotalSeconds),
            MustSelectTenant: user.Role != UserRole.SuperAdmin && activeTenantId is null);
    }

    /// <summary>
    /// Đặt sẵn tiệm đang làm việc cho những vai trò chỉ có đúng một tiệm.
    /// <para>
    /// Chủ tiệm thì KHÔNG: BR-AUTH-025 buộc họ đi qua màn chọn tiệm kể cả khi chỉ quản lý
    /// một tiệm, để thao tác đổi tiệm luôn nằm ở cùng một chỗ và người dùng luôn biết mình
    /// đang làm việc cho tiệm nào.
    /// </para>
    /// <para>
    /// Lễ tân thì có: hồ sơ nhân viên của họ chỉ thuộc đúng một chi nhánh của đúng một tiệm
    /// (BR-EMP-003), nên bắt chọn là bắt bấm một nút không có lựa chọn nào khác.
    /// </para>
    /// </summary>
    private async Task<string?> ResolveInitialTenantAsync(
        AppUser user, DateTimeOffset now, AppSession session, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Receptionist) return null;

        var tenantIds = await userTenants.ListTenantIdsAsync(user.Id, cancellationToken);
        if (tenantIds.Count != 1) return null;

        session.SetActiveTenant(tenantIds[0], now);
        return tenantIds[0];
    }

    /// <summary>
    /// BR-AUD-002 — mọi lần đăng nhập hỏng đều được ghi, kể cả khi không tìm thấy tài khoản.
    /// <para>
    /// Chuỗi định danh người dùng gõ vào được lưu trong phần dữ liệu kèm theo. Đó là thứ
    /// duy nhất phân biệt được "chủ tiệm gõ nhầm mật khẩu ba lần" với "ai đó đang dò lần
    /// lượt hàng loạt email khác nhau".
    /// </para>
    /// </summary>
    private Task RecordFailureAsync(
        AppUser? user, string identifier, string reason, string? ip, CancellationToken cancellationToken)
        => audit.RecordAsync(
            new AuditEntry(
                AuditEvent.LoginFailed,
                ActorUserId: user?.Id,
                ActorRole: user?.Role,
                TargetType: nameof(AppUser),
                TargetId: user?.Id,
                Ip: ip,
                Metadata: new Dictionary<string, string>
                {
                    ["identifier"] = identifier,
                    ["reason"] = reason
                }),
            cancellationToken);
}
