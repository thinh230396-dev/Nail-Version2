using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Domain.Salon.Revenue;

/// <summary>
/// Cổng đọc dữ liệu cho báo cáo doanh thu — BR-REV-001…008.
///
/// <para>
/// Tách khỏi <see cref="ISalesInvoiceRepository"/> dù cùng đọc một bảng, vì hai cổng trả lời
/// hai câu hỏi khác hẳn nhau. Cổng kia phục vụ **sổ hóa đơn**: lấy theo giờ lập, để quầy mở
/// một hóa đơn ra xem và thu tiền. Cổng này phục vụ **báo cáo**: lấy theo giờ THU, vì
/// BR-REV-001 ghi nhận doanh thu trên cơ sở tiền mặt và BR-REV-003 bắt hoàn tiền làm giảm
/// doanh thu tại ngày hoàn chứ không sửa lại ngày cũ.
/// </para>
/// <para>
/// Hai mốc thời gian ấy khác nhau thật: một hóa đơn lập cuối tháng trước mà khách trả nốt đầu
/// tháng này thuộc về sổ hóa đơn tháng trước và doanh thu tháng này. Gộp hai câu hỏi vào một
/// cổng là mời người viết sau chọn nhầm mốc.
/// </para>
/// </summary>
public interface IRevenueRepository
{
    /// <summary>
    /// Mọi hóa đơn có **ít nhất một dòng thu** rơi vào khoảng thời gian, kèm sẵn dòng hàng,
    /// dòng thu tiền và hồ sơ nhân viên được ghi công.
    ///
    /// <para>
    /// Trả về cả hóa đơn chứ không trả riêng các dòng thu, vì công thức BR-REV-001 cần
    /// <c>total</c> và <c>tip</c> của hóa đơn để loại tip ra khỏi doanh thu, và chiều "dịch vụ"
    /// còn cần các dòng hàng. Lọc lại đúng những dòng thu nằm trong khoảng là việc của tầng
    /// use case — nó có luật, còn tầng này chỉ có dữ liệu.
    /// </para>
    /// <para>
    /// Kèm <c>Staff</c> vì BR-EMP-011 tính hoa hồng bằng tỉ lệ trên hồ sơ nhân viên. Không kèm
    /// <c>Customer</c>: BR-REV-004 chỉ có bốn chiều và khách không nằm trong đó.
    /// </para>
    /// </summary>
    /// <param name="branchId">
    /// Thu hẹp về một chi nhánh, hoặc <c>null</c> để lấy cả tiệm. Là bộ lọc của **người dùng**
    /// chọn trên màn hình, không phải ranh giới cách ly — Superadmin không có ô
    /// <c>RevenueReports</c> nào trong ma trận, còn lễ tân thì cũng không.
    /// </param>
    Task<IReadOnlyList<SalesInvoice>> ListCollectedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default);
}
