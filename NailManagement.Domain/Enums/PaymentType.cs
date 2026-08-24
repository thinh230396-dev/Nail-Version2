namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-PAY-002 — ba loại dòng tiền trên hóa đơn bán hàng.
/// <para>
/// <c>Deposit</c> là tiền cọc của lịch hẹn được chuyển thành một dòng đã thu (BR-APT-031).
/// <c>Refund</c> mang số tiền ÂM (BR-PAY-006), nhờ vậy công thức doanh thu ở BR-REV-001
/// chỉ cần cộng dồn là đã tự trừ phần hoàn tiền.
/// </para>
/// </summary>
public enum PaymentType
{
    Deposit = 1,
    Payment = 2,
    Refund = 3
}
