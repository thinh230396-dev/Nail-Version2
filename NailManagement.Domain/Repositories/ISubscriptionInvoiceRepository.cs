using NailManagement.Domain.Entities;

namespace NailManagement.Domain.Repositories;

/// <summary>
/// Cổng ra hóa đơn tiệm trả cho SalonSys.
/// <para>
/// Bảng này KHÔNG mang <c>ITenantOwned</c> nên không có bộ lọc theo tiệm. BR-TENANT-022 là
/// lý do: hóa đơn của tiệm đã xóa mềm vẫn phải đọc được để tính doanh thu nền tảng.
/// </para>
/// </summary>
public interface ISubscriptionInvoiceRepository
{
    Task AddAsync(SubscriptionInvoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổng số hóa đơn đăng ký đã phát hành, dùng để sinh số tiếp theo trong dãy.
    /// <para>
    /// Đếm <b>toàn hệ thống</b> chứ không theo từng tiệm, vì số hóa đơn đăng ký là một dãy
    /// duy nhất do SalonSys phát hành với tư cách người bán — đúng như chỉ số duy nhất trên
    /// cột <c>Code</c> đang quy định. Đếm theo tiệm thì tiệm thứ hai đã sinh ra số trùng.
    /// </para>
    /// <para>
    /// Khác hẳn số hóa đơn <i>bán hàng</i> ở BR-INV-016: số đó đếm theo từng tiệm và reset
    /// mỗi ngày, vì người phát hành nó là chính tiệm chứ không phải SalonSys.
    /// </para>
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
