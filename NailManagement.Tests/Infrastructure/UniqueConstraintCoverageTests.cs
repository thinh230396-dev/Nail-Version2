using NailManagement.Domain.Platform.Subscriptions;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Mỗi chỉ mục duy nhất trong model phải được <b>quyết định có chủ đích</b>: hoặc có câu dịch sang
/// lỗi 422 gắn đúng ô, hoặc nằm trong danh sách cố ý để là lỗi 500.
/// <para>
/// Không có phép thử này, ai đó thêm một chỉ mục duy nhất mới — mã chi nhánh chẳng hạn — và lần
/// đầu hai quầy trùng nhau, người dùng nhận "máy chủ gặp sự cố" thay vì "mã này đã có".
/// </para>
/// </summary>
public sealed class UniqueConstraintCoverageTests
{
    /// <summary>
    /// Các giá trị do máy chủ tự cấp. Trùng ở đây nghĩa là bộ cấp số hỏng — lỗi thật, phải là
    /// 500 và phải nằm trong log, không được đội lốt lỗi nhập liệu của người dùng.
    /// </summary>
    private static readonly (Type Entity, string Property)[] IntentionallyUntranslated =
    [
        (typeof(SalesInvoice), nameof(SalesInvoice.Code)),
        (typeof(SubscriptionInvoice), nameof(SubscriptionInvoice.Code))
    ];

    [Fact]
    public void Moi_chi_muc_duy_nhat_deu_co_cau_dich_hoac_duoc_mien_co_chu_dich()
    {
        var undecided = UniqueIndexKeys()
            .Where(key => !UniqueConstraintTranslator.Covered.Contains(key)
                          && !IntentionallyUntranslated.Contains(key))
            .Select(key => $"{key.Entity.Name}.{key.Property}")
            .ToArray();

        Assert.Empty(undecided);
    }

    [Fact]
    public void Moi_cau_dich_deu_ung_voi_mot_chi_muc_co_that()
    {
        // Đổi tên hay bỏ một chỉ mục mà quên bảng dịch thì câu dịch thành chữ chết, và vi phạm
        // của chỉ mục mới lại rơi về 500.
        var keys = UniqueIndexKeys().ToHashSet();

        Assert.All(UniqueConstraintTranslator.Covered, covered => Assert.Contains(covered, keys));
    }

    private static IEnumerable<(Type Entity, string Property)> UniqueIndexKeys()
    {
        // Dựng model không cần database: chỉ đọc cấu hình, không mở kết nối nào.
        using var db = new DesignTimeNailDbContextFactory().CreateDbContext([]);

        return db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Where(index => index.IsUnique)
            .Select(UniqueConstraintTranslator.Key)
            .OfType<(Type Entity, string Property)>()
            .ToArray();
    }
}
