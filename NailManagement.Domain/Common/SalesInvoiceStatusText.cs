using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Domain.Common;

/// <summary>
/// Tên tiếng Việt của năm trạng thái hóa đơn bán hàng, dùng trong <b>thông báo lỗi gửi tới
/// người dùng</b>.
/// <para>
/// Cùng lý do với <see cref="AppointmentStatusText"/>: <c>DomainException</c> mang theo chính
/// câu chữ mà người ở quầy sẽ đọc, nên ném ra tên hằng số của C# là đưa cho họ những chữ không
/// xuất hiện ở bất kỳ đâu trên màn hình.
/// </para>
/// <para>
/// Khác <c>SalesInvoiceMapper.ToWireFormat</c> ở tầng Application, thứ sinh ra chuỗi
/// <c>PENDING</c> / <c>PARTIAL</c> cho <b>máy</b> đọc. Hai bảng phục vụ hai người đọc khác nhau.
/// </para>
/// </summary>
public static class SalesInvoiceStatusText
{
    public static string Label(SalesInvoiceStatus status) => status switch
    {
        SalesInvoiceStatus.Pending => "Chờ thanh toán",
        SalesInvoiceStatus.Partial => "Thu một phần",
        SalesInvoiceStatus.Paid => "Đã thanh toán",
        SalesInvoiceStatus.Refunded => "Đã hoàn tiền",
        SalesInvoiceStatus.Cancelled => "Đã hủy",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái hóa đơn không hợp lệ.")
    };
}
