namespace NailManagement.Application.DTOs.Salon;

/// <summary>
/// Một dòng trong bảng phân rã doanh thu — dùng chung cho cả bốn chiều của BR-REV-004.
///
/// <para>
/// Cố ý là <b>một kiểu duy nhất</b> thay vì bốn kiểu riêng: bốn chiều chỉ khác nhau ở chỗ
/// <paramref name="Key"/> là ngày, mã chi nhánh, mã nhân viên hay mã dịch vụ. Dựng bốn record
/// gần giống hệt nhau là bắt màn hình viết bốn hàm vẽ bảng cho một việc.
/// </para>
/// </summary>
/// <param name="Key">
/// Mã định danh của mục — ngày dạng <c>yyyy-MM-dd</c>, hoặc mã chi nhánh / nhân viên / dịch
/// vụ. Rỗng chỉ xảy ra ở chiều nhân viên, cho phần doanh thu chưa ghi công cho ai.
/// </param>
/// <param name="Revenue">
/// Doanh thu tiệm, **đã loại tip** theo BR-REV-002. Hoàn tiền đã tự trừ vì dòng hoàn mang số
/// âm (BR-PAY-006), và nó trừ vào ngày hoàn chứ không sửa ngày cũ (BR-REV-003).
/// </param>
/// <param name="Collected">Tổng tiền mặt thật sự đi qua két, gồm cả tip. Luôn ≥ <paramref name="Revenue"/>.</param>
public sealed record RevenueBreakdownRow(
    string Key,
    string Label,
    long Revenue,
    long Collected,
    int InvoiceCount);

/// <summary>
/// Một dòng của chiều "nhân viên", có thêm hoa hồng — BR-REV-005 và BR-EMP-011.
///
/// <para>
/// Tách khỏi <see cref="RevenueBreakdownRow"/> vì hai trường cuối chỉ có nghĩa ở đúng chiều
/// này: tỉ lệ hoa hồng là thuộc tính của hồ sơ nhân viên, và ba chiều kia không có ai để hỏi.
/// </para>
/// </summary>
/// <param name="CommissionRate">Tỉ lệ dạng 0–1 (0,15 là 15%), đọc từ hồ sơ nhân viên tại thời điểm xem báo cáo.</param>
/// <param name="Commission">
/// Tiền hoa hồng — **một phép nhân lúc hiển thị**, không có bảng nào lưu (BR-EMP-011). Đổi tỉ
/// lệ hôm nay sẽ đổi luôn con số của tháng trước; đó là hành vi đã biết của một MVP không có
/// bảng lương và kỳ chốt.
/// </param>
public sealed record StaffRevenueRow(
    string Key,
    string Label,
    long Revenue,
    long Collected,
    int InvoiceCount,
    decimal CommissionRate,
    long Commission);

/// <summary>
/// Báo cáo doanh thu của một khoảng thời gian — BR-REV-001…005.
///
/// <para>
/// Trả cả bốn chiều trong <b>một</b> phản hồi, và đó là quyết định 60 chốt ngày 16: màn báo
/// cáo vẽ một trang cần cả bốn cùng lúc, nên bốn lời gọi riêng là bốn lần quét cùng một
/// khoảng dữ liệu. Tệ hơn, bốn lời gọi có thể rơi vào hai phía của một lần thu tiền ở quầy, và
/// khi đó bốn bảng trên cùng màn hình cộng ra bốn con số khác nhau — thứ không giải thích được
/// với người đọc.
/// </para>
/// <para>
/// <b>Bốn bảng luôn cộng lại ra cùng <paramref name="Revenue"/>.</b> Đó là hệ quả của việc
/// phân bổ theo tỉ lệ ở <c>RevenuePolicy</c>, và là tính chất đáng nêu khi bảo vệ.
/// </para>
/// </summary>
/// <param name="Revenue">Doanh thu tiệm trong khoảng, đã loại tip và đã trừ hoàn tiền.</param>
/// <param name="Collected">Tổng tiền đi qua két, gồm tip. Chênh lệch với <paramref name="Revenue"/> chính là tip.</param>
/// <param name="Tips">BR-REV-002 — tiền của kỹ thuật viên, tiệm chỉ giữ hộ. Hiện riêng để không ai cộng nhầm vào doanh thu.</param>
/// <param name="Refunds">Tổng đã hoàn trong khoảng, ghi bằng số dương cho người đọc dễ hiểu.</param>
public sealed record RevenueReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string? BranchId,
    long Revenue,
    long Collected,
    long Tips,
    long Refunds,
    int InvoiceCount,
    IReadOnlyList<RevenueBreakdownRow> ByDay,
    IReadOnlyList<RevenueBreakdownRow> ByBranch,
    IReadOnlyList<StaffRevenueRow> ByStaff,
    IReadOnlyList<RevenueBreakdownRow> ByService);
