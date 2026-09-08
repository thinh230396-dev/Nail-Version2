using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories.Salon;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Repositories.Salon;

/// <summary>
/// Bản cài đặt <see cref="ICustomerRepository"/> bằng EF Core.
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(c =&gt; c.TenantId == ...)</c>: điều kiện đó
/// đã được <c>NailDbContext</c> gắn sẵn cho mọi entity mang <c>ITenantOwned</c> (BR-ISO-002).
/// Điều đó đúng với cả bảng hóa đơn mà hai phép tổng hợp bên dưới đọc tới, nên tổng chi tiêu
/// không thể vô tình cộng nhầm hóa đơn của tiệm khác.
/// </para>
/// </summary>
public sealed class CustomerRepository(NailDbContext db) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Customers
            // Khách đang hoạt động lên trước, hồ sơ đã ngừng dồn xuống cuối, trong mỗi nhóm
            // sắp theo tên. Hồ sơ chưa có tên dồn xuống cuối nhóm của nó — BR-CUS-003 cho
            // phép tạo khách chỉ với số điện thoại, và ở quầy đông thì đó là chuyện thường,
            // nhưng một dãy số không tên nằm đầu bảng thì không giúp ai tìm được gì.
            //
            // ⚠️ Cố ý KHÔNG viết `customer.FullName ?? customer.Phone.Value`: đó chính là
            // cái bẫy mà CustomerConfiguration đã cảnh báo — EF Core không dịch được lời gọi
            // vào bên trong một value object đã đi qua bộ chuyển đổi, và câu truy vấn sẽ ném
            // lỗi lúc chạy chứ không phải lúc biên dịch. So cả đối tượng thì dịch được.
            .OrderBy(customer => customer.Status == CustomerStatus.Inactive)
            .ThenBy(customer => customer.FullName == null)
            .ThenBy(customer => customer.FullName)
            .ThenBy(customer => customer.Phone)
            .ToListAsync(cancellationToken);

    public async Task<Customer?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public async Task<bool> PhoneExistsAsync(
        string phone, string? exceptCustomerId, CancellationToken cancellationToken = default)
    {
        // Dựng value object TRƯỚC rồi so cả đối tượng. Viết `customer.Phone.Value == phone`
        // ngay trong biểu thức LINQ sẽ ném lỗi lúc chạy: EF Core không dịch được lời gọi vào
        // bên trong một value object đã đi qua bộ chuyển đổi. Dựng ở đây còn được thêm một
        // việc: chuỗi người dùng gõ được chuẩn hóa đúng cách PhoneNumber chuẩn hóa lúc ghi,
        // nên "090 123 4567" và "0901234567" tìm ra cùng một hồ sơ.
        var normalized = PhoneNumber.Create(phone);

        return await db.Customers.AnyAsync(
            customer => customer.Phone == normalized
                        && (exceptCustomerId == null || customer.Id != exceptCustomerId),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, CustomerSpendSummary>> ListSpendSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await TotalsByCustomer(PaidInvoices()).ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.CustomerId,
            row => new CustomerSpendSummary(row.CustomerId, row.Visits, row.TotalSpent, row.LastVisitAt));
    }

    public async Task<CustomerSpendSummary> GetSpendSummaryAsync(
        string customerId, CancellationToken cancellationToken = default)
    {
        // ⚠️ Phép lọc theo khách phải nằm TRƯỚC lệnh gom nhóm. Đặt sau — dạng
        // `TotalsByCustomer(...).FirstOrDefault(row => row.CustomerId == id)` — thì SQL
        // Server phải lọc trên kết quả đã gom, và EF Core từ chối dịch, ném lỗi lúc chạy.
        var row = await TotalsByCustomer(
                PaidInvoices().Where(invoice => invoice.CustomerId == customerId))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? CustomerSpendSummary.Empty(customerId)
            : new CustomerSpendSummary(row.CustomerId, row.Visits, row.TotalSpent, row.LastVisitAt);
    }

    public async Task<IReadOnlyList<CustomerVisit>> ListVisitsAsync(
        string customerId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await PaidInvoices()
            .Where(invoice => invoice.CustomerId == customerId)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .Take(limit)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.Code,
                invoice.CreatedAt,
                BranchName = invoice.Branch != null ? invoice.Branch.Name : invoice.BranchId,
                StaffName = invoice.Staff != null ? invoice.Staff.FullName : null,
                invoice.Total,
                ServiceNames = invoice.Lines.Select(line => line.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new CustomerVisit(
            row.Id, row.Code, row.CreatedAt, row.BranchName, row.StaffName, row.Total, row.ServiceNames))];
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await db.Customers.AddAsync(customer, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        db.Customers.Update(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Quyết định 42 — chỉ hóa đơn <b>đã trả đủ</b> mới được tính vào chi tiêu của khách.
    /// <para>
    /// Đây là chỗ duy nhất định nghĩa "khách đã chi bao nhiêu", và hai phép tổng hợp cùng
    /// danh sách lần ghé đều đi qua nó. Để mỗi hàm tự viết điều kiện là mở đường cho ngăn
    /// chi tiết nói một con số khác với con số trên bảng.
    /// </para>
    /// <para>
    /// Cố ý KHÁC công thức doanh thu ở BR-REV-001, thứ sẽ viết ở ngày 16: doanh thu là tiền
    /// tiệm thực thu nên phải trừ tip, còn đây là tiền khách đã trả nên tip nằm trong.
    /// </para>
    /// </summary>
    private IQueryable<SalesInvoice> PaidInvoices()
        => db.SalesInvoices.Where(invoice => invoice.Status == SalesInvoiceStatus.Paid);

    /// <summary>
    /// Gom hóa đơn theo khách. Nhận nguồn từ bên ngoài thay vì tự dựng, để hai người gọi
    /// dùng chung đúng một phép gom: một người đưa vào cả tiệm, người kia đưa vào đúng một
    /// khách đã lọc sẵn.
    /// </summary>
    private static IQueryable<SpendRow> TotalsByCustomer(IQueryable<SalesInvoice> invoices)
        => invoices
            .GroupBy(invoice => invoice.CustomerId)
            .Select(group => new SpendRow(
                group.Key,
                group.Count(),
                group.Sum(invoice => invoice.Total),
                group.Max(invoice => (DateTimeOffset?)invoice.CreatedAt)));

    /// <summary>Hình dạng trung gian của câu <c>GROUP BY</c>, chỉ tồn tại trong lớp này.</summary>
    private sealed record SpendRow(
        string CustomerId, int Visits, long TotalSpent, DateTimeOffset? LastVisitAt);
}
