namespace NailManagement.Domain.Salon.Invoices;

/// <summary>
/// BR-PAY-005 — năm phương thức thanh toán.
/// <para>
/// Đây CHỈ là nhãn ghi nhận thủ công. Hệ thống không gọi sang MoMo hay ZaloPay; lễ tân
/// nhìn thấy tiền vào rồi tự chọn nhãn tương ứng.
/// </para>
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Bank = 2,
    Card = 3,
    Momo = 4,
    ZaloPay = 5
}
