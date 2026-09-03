namespace NailManagement.Application.DTOs;

/// <summary>
/// Một dòng trên hóa đơn bán hàng.
/// <para>
/// <paramref name="UnitPrice"/> là giá <b>đã chốt lúc lập hóa đơn</b> (BR-SVC-006), không
/// phải giá hiện tại của dịch vụ. Chủ tiệm đổi bảng giá hôm nay không làm sai lệch hóa đơn
/// đã in cho khách tháng trước.
/// </para>
/// </summary>
/// <param name="ServiceId">
/// Rỗng nghĩa là mục nhập tay tự do — BR-INV-012, nhờ đó lễ tân bán được thứ chưa có trong
/// danh mục mà hệ thống không cần cả một bảng sản phẩm.
/// </param>
public sealed record SalesInvoiceLineDto(
    string Id,
    string? ServiceId,
    string Name,
    long UnitPrice,
    int Quantity,
    long LineTotal);

/// <summary>
/// Một lần ghi nhận tiền trên hóa đơn — BR-PAY-001.
/// <para>
/// Có mặt ở lát cắt khung hóa đơn vì <b>tiền cọc của lịch hẹn đã thành một dòng ở đây ngay
/// lúc lập hóa đơn</b> (BR-APT-031). Hai đường ghi còn lại — thu tiền và hoàn tiền — thuộc
/// lát cắt thu tiền.
/// </para>
/// </summary>
/// <param name="Amount">VND. Dòng hoàn tiền mang giá trị <b>âm</b> (BR-PAY-006).</param>
public sealed record InvoicePaymentDto(
    string Id,
    string Type,
    string Method,
    long Amount,
    DateTimeOffset PaidAt,
    string? Reference,
    string? Reason);

