namespace NailManagement.Domain.Salon.Customers;

/// <summary>
/// BR-CUS-007 — hạng khách SUY RA từ tổng chi tiêu, không có cột riêng trong database và
/// không có nghiệp vụ nâng/hạ hạng.
/// BR-CUS-008: hạng khách không ảnh hưởng giá, chỉ để lễ tân nhận ra khách quen.
/// </summary>
public enum CustomerTier
{
    New = 1,
    Standard = 2,
    Loyal = 3,
    Vip = 4
}
