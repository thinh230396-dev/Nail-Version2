using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Repositories.Platform;

namespace NailManagement.Infrastructure.Persistence.Repositories.Platform;

/// <summary>
/// Bản cài đặt <see cref="ISubscriptionInvoiceRepository"/> bằng EF Core.
/// <para>
/// Đây là một trong số ít bảng có cột tiệm mà <b>không</b> mang bộ lọc theo tiệm.
/// BR-TENANT-022 là lý do: hóa đơn của tiệm đã xóa mềm vẫn phải đọc được để tính vào doanh
/// thu nền tảng, mà bộ lọc thì sẽ giấu chúng đi.
/// </para>
/// <para>
/// Vì không có bộ lọc tự động, phép cách ly ở đây là <b>thủ công và tường minh</b>: mỗi phép
/// đọc tự nói rõ nó lấy toàn hệ thống hay lấy một tiệm.
/// </para>
/// </summary>
public sealed class SubscriptionInvoiceRepository(NailDbContext db) : ISubscriptionInvoiceRepository
{
    public async Task<IReadOnlyList<SubscriptionInvoice>> ListAllAsync(
        CancellationToken cancellationToken = default)
        /*
          KHÔNG nối sang bảng tiệm, và đó là điều bắt buộc chứ không phải tối ưu.

          Tiệm có bộ lọc xóa mềm toàn cục, nên một phép `Include` sinh ra INNER JOIN và **đánh
          rơi mọi hóa đơn của tiệm đã xóa mềm** — 6 trên 11 dòng trong bộ dữ liệu hiện tại.
          BR-INV-031 nói thẳng hóa đơn đăng ký **không bao giờ xóa được** vì nó là chứng từ tài
          chính; giấu nó đi vì tiệm bị gỡ là làm mất đúng phần lịch sử mà rule ấy đi giữ.

          Không mất gì khi bỏ phép nối: hóa đơn đã lưu sẵn `TenantName` và `PackageName`, chốt
          tại thời điểm lập (BR-SUB-004). Đó cũng là thứ đúng để hiển thị — tên tiệm lúc phát
          hành hóa đơn, không phải tên hiện tại.
        */
        => await db.SubscriptionInvoices
            .OrderByDescending(invoice => invoice.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubscriptionInvoice>> ListByTenantAsync(
        string tenantId, CancellationToken cancellationToken = default)
        /*
          Cùng một câu truy vấn với ListAllAsync, thêm đúng một mệnh đề WHERE — và mệnh đề ấy
          là toàn bộ ranh giới cách ly cho chủ tiệm, vì bảng này không có bộ lọc toàn cục nào
          đỡ phía sau. Viết thành phép đọc riêng thay vì một tham số tùy chọn chính là để chỗ
          này không thể bị bỏ quên.

          Vẫn không Include sang bảng tiệm, cùng lý do ở trên: tiệm của chính người gọi có thể
          đang bị xóa mềm (BR-TENANT-022), và lúc đó họ vẫn phải đọc được hóa đơn của mình.
        */
        => await db.SubscriptionInvoices
            .Where(invoice => invoice.TenantId == tenantId)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task AddAsync(SubscriptionInvoice invoice, CancellationToken cancellationToken = default)
    {
        await db.SubscriptionInvoices.AddAsync(invoice, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await db.SubscriptionInvoices.CountAsync(cancellationToken);
}
