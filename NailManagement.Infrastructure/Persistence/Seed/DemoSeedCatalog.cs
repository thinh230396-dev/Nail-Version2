using System.Text.Json;
using NailManagement.Domain.Enums.Platform;
using NailManagement.Domain.Enums.Salon;

namespace NailManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu tĩnh cho bộ nạp mẫu: bảng giá, danh mục dịch vụ, danh sách nhân viên và khách.
/// <para>
/// Tách khỏi <see cref="DemoDataSeeder"/> để phần đó chỉ còn logic dựng quan hệ giữa các
/// bảng, không lẫn với hàng trăm dòng dữ liệu.
/// </para>
/// <para>
/// Bảng giá và bộ quyền tính năng chép từ <c>src/utils/subscriptions.ts</c> của frontend
/// theo BR-SUB-007, nhưng <b>giá đã đổi sang VND số nguyên</b> theo BR-VAL-003 — dữ liệu
/// mẫu cũ ghi 49, 99, 249 USD, và giữ nguyên con số đó là để lẫn hai đơn vị tiền trong
/// cùng một hệ thống.
/// </para>
/// </summary>
internal static class DemoSeedCatalog
{
    internal sealed record CapabilityDefinition(string Key, string Label);

    /// <summary>Toàn bộ quyền tính năng, đúng thứ tự của bảng ánh xạ ở frontend.</summary>
    internal static readonly CapabilityDefinition[] Capabilities =
    [
        new("appointments", "Quản lý lịch hẹn"),
        new("online_booking", "Trang đặt lịch trực tuyến"),
        new("customers", "Quản lý khách hàng"),
        new("advanced_reports", "Báo cáo nâng cao"),
        new("loyalty", "Loyalty & khách hàng thân thiết"),
        new("automation", "SMS & Email tự động"),
        new("inventory", "Kế toán & quản lý kho"),
        new("finance", "Sổ thu & chi"),
        new("nail_gallery", "Thư viện màu & mẫu Nail"),
        new("sanitation", "Vệ sinh & an toàn"),
        new("api", "API tích hợp"),
        new("custom_domain", "Custom domain"),
        new("sso", "SSO & bảo mật nâng cao"),
        new("priority_support", "Hỗ trợ ưu tiên 24/7"),
        new("account_manager", "Dedicated Account Manager")
    ];

    // BR-SUB-007 — ba bậc gói chuẩn CỘNG DỒN: Basic nằm trong Premium, Premium nằm trong
    // Enterprise. Viết theo từng bậc rồi cộng lại, thay vì chép tay ba danh sách đầy đủ —
    // chép tay là cách chắc chắn để ba nơi lệch nhau sau vài lần sửa.
    private static readonly string[] BasicTier = ["appointments", "online_booking", "customers"];

    private static readonly string[] PremiumTier =
        ["advanced_reports", "loyalty", "automation", "finance", "nail_gallery", "api", "priority_support"];

    private static readonly string[] EnterpriseTier =
        ["inventory", "sanitation", "custom_domain", "sso", "account_manager"];

    internal sealed record PackageSeed(
        string Id,
        string Name,
        string Description,
        long Price,
        BillingCycle BillingCycle,
        int MaxSalons,
        int MaxStaff,
        string[] EnabledCapabilities,
        string[] Features,
        string LimitsJson,
        string Color);

