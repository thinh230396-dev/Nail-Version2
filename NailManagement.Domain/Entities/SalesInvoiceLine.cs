using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;

namespace NailManagement.Domain.Entities;

/// <summary>
/// Một dòng trên hóa đơn bán hàng.
/// <para>
/// BR-SVC-006 — dòng hóa đơn lưu ĐƠN GIÁ CỦA CHÍNH NÓ tại thời điểm lập, không tham chiếu
/// ngược sang bảng dịch vụ. Nhờ vậy chủ tiệm đổi bảng giá hôm nay không làm sai lệch hóa
/// đơn đã in cho khách tháng trước.
/// </para>
/// <para>
/// BR-INV-012 — <see cref="ServiceId"/> được phép rỗng: dòng không gắn dịch vụ là mục nhập
/// tay tự do, nhờ đó lễ tân bán được thứ chưa có trong danh mục mà hệ thống không cần cả
/// một bảng sản phẩm.
/// </para>
/// </summary>
public class SalesInvoiceLine : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private SalesInvoiceLine()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        InvoiceId = string.Empty;
        Name = string.Empty;
    }

    private SalesInvoiceLine(
        string id,
        string tenantId,
        string invoiceId,
        string? serviceId,
        string name,
        long unitPrice,
        int quantity)
    {
        Id = id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        ServiceId = serviceId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string InvoiceId { get; private set; }
    public string? ServiceId { get; private set; }
    public string Name { get; private set; }
    public long UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public SalesInvoice? Invoice { get; private set; }
    public Service? Service { get; private set; }

    /// <summary>Thành tiền của dòng. Tính ra, không lưu — hai cột cùng nói một chuyện thì sớm muộn cũng lệch.</summary>
    public long LineTotal => UnitPrice * Quantity;

    public static SalesInvoiceLine Create(
        string id,
        string tenantId,
        string invoiceId,
        string? serviceId,
        string name,
        long unitPrice,
        int quantity)
        => new(
            Guard.Reference(id, "id", "Mã dòng hóa đơn"),
            Guard.Reference(tenantId, "tenantId", "Tiệm"),
            Guard.Reference(invoiceId, "invoiceId", "Hóa đơn"),
            string.IsNullOrWhiteSpace(serviceId) ? null : serviceId.Trim(),
            Guard.NotEmpty(name, "name", "Tên dòng hóa đơn", ValidationPolicy.NameMaxLength),
            Guard.Money(unitPrice, "unitPrice", "Đơn giá"),
            Guard.Between(quantity, 1, 999, "quantity", "Số lượng"));
}
