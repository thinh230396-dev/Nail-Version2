using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities;
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
    IPasswordHasher hasher,
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
            throw new InvalidCredentialsException();

        if (user.IsLockedAt(now))
            throw new AccountLockedException(user.LockedUntil!.Value);

        if (!user.IsActive())
            throw new AccountNotActiveException();

        var matches = hasher.Verify(password, new HashedPassword(user.PasswordHash, user.PasswordSalt));
        if (!matches)
        {
            user.RegisterFailedAttempt(now);
            await users.UpdateAsync(user, cancellationToken);
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

        await sessions.AddAsync(session, cancellationToken);

        return new LoginResult(
            AccountMapper.ToDto(user),
            new IssuedSessionDto(session.Id, session.ExpiresAt, (int)lifetime.TotalSeconds));
    }
}
