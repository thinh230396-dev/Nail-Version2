using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories;

namespace NailManagement.Infrastructure.Persistence.Repositories;

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

        var counter = await db.InvoiceCounters
            .FirstOrDefaultAsync(row => row.BusinessDate == businessDate, cancellationToken);

        if (counter is null)
        {
            counter = InvoiceCounter.StartOfDay(tenantId, businessDate);
            await db.InvoiceCounters.AddAsync(counter, cancellationToken);
        }

        var number = counter.NextNumber();

        await db.SaveChangesAsync(cancellationToken);

        return InvoiceCounter.FormatCode(businessDate, number);
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
    /// Hình dạng chung của hai đường đọc: hóa đơn kèm dòng hàng, dòng thu tiền, chi nhánh, khách
    /// và kỹ thuật viên.
    /// <para>
    /// <c>AsSplitQuery</c> vì đây là <b>hai</b> phép nối một–nhiều lồng cùng ba phép nối một–một.
    /// Gộp vào một câu thì mỗi hóa đơn bị nhân lên bằng số dòng hàng nhân số dòng thu tiền, và cả
    /// hồ sơ khách lẫn hồ sơ nhân viên bị chép lại ở từng dòng của tích ấy.
    /// </para>
    /// </summary>
    private IQueryable<SalesInvoice> Readable()
        => db.SalesInvoices
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Branch)
            .Include(invoice => invoice.Customer)
            .Include(invoice => invoice.Staff)
            .AsSplitQuery();
}
