using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Shared;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Infrastructure.Persistence.Salon.Invoices;

/// <summary>
/// Bản cài đặt <see cref="ISalesInvoiceRepository"/> bằng EF Core.
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(i =&gt; i.TenantId == ...)</c>: điều kiện đó đã
/// được <c>NailDbContext</c> gắn sẵn cho mọi entity mang <c>ITenantOwned</c> (BR-ISO-002), và
/// hóa đơn cùng hai bảng con của nó đều mang giao diện ấy.
/// </para>
/// </summary>
public sealed class SalesInvoiceRepository(NailDbContext db, ITenantContext tenantContext)
    : ISalesInvoiceRepository
{
    public async Task<IReadOnlyList<SalesInvoice>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        // Lọc theo GIỜ LẬP hóa đơn, không theo giờ thu tiền: một hóa đơn thu làm nhiều lần
        // (BR-PAY-004) sẽ có nhiều mốc thu, nên "hóa đơn của ngày nào" chỉ có một câu trả lời
        // duy nhất là ngày quầy lập nó. Báo cáo doanh thu ở BR-REV-001 thì ngược lại — nó đếm
        // theo tiền thực thu, nên sẽ đọc bảng dòng thu chứ không dùng hàm này.
        var query = Readable().Where(invoice => invoice.CreatedAt >= from && invoice.CreatedAt < to);

        // BR-ISO-004 — hóa đơn bán hàng cùng nhóm với lịch hẹn và nhân viên: lễ tân chỉ thấy chi
        // nhánh mình. Hợp lệ ở đây vì chi nhánh không phải ranh giới cách ly — bộ lọc theo tiệm
        // đã làm việc đó — mà chỉ là một ô trong ma trận quyền.
        if (branchId is not null)
            query = query.Where(invoice => invoice.BranchId == branchId);

        return await query
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ThenByDescending(invoice => invoice.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesInvoice?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await Readable().FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

    public async Task<SalesInvoice?> FindOpenByAppointmentAsync(
        string appointmentId, CancellationToken cancellationToken = default)
        => await db.SalesInvoices
            .Where(invoice => invoice.AppointmentId == appointmentId
                              && invoice.Status != SalesInvoiceStatus.Cancelled)
            .OrderBy(invoice => invoice.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string> NextCodeAsync(
        DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        // Bộ đếm là entity duy nhất trong hệ thống có khóa chính ghép và không có cột Id, nên nó
        // được tra bằng cả hai mảnh khóa. Mã tiệm phải viết tường minh ở đây: bộ lọc toàn cục
        // lọc được phần đọc, nhưng dòng mới thì vẫn cần biết nó thuộc tiệm nào.
        var tenantId = tenantContext.ActiveTenantId
            ?? throw new InvalidOperationException(
                "Cấp số hóa đơn khi request chưa có tiệm đang làm việc. "
                + "Endpoint gọi tới đây phải nằm sau RequirePermission.");

        // MỘT câu lệnh vừa tăng vừa trả số, thay cho đọc–cộng–ghi trong bộ nhớ.
        //
        // Đọc rồi ghi là hai bước, và giao dịch READ COMMITTED không khóa gì giữa hai bước ấy:
        // hai quầy cùng đọc số 5, cùng ghi số 6, và quầy sau đụng chỉ mục duy nhất trên số hóa
        // đơn — HTTP 500 ngay trước mặt khách. Đầu ngày còn tệ hơn: hai quầy cùng thấy "chưa có
        // bộ đếm" và cùng chèn, một bên đụng khóa chính.
        //
        // MERGE với HOLDLOCK giữ khóa phạm vi trên đúng cặp (tiệm, ngày) từ lúc kiểm tới lúc ghi,
        // nên phép "chưa có thì tạo, có rồi thì tăng" là nguyên tử. Khóa ấy sống tới hết giao dịch
        // lập hóa đơn, nên các quầy xếp hàng ở đây trong vài mili giây và số hóa đơn liền nhau,
        // không nhảy cóc khi một lần lập hóa đơn bị cuộn ngược.
        //
        // SQL viết tay vì EF không có phép upsert nguyên tử. Tham số hóa qua FormattableString —
        // không có chuỗi nào của người dùng được ghép vào câu lệnh.
        var numbers = await db.Database
            .SqlQuery<int>($"""
                MERGE [InvoiceCounters] WITH (HOLDLOCK) AS [target]
                USING (SELECT {tenantId} AS [TenantId], {businessDate} AS [BusinessDate]) AS [source]
                    ON [target].[TenantId] = [source].[TenantId]
                   AND [target].[BusinessDate] = [source].[BusinessDate]
                WHEN MATCHED THEN
                    UPDATE SET [LastNumber] = [target].[LastNumber] + 1
                WHEN NOT MATCHED THEN
                    INSERT ([TenantId], [BusinessDate], [LastNumber])
                    VALUES ([source].[TenantId], [source].[BusinessDate], 1)
                OUTPUT inserted.[LastNumber] AS [Value];
                """)
            .ToListAsync(cancellationToken);

        return InvoiceCounter.FormatCode(businessDate, numbers.Single());
    }

    public async Task AddAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        await db.SalesInvoices.AddAsync(invoice, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        // Cố ý KHÔNG gọi db.SalesInvoices.Update(...). Hàm đó đánh dấu cả cây đối tượng là đã
        // sửa, nên những dòng hàng mà ClearLines vừa bỏ đi sẽ không được nhận ra là mồ côi và
        // không bị xóa — cùng cái bẫy đã gặp ở kho dữ liệu lịch hẹn.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Hình dạng chung của hai đường đọc: hóa đơn kèm dòng hàng và dòng thu tiền — hai bảng con
    /// của chính aggregate này. Tên chi nhánh, khách và kỹ thuật viên thuộc về aggregate khác;
    /// tầng Application đọc chúng theo lô qua <c>SalonDirectoryReader</c>.
    /// <para>
    /// <c>AsSplitQuery</c> vì đây là <b>hai</b> phép nối một–nhiều. Gộp vào một câu thì mỗi hóa
    /// đơn bị nhân lên bằng số dòng hàng nhân số dòng thu tiền.
    /// </para>
    /// </summary>
    private IQueryable<SalesInvoice> Readable()
        => db.SalesInvoices
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.Payments)
            .AsSplitQuery();
}
