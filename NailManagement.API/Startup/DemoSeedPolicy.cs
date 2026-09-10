namespace NailManagement.API.Startup;

/// <summary>
/// Quyết định <b>có được phép nạp dữ liệu demo hay không</b> — tách khỏi <c>Program.cs</c> để
/// điều kiện này đọc được bằng mắt và kiểm được bằng test.
///
/// <para>
/// Đây là một hàng rào bảo mật chứ không phải một tiện nghi cho máy phát triển. Bộ nạp demo tạo
/// tài khoản <c>superadmin@salonsys.vn</c> với mật khẩu cố định nằm ngay trong mã nguồn; trước
/// ngày 24 nó chạy ở <b>mọi</b> môi trường, nên bất kỳ bản triển khai nào dựng trên một database
/// trống cũng tự sinh ra một tài khoản toàn quyền mà mật khẩu ai đọc repo cũng biết.
/// </para>
/// <para>
/// Hai điều kiện phải cùng đúng, và mỗi điều kiện bịt một kiểu nhầm khác nhau:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Môi trường phải là Development</b> — bịt trường hợp quên tắt cờ khi đem đi triển khai.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Cờ <c>DemoSeed:Enabled</c> phải được bật tường minh</b> — bịt trường hợp một máy chủ
///       thật vô tình chạy với <c>ASPNETCORE_ENVIRONMENT=Development</c>, thứ vẫn xảy ra.
///     </description>
///   </item>
/// </list>
/// <para>
/// Mặc định khi thiếu cờ là <b>không nạp</b>. Chọn mặc định ngược lại thì hàng rào thứ hai chỉ
/// còn là hình thức. Máy phát triển bật cờ sẵn trong <c>appsettings.Development.json</c>, nên
/// trải nghiệm "chạy lần đầu trên máy sạch là có dữ liệu" không đổi.
/// </para>
/// </summary>
public static class DemoSeedPolicy
{
    /// <summary>Khóa cấu hình bật bộ nạp demo. Đặt thành hằng để test và appsettings không lệch nhau.</summary>
    public const string EnabledKey = "DemoSeed:Enabled";

    public static bool ShouldSeedDemoData(IHostEnvironment environment, IConfiguration configuration)
        => environment.IsDevelopment() && configuration.GetValue(EnabledKey, false);
}