    internal static readonly PackageSeed[] Packages =
    [
        new(
            "PKG-BASIC", "Basic",
            "Gói cơ bản cho tiệm mới mở và mô hình một chi nhánh.",
            1_200_000, BillingCycle.Monthly, 1, 5,
            BasicTier,
            [
                "Quản lý lịch hẹn cơ bản",
                "Tối đa 5 nhân viên",
                "Báo cáo doanh thu theo ngày",
                "Hỗ trợ qua email"
            ],
            Limits(500, 5, 100, 1, 0, 0, 365),
            "#7c3aed"),

        new(
            "PKG-PREMIUM", "Premium",
            "Gói vận hành chuyên nghiệp cho tiệm nhiều chi nhánh.",
            2_500_000, BillingCycle.Monthly, 3, 999,
            [.. BasicTier, .. PremiumTier],
            [
                "Quản lý lịch hẹn nâng cao",
                "Tối đa 3 chi nhánh",
                "Báo cáo doanh thu 4 chiều",
                "Sổ thu & chi",
                "Hỗ trợ ưu tiên trong giờ hành chính"
            ],
            Limits(null, 50, 2000, 5, 10000, 1, 730),
            "#10b981"),

        new(
            "PKG-ENTERPRISE", "Enterprise",
            "Gói dành cho chuỗi tiệm, cần bảo mật và tích hợp sâu.",
            6_200_000, BillingCycle.Monthly, 99, 9999,
            [.. BasicTier, .. PremiumTier, .. EnterpriseTier],
            [
                "Toàn bộ tính năng gói Premium",
                "Quản lý chuỗi nhiều chi nhánh",
                "Kế toán & quản lý kho",
                "SSO và bảo mật nâng cao",
                "Chuyên viên hỗ trợ riêng"
            ],
            Limits(null, 500, null, null, null, null, null),
            "#f59e0b")
    ];

    internal sealed record ServiceSeed(
        string Id, string Name, string Category, long Price, int DurationMinutes, int BufferMinutes);

    /// <summary>Tám dịch vụ của tiệm chính, giá theo mặt bằng tiệm nail tại TP.HCM.</summary>
    internal static readonly ServiceSeed[] LumiereServices =
    [
        new("SVC-LUM-01", "Sơn gel tay", "Sơn gel", 250_000, 60, 10),
        new("SVC-LUM-02", "Sơn gel chân", "Sơn gel", 300_000, 75, 10),
        new("SVC-LUM-03", "Đắp bột tay", "Đắp bột", 450_000, 120, 15),
        new("SVC-LUM-04", "Vẽ móng nghệ thuật", "Nail art", 180_000, 45, 5),
        new("SVC-LUM-05", "Chăm sóc da tay", "Chăm sóc", 200_000, 40, 5),
        new("SVC-LUM-06", "Chăm sóc da chân", "Chăm sóc", 260_000, 50, 5),
        new("SVC-LUM-07", "Tháo gel và phục hồi móng", "Chăm sóc", 120_000, 30, 5),
        new("SVC-LUM-08", "Combo cưới: gel tay chân và nail art", "Combo", 850_000, 180, 20)
    ];

    /// <summary>
    /// Ba dịch vụ của tiệm thứ hai. Tiệm này tồn tại để chứng minh cách ly dữ liệu ở
    /// BR-ISO-006: cùng một tài khoản chủ tiệm quản lý cả hai, nhưng dữ liệu không lẫn.
    /// </summary>
    internal static readonly ServiceSeed[] MuseServices =
    [
        new("SVC-MUSE-01", "Sơn gel tay", "Sơn gel", 220_000, 60, 10),
        new("SVC-MUSE-02", "Đắp bột tay", "Đắp bột", 400_000, 110, 15),
        new("SVC-MUSE-03", "Vẽ móng nghệ thuật", "Nail art", 160_000, 45, 5)
    ];

    internal sealed record StaffSeed(
        string Id,
        string FullName,
        string Phone,
        StaffRole Role,
        string BranchId,
        int ShiftStartHour,
        int ShiftEndHour,
        decimal CommissionRate,
        string[] Skills);

    internal static readonly StaffSeed[] LumiereStaff =
    [
        new("STF-LUM-01", "Trần Thị Mai", "0901000101", StaffRole.Technician, DemoIds.LumiereBranchQ3,
            9, 18, 0.15m, ["Sơn gel", "Nail art"]),
        new("STF-LUM-02", "Nguyễn Thu Trang", "0901000102", StaffRole.Technician, DemoIds.LumiereBranchQ3,
            12, 21, 0.15m, ["Đắp bột", "Sơn gel"]),
        new("STF-LUM-03", "Phạm Ngọc Hân", "0901000103", StaffRole.Technician, DemoIds.LumiereBranchQ1,
            9, 18, 0.18m, ["Nail art", "Chăm sóc da tay"]),
        new("STF-LUM-04", "Võ Kim Ngân", "0901000104", StaffRole.Technician, DemoIds.LumiereBranchQ1,
            12, 21, 0.12m, ["Sơn gel", "Chăm sóc da chân"]),
        new("STF-LUM-05", "Lê Hoàng Nam", "0901000105", StaffRole.Receptionist, DemoIds.LumiereBranchQ3,
            9, 18, 0m, ["Tiếp đón", "Thu ngân"]),
        new("STF-LUM-06", "Đặng Bảo Châu", "0901000106", StaffRole.Receptionist, DemoIds.LumiereBranchQ1,
            12, 21, 0m, ["Tiếp đón"])
    ];

