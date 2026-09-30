using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Application.Common;

/// <summary>
/// Chi nhánh, khách và nhân viên mà một nhóm lịch hẹn hoặc hóa đơn trỏ tới — đọc sẵn để dựng
/// DTO có tên thay vì chỉ có mã.
/// <para>
/// Lịch hẹn và hóa đơn chỉ giữ <b>mã</b> của các aggregate khác, không giữ thuộc tính điều
/// hướng. Tên hiển thị vì vậy được nối ở tầng đọc, bằng đúng ba truy vấn cho cả một danh sách
/// — không phải một lượt cho mỗi dòng, và không phải một câu JOIN kéo cả hồ sơ khách vào
/// entity hóa đơn.
/// </para>
/// </summary>
public sealed class SalonDirectory(
    IReadOnlyDictionary<string, Branch> branches,
    IReadOnlyDictionary<string, Customer> customers,
    IReadOnlyDictionary<string, Staff> staff)
{
    public Branch Branch(string id)
        => branches.TryGetValue(id, out var found) ? found : throw Missing(nameof(Branch), id);

    public Customer Customer(string id)
        => customers.TryGetValue(id, out var found) ? found : throw Missing(nameof(Customer), id);

    public Staff StaffMember(string id)
        => staff.TryGetValue(id, out var found) ? found : throw Missing(nameof(Staff), id);

    /// <summary>Cho nơi chỉ cần tên để hiển thị và có sẵn cách lùi khi không tìm thấy.</summary>
    public Branch? BranchOrNull(string id) => branches.GetValueOrDefault(id);

    /// <summary>Cho các trường không bắt buộc, như kỹ thuật viên của một hóa đơn bán lẻ.</summary>
    public Staff? StaffMemberOrNull(string? id)
        => id is not null && staff.TryGetValue(id, out var found) ? found : null;

    // Khóa ngoại ghép (mã, tiệm) bảo đảm bản ghi được trỏ tới luôn tồn tại trong cùng tiệm.
    // Tới được đây nghĩa là danh bạ được đọc thiếu — lỗi lập trình, hỏng ngay và nói rõ thay vì
    // lặng lẽ trả ra một dòng không tên.
    private static InvalidOperationException Missing(string kind, string id)
        => new($"{kind} {id} không có trong danh bạ đã đọc. Nơi gọi phải nạp đủ mã trước khi dựng DTO.");
}

/// <summary>Đọc <see cref="SalonDirectory"/> theo các mã được hỏi.</summary>
public interface ISalonDirectoryReader
{
    /// <summary>
    /// Đọc đúng những gì được hỏi — báo cáo doanh thu chỉ cần chi nhánh và nhân viên, khỏi kéo
    /// theo hồ sơ khách của cả năm hóa đơn.
    /// </summary>
    Task<SalonDirectory> LoadAsync(
        IEnumerable<string> branchIds,
        IEnumerable<string> customerIds,
        IEnumerable<string?> staffIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Hai cách hỏi thường gặp, dựng trên <see cref="ISalonDirectoryReader.LoadAsync"/>.</summary>
public static class SalonDirectoryReaderExtensions
{
    public static Task<SalonDirectory> ForInvoicesAsync(
        this ISalonDirectoryReader reader,
        IReadOnlyCollection<SalesInvoice> invoices,
        CancellationToken cancellationToken = default)
        => reader.LoadAsync(
            invoices.Select(invoice => invoice.BranchId),
            invoices.Select(invoice => invoice.CustomerId),
            invoices.Select(invoice => invoice.StaffId),
            cancellationToken);

    public static Task<SalonDirectory> ForAppointmentsAsync(
        this ISalonDirectoryReader reader,
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken = default)
        => reader.LoadAsync(
            appointments.Select(appointment => appointment.BranchId),
            appointments.Select(appointment => appointment.CustomerId),
            appointments.Select(appointment => (string?)appointment.StaffId),
            cancellationToken);
}

/// <summary>Bản cài đặt đọc qua ba repository — ba truy vấn cho cả một danh sách.</summary>
public sealed class SalonDirectoryReader(
    IBranchRepository branches,
    ICustomerRepository customers,
    IStaffRepository staffMembers) : ISalonDirectoryReader
{
    public async Task<SalonDirectory> LoadAsync(
        IEnumerable<string> branchIds,
        IEnumerable<string> customerIds,
        IEnumerable<string?> staffIds,
        CancellationToken cancellationToken = default)
    {
        // Ba lượt đọc tuần tự chứ không song song: cả ba dùng chung một DbContext, và DbContext
        // không cho hai truy vấn chạy cùng lúc.
        var branchMap = await branches.ListByIdsAsync(Distinct(branchIds), cancellationToken);
        var customerMap = await customers.ListByIdsAsync(Distinct(customerIds), cancellationToken);
        var staffMap = await staffMembers.ListByIdsAsync(Distinct(staffIds), cancellationToken);

        return new SalonDirectory(branchMap, customerMap, staffMap);
    }

    private static string[] Distinct(IEnumerable<string?> ids)
        => [.. ids.OfType<string>().Distinct(StringComparer.Ordinal)];
}
