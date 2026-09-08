using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Common;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Auth;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories.Auth;
using NailManagement.Domain.Repositories.Salon;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Application.UseCases.Staff;

/// <summary>
/// Cấp tài khoản đăng nhập cho một hồ sơ lễ tân đã tồn tại — BR-AUTH-013.
/// <para>
/// <b>Đây là đường duy nhất để một tài khoản lễ tân ra đời.</b> Không có endpoint tạo tài
/// khoản lễ tân rời, và đó là điều cố ý: <c>staff_id</c> của tài khoản lễ tân là
/// <c>NOT NULL</c>, còn BR-EMP-004 quy định chi nhánh chỉ nằm trên hồ sơ nhân viên. Một tài
/// khoản lễ tân không gắn hồ sơ sẽ là một tài khoản không thuộc chi nhánh nào, tức không
/// làm được gì ở quầy.
/// </para>
/// <para>
/// Ba việc trong một giao dịch: tài khoản, liên kết tài khoản với tiệm, và mối nối sang hồ
/// sơ nhân viên. Thiếu dòng liên kết ở bảng <c>UserTenants</c> thì lễ tân đăng nhập được
/// nhưng phiên không nhận tiệm nào — BR-ISO-003 bắt kiểm tra liên kết đó trước khi đặt tiệm
/// đang làm việc, không phân biệt vai trò.
/// </para>
/// </summary>
public sealed class GrantStaffAccountUseCase(
    IStaffRepository staffMembers,
    IUserRepository users,
    IUserTenantRepository userTenants,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    IPasswordGenerator passwordGenerator,
    IAuditLogger audit,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<GrantStaffAccountResult> ExecuteAsync(
        GrantStaffAccountCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        var staff = await staffMembers.FindByIdAsync(command.StaffId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhân viên.");

        // BR-AUTH-002 — kỹ thuật viên không có tài khoản đăng nhập. Chặn ở đây để thông báo
        // nói đúng lý do; <c>AppUser.AttachStaff</c> vẫn từ chối lần nữa nếu ai đó đi vòng.
        if (staff.Role != StaffRole.Receptionist)
        {
            throw DomainException.ForField(
                "staffId", "Chỉ hồ sơ lễ tân mới được cấp tài khoản đăng nhập.");
        }

        // Cấp quyền đăng nhập cho người đã nghỉ việc là mở lại đúng cánh cửa mà lệnh cho
        // nghỉ vừa đóng (quyết định 32).
        if (staff.Status == StaffStatus.Inactive)
        {
            throw DomainException.ForField(
                "staffId", "Nhân viên đã nghỉ việc. Nhận lại họ trước rồi mới cấp tài khoản.");
        }

        if (await users.FindByStaffIdAsync(staff.Id, cancellationToken) is not null)
        {
            throw DomainException.ForField(
                "staffId", "Hồ sơ này đã có tài khoản đăng nhập.");
        }

        // Email trên hồ sơ nhân viên là giá trị mặc định hợp lý: người cấp tài khoản vừa
        // nhập nó ở màn hồ sơ, bắt gõ lại là mời gõ lệch.
        var rawEmail = string.IsNullOrWhiteSpace(command.Email) ? staff.Email : command.Email;

        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            throw DomainException.ForField(
                "email", "Hồ sơ chưa có email nên phải nhập email đăng nhập cho tài khoản này.");
        }

        var email = Email.Create(rawEmail, "email");

        // BR-VAL-001 — email duy nhất toàn hệ thống. Tra bằng chính hàm mà lúc đăng nhập
        // dùng, nên phép so khớp hoa thường ở hai nơi không thể lệch nhau.
        if (await users.FindByIdentifierAsync(email.Value, cancellationToken) is not null)
            throw DomainException.ForField("email", "Email này đã được dùng cho một tài khoản khác.");

        var username = Guard.Optional(command.Username, "username", "Tên đăng nhập", 64)?.ToLowerInvariant();

        if (username is not null
            && await users.FindByIdentifierAsync(username, cancellationToken) is not null)
        {
            throw DomainException.ForField("username", "Tên đăng nhập này đã có người dùng.");
        }

        // Bỏ trống thì máy chủ sinh, và trả lại đúng một lần. Hệ thống không gửi email nên
        // không trả về nghĩa là tài khoản vừa tạo không ai đăng nhập được.
        var generated = string.IsNullOrWhiteSpace(command.Password) ? passwordGenerator.Generate() : null;
        var hashed = hasher.Hash(RawPassword.Create(generated ?? command.Password!, "password"));

        var account = AppUser.Create(
            ids.NewId("USR"),
            email,
            username,
            hashed.Hash,
            hashed.Salt,
            UserRole.Receptionist,
            string.IsNullOrWhiteSpace(command.DisplayName) ? staff.FullName : command.DisplayName.Trim(),
            now);

        // BR-AUTH-013 — mối nối sang hồ sơ, và cũng là đường duy nhất để tài khoản này có
        // chi nhánh (BR-EMP-004). Gọi trước khi ghi để một tài khoản chưa gắn hồ sơ không
        // bao giờ tồn tại, dù chỉ trong một khoảnh khắc của giao dịch.
        account.AttachStaff(staff.Id, now);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await users.AddAsync(account, ct);
            await userTenants.LinkAsync(account.Id, tenantId, now, ct);

            return true;
        }, cancellationToken);

        await audit.RecordAsync(
            new AuditEntry(
                AuditEvent.AccountCreated,
                actor.UserId,
                actor.Role,
                tenantId,
                nameof(AppUser),
                account.Id,
                actor.Ip,
                new Dictionary<string, string>
                {
                    ["email"] = account.Email.Value,
                    ["role"] = AccountMapper.ToWireFormat(account.Role),
                    ["staffId"] = staff.Id,
                    ["staffName"] = staff.FullName
                }),
            cancellationToken);

        return new GrantStaffAccountResult(StaffMapper.ToDto(staff, account), generated);
    }
}