    internal static readonly StaffSeed[] MuseStaff =
    [
        new("STF-MUSE-01", "Huỳnh Gia Linh", "0902000201", StaffRole.Technician, DemoIds.MuseBranchMain,
            9, 18, 0.15m, ["Sơn gel"]),
        new("STF-MUSE-02", "Bùi Thanh Thảo", "0902000202", StaffRole.Receptionist, DemoIds.MuseBranchMain,
            9, 18, 0m, ["Tiếp đón"])
    ];

    /// <summary>Hai mươi khách của tiệm chính. Số điện thoại đặt theo dải cố định để dễ tra khi demo.</summary>
    internal static readonly (string Phone, string Name)[] LumiereCustomers =
    [
        ("0911000001", "Nguyễn Thị Hồng Nhung"),
        ("0911000002", "Trần Mỹ Duyên"),
        ("0911000003", "Lê Phương Anh"),
        ("0911000004", "Phạm Khánh Vy"),
        ("0911000005", "Hoàng Thị Lan"),
        ("0911000006", "Đỗ Minh Thư"),
        ("0911000007", "Vũ Hải Yến"),
        ("0911000008", "Bùi Thu Hà"),
        ("0911000009", "Đặng Quỳnh Chi"),
        ("0911000010", "Ngô Bảo Trân"),
        ("0911000011", "Dương Thanh Tâm"),
        ("0911000012", "Lý Gia Hân"),
        ("0911000013", "Trịnh Thảo My"),
        ("0911000014", "Cao Ngọc Diệp"),
        ("0911000015", "Mai Tuyết Nhi"),
        ("0911000016", "Phan Kiều Oanh"),
        ("0911000017", "Tạ Hồng Ngọc"),
        ("0911000018", "Lâm Tuệ Minh"),
        ("0911000019", "Chu Diễm Quỳnh"),
        ("0911000020", "Hồ Bích Ngân")
    ];

    /// <summary>
    /// Ba khách của tiệm thứ hai. Số đầu tiên TRÙNG với một khách của tiệm chính — đó là
    /// bằng chứng sống cho BR-CUS-002: số điện thoại chỉ duy nhất trong phạm vi một tiệm.
    /// </summary>
    internal static readonly (string Phone, string Name)[] MuseCustomers =
    [
        ("0911000001", "Nguyễn Thị Hồng Nhung"),
        ("0922000002", "Trương Mỹ Lệ"),
        ("0922000003", "Đinh Thùy Dương")
    ];

    internal static string CapabilitiesJson(IEnumerable<string> enabledKeys)
    {
        var enabled = enabledKeys.ToHashSet();

        var payload = Capabilities.Select(capability => new
        {
            key = capability.Key,
            label = capability.Label,
            enabled = enabled.Contains(capability.Key)
        });

        return JsonSerializer.Serialize(payload);
    }

    internal static string FeaturesJson(IEnumerable<string> features) => JsonSerializer.Serialize(features);

    internal static string SkillsJson(IEnumerable<string> skills) => JsonSerializer.Serialize(skills);

    /// <summary>
    /// BR-SUB-006 — sáu hạn mức chỉ để trưng bày trên bảng giá. Giá trị null nghĩa là
    /// không giới hạn; hệ thống không đo và không cưỡng chế bất kỳ con số nào ở đây.
    /// </summary>
    private static string Limits(
        int? appointmentsPerMonth,
        int? storageGb,
        int? messagesPerMonth,
        int? adminUsers,
        int? apiCallsPerMonth,
        int? customDomains,
        int? dataRetentionDays)
        => JsonSerializer.Serialize(new
        {
            appointmentsPerMonth,
            storageGb,
            messagesPerMonth,
            adminUsers,
            apiCallsPerMonth,
            customDomains,
            dataRetentionDays
        });
}
