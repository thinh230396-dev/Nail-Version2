namespace NailManagement.Domain.Enums.Auth;

/// <summary>
/// Các nhóm chức năng trong ma trận phân quyền ở mục 3.4 của tài liệu nghiệp vụ.
/// <para>
/// Đây là <b>đơn vị để hỏi "vai trò này có được làm không"</b>, nên nó chia theo nghiệp vụ
/// chứ không chia theo endpoint. Một nhóm có thể trải trên nhiều đường dẫn HTTP, và ngược
/// lại một đường dẫn chỉ thuộc đúng một nhóm.
/// </para>
/// </summary>
public enum Feature
{
    /// <summary>Tạo, sửa, khóa, xóa mềm tiệm — chỉ Superadmin.</summary>
    Tenants = 1,

    /// <summary>Bảng giá gói dịch vụ — chỉ Superadmin.</summary>
    Packages = 2,

    /// <summary>Hóa đơn đăng ký. Superadmin toàn quyền, chủ tiệm xem và nộp chứng từ.</summary>
    SubscriptionInvoices = 3,

    /// <summary>Yêu cầu nâng cấp gói. Chủ tiệm gửi và hủy, Superadmin duyệt.</summary>
    UpgradeRequests = 4,

    /// <summary>Tài khoản chủ tiệm — chỉ Superadmin tạo được (BR-AUTH-010).</summary>
    TenantAdminAccounts = 5,

    /// <summary>Tài khoản lễ tân — chỉ chủ tiệm tạo được (BR-AUTH-011).</summary>
    ReceptionistAccounts = 6,

    Branches = 7,
    Staff = 8,
    Services = 9,
    Customers = 10,
    Appointments = 11,

    /// <summary>Hóa đơn bán hàng: lập và thu tiền.</summary>
    SalesInvoices = 12,

    /// <summary>Hoàn tiền — tách riêng khỏi hóa đơn vì BR-PAY-007 chỉ cho chủ tiệm làm.</summary>
    Refunds = 13,

    /// <summary>
    /// Hoàn tất lịch hẹn khi hóa đơn chưa thu đủ — tách riêng vì BR-APT-027 là ngoại lệ
    /// chỉ chủ tiệm có, còn lễ tân thì không.
    /// </summary>
    ForceCompleteAppointment = 14,

    /// <summary>Báo cáo doanh thu tiệm.</summary>
    RevenueReports = 15,

    /// <summary>Nhật ký kiểm toán — BR-AUD-005.</summary>
    AuditLogs = 16
}
