using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Domain.Policies;

/// <summary>
/// Ma trận phân quyền ở mục 3.4 của tài liệu nghiệp vụ, viết thành <b>một bảng dữ liệu</b>.
/// <para>
/// Lý do không rải các nhánh <c>if</c> theo từng endpoint: ma trận có 16 nhóm chức năng và
/// 3 vai trò, tức 48 ô. Rải ra thì không ai đọc được toàn cảnh, và mỗi lần thêm endpoint
/// lại phải nhớ mình đã quyết gì cho ô tương ứng. Ở dạng bảng thì so với tài liệu nghiệp vụ
/// bằng mắt là xong.
/// </para>
/// <para>
/// BR-AUTH-030 là hàng quan trọng nhất: Superadmin <b>không</b> chạm được vào dữ liệu
/// nghiệp vụ bên trong tiệm — khách hàng, nhân viên, lịch hẹn, hóa đơn bán hàng, báo cáo
/// doanh thu. Ranh giới đó được cưỡng chế hai lớp: bảng này chặn ở tầng quyền, và bộ lọc
/// theo tiệm ở <c>NailDbContext</c> chặn ở tầng dữ liệu.
/// </para>
/// </summary>
public static class PermissionMatrix
{
    private static readonly Dictionary<(UserRole Role, Feature Feature), AccessLevel> Matrix = new()
    {
        // ── Tầng nền tảng ─────────────────────────────────────────────────────
        [(UserRole.SuperAdmin, Feature.Tenants)] = AccessLevel.Full,
        [(UserRole.SuperAdmin, Feature.Packages)] = AccessLevel.Full,
        [(UserRole.SuperAdmin, Feature.SubscriptionInvoices)] = AccessLevel.Full,
        [(UserRole.SuperAdmin, Feature.UpgradeRequests)] = AccessLevel.Full,
        [(UserRole.SuperAdmin, Feature.TenantAdminAccounts)] = AccessLevel.Full,
        [(UserRole.SuperAdmin, Feature.AuditLogs)] = AccessLevel.Full,

        // Chủ tiệm xem hóa đơn đăng ký của mình và nộp chứng từ thanh toán (BR-INV-032).
        // Mức ghi ở đây là Full vì "nộp chứng từ" là một thao tác ghi; phạm vi chỉ giới hạn
        // trong tiệm của họ, và điều đó do bộ lọc dữ liệu lo, không phải bảng này.
        [(UserRole.TenantAdmin, Feature.SubscriptionInvoices)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.UpgradeRequests)] = AccessLevel.Full,

        // ── Quản trị tiệm ─────────────────────────────────────────────────────
        // Hồ sơ của chính tiệm mình — chỉ xem. Không phải hàng "Tenant" của ma trận mục
        // 3.4: hàng đó nói về quản lý tiệm của người khác và vẫn là ô riêng của Superadmin.
        [(UserRole.TenantAdmin, Feature.OwnTenantProfile)] = AccessLevel.Read,

        [(UserRole.TenantAdmin, Feature.ReceptionistAccounts)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Branches)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Staff)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Services)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Customers)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Appointments)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.SalesInvoices)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.Refunds)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.ForceCompleteAppointment)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.RevenueReports)] = AccessLevel.Full,
        [(UserRole.TenantAdmin, Feature.AuditLogs)] = AccessLevel.Read,

        // ── Lễ tân ────────────────────────────────────────────────────────────
        // Chi nhánh, nhân viên và dịch vụ: chỉ xem. Lễ tân cần biết ai đang làm và dịch vụ
        // giá bao nhiêu, nhưng không được sửa.
        [(UserRole.Receptionist, Feature.Branches)] = AccessLevel.Read,
        [(UserRole.Receptionist, Feature.Staff)] = AccessLevel.Read,
        [(UserRole.Receptionist, Feature.Services)] = AccessLevel.Read,

        // Khách hàng, lịch hẹn, hóa đơn: làm được thật. Đây là công việc hằng ngày ở quầy.
        [(UserRole.Receptionist, Feature.Customers)] = AccessLevel.Full,
        [(UserRole.Receptionist, Feature.Appointments)] = AccessLevel.Full,
        [(UserRole.Receptionist, Feature.SalesInvoices)] = AccessLevel.Full

        // Ba ô cố ý VẮNG MẶT với lễ tân, và vắng mặt nghĩa là không có quyền:
        //   Refunds                  — BR-PAY-007, chỉ chủ tiệm hoàn tiền được
        //   ForceCompleteAppointment — BR-APT-027, chỉ chủ tiệm đóng lịch khi chưa thu đủ
        //   RevenueReports           — doanh thu tiệm không phải việc của quầy
    };

    /// <summary>Mức truy cập của một vai trò với một nhóm chức năng. Không có trong bảng nghĩa là không có quyền.</summary>
    public static AccessLevel Resolve(UserRole role, Feature feature)
        => Matrix.TryGetValue((role, feature), out var level) ? level : AccessLevel.None;

    /// <param name="isWrite">
    /// Thao tác ghi hay đọc. Phân biệt ở đây thay vì tách thành hai bảng, để một ô "chỉ xem"
    /// không thể vô tình bị đọc thành "toàn quyền".
    /// </param>
    public static bool Allows(UserRole role, Feature feature, bool isWrite)
    {
        var level = Resolve(role, feature);

        return isWrite ? level == AccessLevel.Full : level != AccessLevel.None;
    }
}
