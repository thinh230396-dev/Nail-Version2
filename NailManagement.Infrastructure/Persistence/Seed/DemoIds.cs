namespace NailManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Mã định danh cố định của dữ liệu mẫu.
/// <para>
/// Đặt tên rõ ràng thay vì sinh ngẫu nhiên để lúc demo có thể mở bảng trong SQL Server
/// Object Explorer và đọc hiểu ngay, và để hai bộ nạp (tài khoản và dữ liệu nghiệp vụ)
/// tham chiếu tới cùng một bản ghi mà không phải truyền tham số qua lại.
/// </para>
/// </summary>
internal static class DemoIds
{
    // Ba tài khoản đăng nhập có sẵn từ ngày 1, giữ nguyên email và mật khẩu.
    public const string SuperAdminUser = "USR-SUPERADMIN";
    public const string LumiereAdminUser = "USR-TENANT-LUMIERE";
    public const string NaileReceptionUser = "USR-RECEPTION-NAILE";

    // Hai tiệm do CÙNG một tài khoản chủ tiệm quản lý — BR-AUTH-023.
    public const string LumiereTenant = "TEN-LUMIERE";
    public const string MuseTenant = "TEN-MUSE";

    // Bốn tiệm còn lại chỉ có hồ sơ, để danh sách của Superadmin hiện đủ bốn trạng thái
    // mà BR-TENANT-002 tính lúc đọc.
    public const string BloomTenant = "TEN-BLOOM";
    public const string OasisTenant = "TEN-OASIS";
    public const string MorningTenant = "TEN-MORNING";
    public const string AuroraTenant = "TEN-AURORA";

    public const string LumiereBranchQ3 = "BRN-LUMIERE-Q3";
    public const string LumiereBranchQ1 = "BRN-LUMIERE-Q1";
    public const string MuseBranchMain = "BRN-MUSE-01";
}
