using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Shared;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// Dịch lỗi vi phạm chỉ mục duy nhất của SQL Server thành đúng lỗi 422 mà phép kiểm ở use case
/// đã trả nếu hai request không đến cùng lúc.
/// <para>
/// Use case vẫn kiểm trước — để thông báo sớm, gắn đúng ô, không mở giao dịch vô ích. Nhưng giữa
/// phép kiểm và lệnh ghi luôn có một khe: hai quầy cùng qua phép kiểm, chỉ mục duy nhất chặn bản
/// thứ hai. Chặn là đúng; trả HTTP 500 cho nó thì không. Ở đây là lưới cuối cho đúng khe ấy.
/// </para>
/// <para>
/// Tra chỉ mục bị vi phạm trong <b>model EF</b> — entity nào, cột nào — thay vì cứng hóa tên chỉ
/// mục, để đổi quy ước đặt tên không lặng lẽ làm hỏng bảng dịch. Chỉ mục không có trong bảng dưới
/// đây (số hóa đơn, khóa chính…) không được dịch: vi phạm chúng là lỗi thật, phải hiện là 500 và
/// nằm trong log.
/// </para>
/// </summary>
internal static partial class UniqueConstraintTranslator
{
    // 2601: chỉ mục duy nhất. 2627: ràng buộc UNIQUE / khóa chính.
    private static readonly int[] DuplicateKeyErrors = [2601, 2627];

    private static readonly Dictionary<(Type Entity, string Property), (string Field, string Message)> Messages = new()
    {
        [(typeof(Customer), nameof(Customer.Phone))] =
            ("phone", "Tiệm đã có khách mang số điện thoại này. Hãy mở hồ sơ hiện có thay vì tạo mới."),
        [(typeof(Branch), nameof(Branch.Name))] = ("name", "Tiệm đã có chi nhánh cùng tên."),
        [(typeof(Service), nameof(Service.Name))] = ("name", "Tiệm đã có dịch vụ cùng tên."),
        [(typeof(Tenant), nameof(Tenant.Code))] = ("code", "Mã tiệm này đã được dùng."),
        [(typeof(Package), nameof(Package.Name))] = ("name", "Đã có gói dịch vụ cùng tên."),
        // Tên ô là tên thường gặp nhất. Vài biểu mẫu đặt tên riêng (adminEmail ở màn tạo tiệm): ở đó
        // lỗi vẫn là 422 với đúng câu chữ, chỉ không gắn vào ô — chấp nhận được cho một va chạm cần
        // hai người gõ cùng một email trong cùng một khoảnh khắc.
        [(typeof(AppUser), nameof(AppUser.Email))] = ("email", "Email này đã được dùng cho một tài khoản khác."),
        [(typeof(AppUser), nameof(AppUser.Username))] = ("username", "Tên đăng nhập này đã có người dùng."),
        [(typeof(AppUser), nameof(AppUser.StaffId))] = ("staffId", "Nhân viên này đã có tài khoản đăng nhập.")
    };

    public static DomainException? TryTranslate(DbUpdateException exception, IModel model)
    {
        if (exception.InnerException is not SqlException { } sql || !DuplicateKeyErrors.Contains(sql.Number))
            return null;

        var name = QuotedName().Matches(sql.Message)
            .Select(match => match.Groups[1].Value)
            .FirstOrDefault(candidate => !candidate.StartsWith("dbo.", StringComparison.Ordinal));

        if (name is null) return null;

        var index = model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .FirstOrDefault(candidate => candidate.IsUnique && candidate.GetDatabaseName() == name);

        if (index is null) return null;

        return Key(index) is { } key && Messages.TryGetValue(key, out var entry)
            ? DomainException.ForField(entry.Field, entry.Message)
            : null;
    }

    /// <summary>Những cặp (entity, cột) có câu dịch — để phép thử đối chiếu với model.</summary>
    internal static IReadOnlyCollection<(Type Entity, string Property)> Covered => Messages.Keys;

    /// <summary>
    /// Cột "của người dùng" trong một chỉ mục duy nhất. Chỉ mục theo tiệm luôn mang TenantId đi kèm;
    /// cột đáng nói với người dùng là cột còn lại.
    /// </summary>
    internal static (Type Entity, string Property)? Key(IReadOnlyIndex index)
        => index.Properties
                .Select(candidate => candidate.Name)
                .LastOrDefault(candidate => candidate != nameof(ITenantOwned.TenantId)) is { } property
            ? (index.DeclaringEntityType.ClrType, property)
            : null;

    // Tên đối tượng trong câu lỗi SQL Server nằm giữa hai dấu nháy đơn: 'dbo.Customers',
    // 'IX_Customers_TenantId_Phone'.
    [GeneratedRegex("'([^']+)'")]
    private static partial Regex QuotedName();
}
