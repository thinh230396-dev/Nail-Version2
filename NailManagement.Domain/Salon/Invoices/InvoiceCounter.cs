using NailManagement.Domain.Shared;

namespace NailManagement.Domain.Salon.Invoices;

/// <summary>
/// Bộ đếm số hóa đơn bán hàng — BR-INV-016, đánh số theo TỪNG TIỆM và reset mỗi ngày.
/// <para>
/// Vì sao cần một bảng riêng thay vì đếm số hóa đơn đã có: đếm bằng câu lệnh COUNT thì hai
/// máy tính ở quầy bấm thanh toán cùng lúc sẽ đọc ra cùng một con số và sinh hai hóa đơn
/// trùng số. Bảng này có một dòng cho mỗi cặp tiệm–ngày, và việc tăng số diễn ra trong
/// cùng giao dịch với việc tạo hóa đơn, nên hai người bấm cùng lúc vẫn ra hai số khác nhau.
/// </para>
/// </summary>
public class InvoiceCounter : ITenantOwned
{
    /// <summary>Dành riêng cho EF Core.</summary>
    private InvoiceCounter()
    {
        TenantId = string.Empty;
    }

    private InvoiceCounter(string tenantId, DateOnly businessDate)
    {
        TenantId = tenantId;
        BusinessDate = businessDate;
        LastNumber = 0;
    }

    public string TenantId { get; private set; }

    /// <summary>Ngày làm việc mà bộ đếm này phục vụ. Sang ngày mới là một dòng mới, số quay về 1.</summary>
    public DateOnly BusinessDate { get; private set; }

    public int LastNumber { get; private set; }

    public static InvoiceCounter StartOfDay(string tenantId, DateOnly businessDate)
        => new(Guard.Reference(tenantId, "tenantId", "Tiệm"), businessDate);

    /// <summary>Cấp số tiếp theo. Phải gọi bên trong cùng một giao dịch với việc tạo hóa đơn.</summary>
    public int NextNumber()
    {
        LastNumber += 1;
        return LastNumber;
    }

    /// <summary>BR-INV-016 — định dạng số hóa đơn: HD-yyyyMMdd-nnn.</summary>
    public static string FormatCode(DateOnly businessDate, int number)
        => $"HD-{businessDate:yyyyMMdd}-{number:D3}";
}
