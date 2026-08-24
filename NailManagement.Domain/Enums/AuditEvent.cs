namespace NailManagement.Domain.Enums;

/// <summary>
/// BR-AUD-002 — hệ thống chỉ ghi đúng những loại sự kiện này, không ghi mọi thao tác.
/// BR-AUD-004: bản ghi nhật ký không sửa được và không xóa được.
/// </summary>
public enum AuditEvent
{
    Login = 1,
    LoginFailed = 2,
    TenantCreated = 3,
    TenantUpdated = 4,
    TenantDeleted = 5,
    AccountCreated = 6,
    AccountLocked = 7,
    PaymentReceived = 8,
    RefundIssued = 9,
    PackageChanged = 10
}
