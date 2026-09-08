using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Domain.Repositories.Salon;

/// <summary>
/// Cổng ra kho dữ liệu hóa đơn bán hàng — BR-INV-001, thứ khách trả cho tiệm.
/// <para>
/// ⚠️ Không nhầm với <see cref="ISubscriptionInvoiceRepository"/>, thứ quản hóa đơn tiệm trả
/// cho SalonSys. Hai bảng tách hoàn toàn, và nhầm chúng là nhầm luôn ý nghĩa của mọi con số
/// doanh thu.
/// </para>
/// <para>
/// Không hàm nào nhận mã tiệm: hóa đơn cùng dòng hóa đơn và dòng thu tiền đều mang
/// <c>ITenantOwned</c> nên bộ lọc toàn cục ở <c>NailDbContext</c> đã gắn sẵn điều kiện theo
/// tiệm đang làm việc (BR-ISO-002). Ngược lại, hàm danh sách <b>có</b> nhận mã chi nhánh:
/// BR-ISO-004 xếp hóa đơn bán hàng vào nhóm mà lễ tân chỉ thấy chi nhánh mình.
/// </para>
/// </summary>
public interface ISalesInvoiceRepository
{
    /// <summary>
    /// Hóa đơn lập trong khoảng thời gian, kèm sẵn dòng hóa đơn, dòng thu tiền, khách và
    /// kỹ thuật viên.
    /// <para>
    /// Có khoảng ngày vì cùng lý do với lịch hẹn: hóa đơn cộng dồn mãi theo thời gian, còn
    /// màn hình thì chỉ bao giờ xem một ngày hoặc một tháng.
    /// </para>
    /// </summary>
    /// <param name="branchId">
    /// Giới hạn theo một chi nhánh, hoặc <c>null</c> để lấy cả tiệm. Giá trị luôn đến từ phiên
    /// đăng nhập, không bao giờ từ chuỗi truy vấn.
    /// </param>
    Task<IReadOnlyList<SalesInvoice>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default);

    Task<SalesInvoice?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-INV-010 — hóa đơn đang mở của một lịch hẹn, nếu có.
    /// <para>
    /// Dùng để chặn lập hóa đơn thứ hai cho cùng một lịch: lễ tân bấm "Thanh toán" hai lần vì
    /// màn hình chậm là chuyện thường, và kết quả sẽ là khách bị tính tiền hai lần. Hóa đơn đã
    /// hủy không tính — hủy rồi thì lập lại được.
    /// </para>
    /// </summary>
    Task<SalesInvoice?> FindOpenByAppointmentAsync(
        string appointmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// BR-INV-016 — cấp số hóa đơn kế tiếp cho một ngày làm việc, dạng <c>HD-yyyyMMdd-nnn</c>,
    /// đánh theo từng tiệm và reset mỗi ngày.
    /// <para>
    /// Nằm ở kho dữ liệu hóa đơn chứ không có một cổng riêng cho bảng <c>InvoiceCounters</c>:
    /// bộ đếm ấy không phải thứ ai đó truy vấn, nó chỉ tồn tại để trả lời đúng câu hỏi này.
    /// Một interface riêng với đúng một hàm mà chỉ một use case gọi là thêm một tầng gián tiếp
    /// không mua lại được gì.
    /// </para>
    /// <para>
    /// ⚠️ Người gọi <b>phải</b> bọc lời gọi này và lệnh tạo hóa đơn trong cùng một giao dịch.
    /// Ngoài giao dịch thì hai quầy bấm thanh toán cùng lúc vẫn có thể nhận cùng một số.
    /// </para>
    /// </summary>
    Task<string> NextCodeAsync(DateOnly businessDate, CancellationToken cancellationToken = default);

    Task AddAsync(SalesInvoice invoice, CancellationToken cancellationToken = default);

    Task UpdateAsync(SalesInvoice invoice, CancellationToken cancellationToken = default);
}
