namespace NailManagement.Domain.Salon.Services;

/// <summary>BR-SVC-004/005 — "ngừng dịch vụ" là <c>Inactive</c>, không đặt lịch mới được.</summary>
public enum ServiceStatus
{
    Active = 1,
    Inactive = 2
}
