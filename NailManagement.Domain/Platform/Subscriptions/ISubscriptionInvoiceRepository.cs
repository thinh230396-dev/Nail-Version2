using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Platform.Subscriptions;

/// <summary>
/// Cổng ra hóa đơn tiệm trả cho SalonSys.
/// <para>
/// Bảng này KHÔNG mang <c>ITenantOwned</c> nên không có bộ lọc theo tiệm. BR-TENANT-022 là
/// lý do: hóa đơn của tiệm đã xóa mềm vẫn phải đọc được để tính doanh thu nền tảng.
/// </para>
/// <para>
/// Hệ quả của việc thiếu bộ lọc tự động: phạm vi phải do <b>người gọi</b> nói ra. Vì thế cổng
/// này có hai phép đọc tách bạch — <see cref="ListAllAsync"/> cho tầng nền tảng và
/// <see cref="ListByTenantAsync"/> cho một tiệm — thay vì một phép đọc nhận tham số tiệm có
/// thể để trống. Một tham số có thể để trống nghĩa là quên truyền thì lặng lẽ trả về tất cả,
/// và đó đúng là cách lỗ rò rỉ ngày 24 đã xảy ra.
/// </para>
/// </summary>
public interface ISubscriptionInvoiceRepository
{
    /// <summary>
    /// Toàn bộ hóa đơn đăng ký của mọi tiệm, mới nhất trước, kèm sẵn hồ sơ tiệm.
    ///
    /// <para>
    /// Là phép đọc <b>toàn hệ thống</b>, không lọc theo tiệm — và đó là điểm khác mọi kho dữ
    /// liệu khác. BR-INV-001 xếp bảng này về phía nền tảng: đây là tiền <b>tiệm trả cho
    /// SalonSys</b>, nên nó thuộc quyền Superadmin (BR-AUD-005 cùng mô hình). Bộ lọc theo tiệm
    /// ở <c>NailDbContext</c> cũng không áp cho nó vì Superadmin không làm việc trong tiệm nào.
    /// </para>
    /// <para>
    /// ⚠️ Chỉ gọi được sau khi đã xác định người gọi là Superadmin. Chủ tiệm phải đi qua
    /// <see cref="ListByTenantAsync"/>.
    /// </para>
    /// <para>
    /// Không có khoảng ngày như sổ hóa đơn bán hàng: một tiệm sinh vài hóa đơn đăng ký mỗi năm,
    /// nên bảng này có trần tự nhiên rất thấp và trả trọn là đủ.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SubscriptionInvoice>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hóa đơn đăng ký của <b>một tiệm</b>, mới nhất trước — phép đọc dành cho chủ tiệm
    /// (BR-INV-032: họ xem hóa đơn của chính mình).
    /// <para>
    /// Mã tiệm là tham số bắt buộc và đến từ phiên đăng nhập ở máy chủ, không từ thân request.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SubscriptionInvoice>> ListByTenantAsync(
        string tenantId, CancellationToken cancellationToken = default);

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
