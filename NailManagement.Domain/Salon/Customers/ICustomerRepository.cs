using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Customers;

/// <summary>
/// Tổng chi tiêu và số lượt đến của một khách, tổng hợp từ hóa đơn bán hàng.
/// <para>
/// BR-CUS-009 — hai con số này <b>không có cột nào</b> trong bảng khách hàng. Chúng được
/// tính lúc đọc, và đó là lý do chúng đi thành một bản ghi riêng thay vì thành thuộc tính
/// của <see cref="Customer"/>: gắn vào entity là mời người khác gán giá trị rồi lưu xuống.
/// </para>
/// </summary>
/// <param name="PaidInvoiceCount">
/// Số hóa đơn đã trả đủ. Đây cũng chính là số lượt đến mà màn hình hiển thị: một lần ghé
/// tiệm có trả tiền là một hóa đơn.
/// </param>
/// <param name="LastVisitAt">Ngày lập hóa đơn gần nhất trong số đó. Rỗng nghĩa là khách chưa từng trả tiền lần nào.</param>
public sealed record CustomerSpendSummary(
    string CustomerId,
    int PaidInvoiceCount,
    long TotalSpent,
    DateTimeOffset? LastVisitAt)
{
    /// <summary>Khách chưa phát sinh hóa đơn nào — hạng <c>NEW</c> theo BR-CUS-007.</summary>
    public static CustomerSpendSummary Empty(string customerId) => new(customerId, 0, 0, null);
}

/// <summary>
/// Một lần ghé tiệm có trả tiền, đọc từ hóa đơn đã thanh toán.
/// <para>
/// Cố ý không phải là lịch hẹn: lịch hẹn có thể bị hủy hoặc khách không đến, còn hóa đơn
/// đã trả đủ thì chắc chắn là một lần khách thật sự được phục vụ.
/// </para>
/// </summary>
/// <param name="BranchName">
/// Tên chi nhánh, không phải mã. Khác quy ước của <c>StaffDto</c> — thứ trả mã để màn hình
/// tự ghép tên — vì đây là một <b>bản đọc lịch sử</b>: ngăn chi tiết khách chỉ cần đọc, và
/// bắt nó nạp thêm danh sách chi nhánh cùng danh sách nhân viên chỉ để dịch vài dòng lịch
/// sử là ba lời gọi mạng cho một việc mà một phép nối đã làm xong.
/// </param>
public sealed record CustomerVisit(
    string InvoiceId,
    string InvoiceCode,
    DateTimeOffset IssuedAt,
    string BranchName,
    string? StaffName,
    long Total,
    IReadOnlyList<string> ServiceNames);

/// <summary>
/// Cổng ra kho dữ liệu khách hàng.
/// <para>
/// Không hàm nào nhận mã tiệm, cùng lý do đã ghi ở <see cref="IServiceRepository"/>: khách
/// hàng mang <c>ITenantOwned</c> nên bộ lọc toàn cục ở <c>NailDbContext</c> đã gắn sẵn điều
/// kiện theo tiệm đang làm việc (BR-ISO-002).
/// </para>
/// <para>
/// Cũng không hàm nào nhận mã chi nhánh: BR-CUS-001 quy định khách thuộc tiệm và dùng chung
/// cho mọi chi nhánh. Một tham số chi nhánh ở đây sẽ gợi ý một cách chia dữ liệu mà hệ thống
/// không có — và tệ hơn, sẽ khiến lễ tân không tra được khách đã từng đến chi nhánh khác.
/// </para>
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Toàn bộ khách của tiệm đang làm việc, <b>kể cả hồ sơ đã ngừng hoạt động</b>.
    /// <para>
    /// BR-DEL-003 — hồ sơ <c>INACTIVE</c> vẫn phải hiện đúng tên trong hóa đơn cũ, và màn
    /// quản lý cần nhìn thấy chúng thì mới có đường bật lại.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken = default);

    Task<Customer?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhiều hồ sơ khách theo mã, trong tiệm đang làm việc, bằng MỘT truy vấn — cho các màn danh sách
    /// cần hiện tên thay vì mã. Mã không tìm thấy thì vắng mặt trong kết quả; bản đọc không
    /// được theo dõi thay đổi.
    /// </summary>
    Task<IReadOnlyDictionary<string, Customer>> ListByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-CUS-002 — số điện thoại duy nhất <b>trong phạm vi một tiệm</b>. Kiểm ở đây trước
    /// khi ghi để người dùng nhận thông báo gắn đúng ô nhập, thay vì một lỗi ràng buộc thô
    /// từ chỉ số duy nhất của database.
    /// </summary>
    /// <param name="exceptCustomerId">Bỏ qua chính hồ sơ đang sửa, nếu không thì nó tự trùng số với mình.</param>
    Task<bool> PhoneExistsAsync(
        string phone, string? exceptCustomerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổng chi tiêu của <b>mọi</b> khách trong tiệm, gom bằng một câu <c>GROUP BY</c>.
    /// <para>
    /// Một câu cho cả danh sách chứ không phải mỗi hồ sơ một câu: màn khách hàng hiện hạng
    /// khách ngay trên bảng, nên hỏi từng dòng là hỏi hai mươi lần cho một lần mở màn hình.
    /// </para>
    /// <para>
    /// Khách chưa có hóa đơn nào <b>không xuất hiện</b> trong kết quả — người gọi phải coi
    /// chỗ vắng mặt là <see cref="CustomerSpendSummary.Empty"/>.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, CustomerSpendSummary>> ListSpendSummariesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Tổng chi tiêu của đúng một khách, cho ngăn chi tiết.</summary>
    Task<CustomerSpendSummary> GetSpendSummaryAsync(
        string customerId, CancellationToken cancellationToken = default);

    /// <summary>Những lần ghé gần nhất của một khách, mới nhất trước.</summary>
    Task<IReadOnlyList<CustomerVisit>> ListVisitsAsync(
        string customerId, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
