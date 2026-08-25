using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities;
using NailManagement.Domain.Enums;
using NailManagement.Domain.Repositories;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>
/// Tạo một tiệm mới — BR-TENANT-004/005.
/// <para>
/// Đây là use case nặng nhất của tầng nền tảng vì nó chạm vào <b>năm bảng</b> trong một lần:
/// tiệm, chi nhánh chính, tài khoản chủ tiệm, dòng liên kết tài khoản–tiệm, và hóa đơn đăng
/// ký. Cả năm nằm trong một giao dịch. Hỏng ở bước thứ tư mà ba bước đầu đã ghi thì hệ thống
/// còn lại một tiệm không ai quản lý được, và không màn hình nào sửa được tình trạng đó.
/// </para>
/// <para>
/// Số lượng cổng phụ thuộc lớn là hệ quả trực tiếp của việc trên, không phải dấu hiệu use
/// case làm quá nhiều việc: đây đúng là <i>một</i> thao tác dưới mắt người dùng — bấm "Tạo
/// tiệm" một lần.
/// </para>
/// </summary>
public sealed class CreateTenantUseCase(
    ITenantRepository tenants,
    IBranchRepository branches,
    IPackageRepository packages,
    IUserRepository users,
    IUserTenantRepository userTenants,
    ISubscriptionInvoiceRepository subscriptionInvoices,
    TenantReadService reader,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    IPasswordGenerator passwordGenerator,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Tên chi nhánh mặc định khi Superadmin không nhập gì — BR-BRANCH-001.</summary>
    private const string DefaultPrimaryBranchName = "Chi nhánh chính";

    public async Task<CreateTenantResult> ExecuteAsync(
        CreateTenantCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var code = Guard.NotEmpty(command.Code, "code", "Mã tiệm", 32).ToUpperInvariant();

        // BR-VAL-001 — kiểm mã trùng ở đây thay vì để database từ chối, vì lỗi ràng buộc thô
        // không nói được ô nhập nào sai và frontend không gắn được thông báo vào đúng chỗ.
        if (await tenants.CodeExistsAsync(code, cancellationToken))
            throw DomainException.ForField("code", $"Mã tiệm {code} đã được dùng.");

        var package = await packages.FindByIdAsync(command.PackageId ?? string.Empty, cancellationToken)
            ?? throw DomainException.ForField("packageId", "Không tìm thấy gói dịch vụ.");

        // BR-SUB-002 — gói đã ngừng bán vẫn phục vụ tiệm đang dùng, nhưng không nhận đăng ký mới.
        if (!package.AcceptsNewSubscriptions())
            throw DomainException.ForField("packageId", $"Gói {package.Name} đã ngừng bán, không đăng ký mới được.");

        var owner = await ResolveOwnerAsync(command.Owner, now, cancellationToken);

        var tenantId = $"TEN-{code}";

        // BR-SUB-004 — giá và số phiên bản gói chốt tại đây. Sau này Superadmin sửa bảng giá
        // thì tiệm này vẫn trả đúng con số đã thỏa thuận hôm nay.
        var tenant = Tenant.Create(
            tenantId,
            code,
            command.Name,
            package.Id,
            package.Price,
            package.Version,
            PackageMapper.ParseBillingCycle(command.BillingCycle),
            command.IsTrial,
            command.ExpiresAt,
            command.Address,
            command.Phone,
            command.ContactEmail,
            now,
            string.IsNullOrWhiteSpace(command.Timezone) ? "Asia/Ho_Chi_Minh" : command.Timezone.Trim());

        var branch = Branch.Create(
            $"BRN-{code}-01",
            tenantId,
            string.IsNullOrWhiteSpace(command.PrimaryBranchName)
                ? DefaultPrimaryBranchName
                : command.PrimaryBranchName,
            command.PrimaryBranchCode,
            command.Address,
            command.Phone,
            isPrimary: true,
            now);

        var detail = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await tenants.AddAsync(tenant, ct);
            await branches.AddAsync(branch, ct);

            if (owner.IsNew) await users.AddAsync(owner.User, ct);

            // BR-AUTH-023 — quyền quản lý tiệm nằm ở bảng nối này, không phải ở một cột trên
            // tài khoản. Đó cũng là thứ mà BR-AUTH-026 kiểm mỗi lần chủ tiệm đổi tiệm.
            await userTenants.LinkAsync(owner.User.Id, tenantId, now, ct);

            // BR-SUB-011 — tiệm dùng thử chưa phải trả tiền. Phát hành hóa đơn cho họ là dựng
            // ra một khoản nợ không có thật, và tiệm sẽ hiện "còn hóa đơn chưa thanh toán".
            if (!command.IsTrial) await IssueSubscriptionInvoiceAsync(tenant, package, now, ct);

            // Đọc lại tiệm vừa ghi thay vì mô tả thẳng đối tượng trong tay: Tenant.Create chỉ
            // đặt khóa ngoại PackageId chứ không gắn đối tượng gói, mà phần mô tả thì cần cả
            // hạn mức lẫn tên gói. Lượt đọc này chạy trong cùng giao dịch nên nó thấy được
            // những gì vừa ghi, và trả về đúng đối tượng đang được theo dõi.
            var saved = await tenants.FindByIdAsync(tenantId, ct)
                ?? throw new InvalidOperationException($"Vừa ghi tiệm {tenantId} nhưng đọc lại không thấy.");

            return await reader.DescribeAsync(saved, now, ct);
        }, cancellationToken);

        // Nhật ký ghi SAU khi giao dịch chốt. Nằm trong giao dịch thì nó sẽ bị hủy theo khi
        // có lỗi, mà một dòng "đã tạo tiệm" cho tiệm chưa từng tồn tại còn tệ hơn là không
        // có dòng nào — BR-AUD-001 muốn nhật ký kể lại đúng những gì đã xảy ra thật.
        await audit.RecordAsync(
            new AuditEntry(
                AuditEvent.TenantCreated,
                actor.UserId,
                actor.Role,
                tenantId,
                nameof(Tenant),
                tenantId,
                actor.Ip,
                new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["name"] = tenant.Name,
                    ["package"] = package.Name,
                    ["ownerEmail"] = owner.User.Email.Value
                }),
            cancellationToken);

        if (owner.IsNew)
        {
            await audit.RecordAsync(
                new AuditEntry(
                    AuditEvent.AccountCreated,
                    actor.UserId,
                    actor.Role,
                    tenantId,
                    nameof(AppUser),
                    owner.User.Id,
                    actor.Ip,
                    new Dictionary<string, string>
                    {
                        ["email"] = owner.User.Email.Value,
                        ["role"] = AccountMapper.ToWireFormat(owner.User.Role)
                    }),
                cancellationToken);
        }

        return new CreateTenantResult(detail, owner.GeneratedPassword);
    }

    /// <summary>
    /// BR-TENANT-004 — hai đường vào: tạo tài khoản chủ tiệm mới, hoặc gán một tài khoản đã có.
    /// <para>
    /// Tài khoản mới CHƯA được ghi xuống ở đây, chỉ dựng trong bộ nhớ. Lệnh ghi nằm trong
    /// giao dịch cùng với tiệm, vì tài khoản chủ tiệm không có tiệm nào là một bản ghi vô nghĩa.
    /// </para>
    /// </summary>
    private async Task<ResolvedOwner> ResolveOwnerAsync(
        TenantOwnerCommand command, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var mode = (command.Mode ?? string.Empty).Trim().ToLowerInvariant();

        if (mode == "existing")
        {
            var existing = await users.FindByIdAsync(command.ExistingUserId ?? string.Empty, cancellationToken)
                ?? throw DomainException.ForField("adminEmail", "Không tìm thấy tài khoản chủ tiệm đã chọn.");

            // BR-AUTH-010 — chỉ tài khoản mang vai trò chủ tiệm mới được giao quản lý tiệm.
            // Gán nhầm một tài khoản lễ tân sẽ cho họ quyền của chủ tiệm ở tiệm mới.
            if (existing.Role != UserRole.TenantAdmin)
                throw DomainException.ForField("adminEmail", "Tài khoản được chọn không phải tài khoản chủ tiệm.");

            if (!existing.IsActive())
                throw DomainException.ForField("adminEmail", "Tài khoản chủ tiệm đang bị khóa hoặc đã vô hiệu hóa.");

            return new ResolvedOwner(existing, IsNew: false, GeneratedPassword: null);
        }

        if (mode != "new")
            throw DomainException.ForField("adminCreationMode", "Chưa chọn cách cấp tài khoản chủ tiệm.");

        var email = Email.Create(command.Email ?? string.Empty, "adminEmail");

        // BR-VAL-001 — email duy nhất toàn hệ thống. Tra bằng chính hàm mà lúc đăng nhập
        // dùng, nên phép so khớp hoa thường ở hai nơi không thể lệch nhau.
        if (await users.FindByIdentifierAsync(email.Value, cancellationToken) is not null)
            throw DomainException.ForField("adminEmail", "Email này đã được dùng cho một tài khoản khác.");

        var username = Guard.Optional(command.Username, "adminUsername", "Tên đăng nhập", 64)?.ToLowerInvariant();

        if (username is not null
            && await users.FindByIdentifierAsync(username, cancellationToken) is not null)
        {
            throw DomainException.ForField("adminUsername", "Tên đăng nhập này đã có người dùng.");
        }

        // Quyết định 3 chốt ngày 25/08: bỏ trống thì máy chủ sinh, và trả lại đúng một lần.
        // Hệ thống không gửi email (mục 9.4 của lộ trình) nên không trả về nghĩa là tài
        // khoản vừa tạo không ai đăng nhập được.
        var generated = string.IsNullOrWhiteSpace(command.Password) ? passwordGenerator.Generate() : null;
        var raw = RawPassword.Create(generated ?? command.Password!, "adminPassword");
        var hashed = hasher.Hash(raw);

        var user = AppUser.Create(
            $"USR-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            email,
            username,
            hashed.Hash,
            hashed.Salt,
            UserRole.TenantAdmin,
            Guard.NotEmpty(command.DisplayName, "adminName", "Tên chủ tiệm", 160),
            now);

        return new ResolvedOwner(user, IsNew: true, generated);
    }

    /// <summary>
    /// BR-INV-030 — tạo tiệm là một trong ba lý do phát hành hóa đơn đăng ký.
    /// <para>
    /// Số thứ tự lấy từ <b>dãy chung toàn hệ thống</b>, vì cột <c>Code</c> mang chỉ số duy
    /// nhất không giới hạn theo tiệm: đây là hóa đơn do SalonSys phát hành, nên nó nằm trong
    /// một dãy số của người bán. Đếm theo từng tiệm sẽ trùng số ngay với dữ liệu đã có.
    /// </para>
    /// <para>
    /// Phép đếm chạy trong cùng giao dịch với lệnh ghi. Hai lần tạo tiệm <i>đồng thời</i> vẫn
    /// có thể cùng đọc ra một số ở mức cô lập mặc định của SQL Server; khi đó bản ghi thứ hai
    /// bị chỉ số duy nhất từ chối và cả giao dịch cuộn lại — hỏng thấy được, không phải hỏng
    /// âm thầm. Ở phạm vi MVP chỉ có một tài khoản Superadmin nên tình huống đó không xảy ra.
    /// </para>
    /// </summary>
    private async Task IssueSubscriptionInvoiceAsync(
        Tenant tenant, Package package, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sequence = await subscriptionInvoices.CountAsync(cancellationToken) + 1;

        var invoice = SubscriptionInvoice.Issue(
            $"SUB-{tenant.Code}-{sequence:D3}",
            $"DK-{now:yyyyMM}-{sequence:D3}",
            tenant.Id,
            tenant.Name,
            package.Id,
            package.Name,
            tenant.SubscriptionPrice,
            tenant.BillingCycle,
            now,
            tenant.ExpiresAt,
            now.AddDays(7),
            "Đăng ký mới",
            now);

        await subscriptionInvoices.AddAsync(invoice, cancellationToken);
    }

    private sealed record ResolvedOwner(AppUser User, bool IsNew, string? GeneratedPassword);
}
