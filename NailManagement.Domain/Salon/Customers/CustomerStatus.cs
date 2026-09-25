namespace NailManagement.Domain.Salon.Customers;

/// <summary>BR-CUS-005 — khách hàng chỉ có hai trạng thái. Bỏ <c>CARE</c> của bản frontend cũ.</summary>
public enum CustomerStatus
{
    Active = 1,
    Inactive = 2
}