/// <summary>
/// Một hóa đơn bán hàng của tiệm đang làm việc.
/// <para>
/// Bốn con số tiền — <paramref name="Subtotal"/>, <paramref name="Total"/>,
/// <paramref name="Collected"/>, <paramref name="Remaining"/> — đều do <b>máy chủ</b> tính
/// theo công thức BR-INV-020 và gửi kèm, chứ không để màn hình tự cộng lại. Cộng lại ở
/// trình duyệt là dựng ra một phép tính thứ hai, và ngày nó lệch với phép tính của máy chủ
/// thì người ở quầy là người phát hiện, ngay trước mặt khách.
/// </para>
/// <para>
/// Mang tên khách, tên kỹ thuật viên và tên chi nhánh theo đúng quy ước đã dùng cho lịch hẹn
/// ở ngày 11: đây là một bản đọc, và danh sách hóa đơn phải hiện được tên ngay trên từng dòng.
/// </para>
/// </summary>
/// <param name="Status">
/// BR-PAY-003 — <c>PENDING</c>, <c>PARTIAL</c> và <c>PAID</c> <b>suy ra từ tổng thu</b>, không
/// đặt tay. Chỉ <c>CANCELLED</c> và <c>REFUNDED</c> là do người dùng chủ động chọn.
/// </param>
/// <param name="AppointmentId">Rỗng nghĩa là khách mua lẻ, không đi từ lịch hẹn nào — BR-INV-011.</param>
public sealed record SalesInvoiceDto(
    string Id,
    string TenantId,
    string BranchId,
    string BranchName,
    string CustomerId,
    string? CustomerName,
    string CustomerPhone,
    string? AppointmentId,
    string? StaffId,
    string? StaffName,
    string Code,
    string Status,
    long Subtotal,
    long Discount,
    string? DiscountReason,
    long Tip,
    long Total,
    long Collected,
    long Remaining,
    string? Note,
    IReadOnlyList<SalesInvoiceLineDto> Lines,
    IReadOnlyList<InvoicePaymentDto> Payments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Một dòng người dùng gửi lên khi lập hoặc sửa hóa đơn.
/// <para>
/// Có <b>hai cách dùng loại trừ nhau</b>: gửi <paramref name="ServiceId"/> thì máy chủ tự đọc
/// tên và giá hiện tại từ bảng dịch vụ (BR-SVC-007); gửi <paramref name="Name"/> và
/// <paramref name="UnitPrice"/> thì đó là mục nhập tay (BR-INV-012).
/// </para>
/// <para>
/// Giá của một dịch vụ có trong danh mục <b>không</b> nhận từ client, kể cả khi client gửi
/// kèm: nhận nó là để trình duyệt tự quyết định một buổi làm gel giá mười nghìn.
/// </para>
/// </summary>
public sealed record SalesInvoiceLineInput(
    string? ServiceId,
    string? Name,
    long UnitPrice,
    int Quantity);

/// <summary>
/// Lập hóa đơn — BR-INV-010 khi bấm "Thanh toán" trên một lịch hẹn, hoặc BR-INV-011 cho khách
/// mua lẻ.
/// <para>
/// Không có ô <c>code</c>: số hóa đơn do máy chủ cấp trong giao dịch (BR-INV-016). Không có ô
/// <c>status</c>: trạng thái suy ra từ tổng thu (BR-PAY-003). Không có ô <c>branchId</c>: chi
/// nhánh đi theo lịch hẹn, hoặc theo chi nhánh của người lập nếu là khách mua lẻ.
/// </para>
/// </summary>
/// <param name="AppointmentId">
/// Có thì các dòng lấy từ <c>appointment_services</c> và bỏ qua <paramref name="Lines"/>; tiền
/// cọc của lịch hẹn trở thành một dòng thu loại <c>DEPOSIT</c> (BR-APT-031). Rỗng thì đây là
/// hóa đơn bán lẻ và <paramref name="Lines"/> là bắt buộc.
/// </param>
/// <param name="BranchId">
/// Chỉ dùng cho hóa đơn bán lẻ do <b>chủ tiệm</b> lập, vì họ không thuộc chi nhánh nào. Lễ tân
/// gửi lên cũng bị bỏ qua: chi nhánh của họ đến từ phiên đăng nhập.
/// </param>
public sealed record CreateSalesInvoiceCommand(
    string? AppointmentId,
    string? CustomerId,
    string? StaffId,
    string? BranchId,
    IReadOnlyList<SalesInvoiceLineInput>? Lines,
    long Discount,
    string? DiscountReason,
    long Tip,
    string? Note);

/// <summary>
/// Sửa trọn một hóa đơn chưa thu đủ — BR-INV-015.
/// <para>
/// Thay cả danh sách dòng, giảm giá, tip và ghi chú, cùng khuôn thay-trọn đã dùng cho nhân
/// viên, khách hàng và lịch hẹn. BR-INV-014 cấm sửa hóa đơn đã thanh toán đủ; sai sót ở đó chỉ
/// xử lý bằng hoàn tiền.
/// </para>
/// <para>
/// Không đổi được khách, lịch hẹn hay chi nhánh. Ba thứ đó là <b>danh tính</b> của hóa đơn:
/// đổi chúng là lập một hóa đơn khác, và khi đó việc đúng là hủy cái cũ rồi lập cái mới, để
/// số hóa đơn cũ vẫn tra được.
/// </para>
/// </summary>
public sealed record UpdateSalesInvoiceCommand(
    string InvoiceId,
    string? StaffId,
    IReadOnlyList<SalesInvoiceLineInput>? Lines,
    long Discount,
    string? DiscountReason,
    long Tip,
    string? Note);

/// <param name="Status">
/// Ở lát cắt này chỉ nhận <c>CANCELLED</c> — BR-INV-015. Ba trạng thái <c>PENDING</c>,
/// <c>PARTIAL</c>, <c>PAID</c> không đặt tay được vì chúng suy ra từ tổng thu (BR-PAY-003), còn
/// <c>REFUNDED</c> đi qua đường hoàn tiền của lát cắt thu tiền.
/// </param>
public sealed record ChangeSalesInvoiceStatusCommand(string InvoiceId, string Status);

/// <summary>
/// Ghi nhận một lần khách trả tiền — BR-PAY-001.
/// <para>
/// Không có ô <c>type</c>: đường này luôn sinh dòng <c>PAYMENT</c>. Hai loại còn lại ở
/// BR-PAY-002 đều có nguồn riêng — <c>DEPOSIT</c> do tiền cọc của lịch hẹn chuyển sang ngay
/// lúc lập hóa đơn (BR-APT-031), còn <c>REFUND</c> đi qua <see cref="IssueRefundCommand"/> vì
/// nó cần lý do và chỉ chủ tiệm làm được.
/// </para>
/// <para>
/// Không có ô <c>paidAt</c>: giờ thu là <b>giờ máy chủ</b>. Doanh thu ở BR-REV-001 đếm theo
/// tiền thực thu, nên nhận mốc thời gian từ client là cho phép dời một khoản thu sang tháng
/// khác — và người ở quầy sẽ không bao giờ biết con số báo cáo đã bị dời.
/// </para>
/// <para>
/// BR-PAY-004 — khách trả nửa tiền mặt nửa chuyển khoản thì gọi hai lần, mỗi lần một phương
/// thức. Không cần dạng danh sách: mỗi phương thức vốn đã là một dòng riêng, và gom hai lần
/// thu vào một request chỉ dựng thêm một ca đặc biệt cho thứ mà mô hình dữ liệu đã trả lời.
/// </para>
/// </summary>
/// <param name="Reference">Mã giao dịch ngân hàng hoặc ví, lễ tân nhập tay (BR-PAY-005). Không bắt buộc.</param>
public sealed record RecordPaymentCommand(
    string InvoiceId,
    string? Method,
    long Amount,
    string? Reference);

/// <summary>
/// Hoàn tiền cho khách — BR-PAY-006, một dòng thu mang số <b>âm</b> kèm lý do bắt buộc.
/// <para>
/// Không sửa và không xóa dòng thu cũ: giữ đủ cả hai chiều tiền thì sổ sách mới đối chiếu
/// được, và công thức doanh thu ở BR-REV-001 chỉ cần cộng dồn là đã tự trừ phần trả lại.
/// </para>
/// <para>
/// BR-PAY-007 — chỉ chủ tiệm. Đó là lý do nhóm chức năng <c>Refunds</c> tách khỏi
/// <c>SalesInvoices</c> trong ma trận quyền: lễ tân thu tiền cả ngày nhưng không được trả
/// tiền ra khỏi két.
/// </para>
/// </summary>
/// <param name="Reason">Bắt buộc — BR-PAY-006. Đây là thứ duy nhất giải thích được vì sao két thiếu tiền.</param>
public sealed record IssueRefundCommand(
    string InvoiceId,
    string? Method,
    long Amount,
    string? Reason);
