using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NailManagement.API.Startup;

namespace NailManagement.Tests.Startup;

/// <summary>
/// Hàng rào chặn tài khoản demo sinh ra ngoài máy phát triển.
///
/// <para>
/// Là bộ kiểm thử <b>đơn vị</b> chứ không đi qua HTTP, và đó là lựa chọn có cân nhắc. Muốn chứng
/// minh bằng một máy chủ thật thì phải dựng nó trên một database <b>trống và riêng</b>; nhưng cả
/// harness hiện tại chia nhau đúng một database qua biến môi trường <c>ConnectionStrings__Default</c>
/// đặt trong hàm khởi tạo factory, nên một database thứ hai sẽ đổi biến ấy giữa chừng và kéo theo
/// những lớp kiểm thử khác nối nhầm chỗ. Đổi lấy rủi ro đó để lấy một phép khẳng định mạnh hơn là
/// một món hời tồi.
/// </para>
/// <para>
/// Điều kiện được kiểm ở đây là <b>toàn bộ</b> quyết định: <c>DatabaseBootstrap</c> không có nhánh
/// nào khác dẫn tới bộ nạp demo.
/// </para>
/// </summary>
public sealed class DemoSeedPolicyTests
{
    [Theory]
    // Máy phát triển đã bật cờ — đây là trường hợp duy nhất được nạp.
    [InlineData("Development", "true", true)]
    // Cùng môi trường ấy nhưng cờ tắt: người vận hành đã nói không, và lời ấy phải được nghe.
    [InlineData("Development", "false", false)]
    // Và đây là trường hợp lần vá này sinh ra để chặn: một máy chủ thật vô tình chạy với
    // ASPNETCORE_ENVIRONMENT sai, hoặc một bản triển khai quên tắt cờ.
    [InlineData("Staging", "true", false)]
    [InlineData("Production", "true", false)]
    [InlineData("Production", "false", false)]
    public void Chi_moi_truong_Development_da_bat_co_moi_duoc_nap_demo(
        string environmentName, string flag, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DemoSeedPolicy.EnabledKey] = flag
            })
            .Build();

        var shouldSeed = DemoSeedPolicy.ShouldSeedDemoData(new FakeEnvironment(environmentName), configuration);

        Assert.Equal(expected, shouldSeed);
    }

    [Fact]
    public void Thieu_han_cau_hinh_thi_khong_nap()
    {
        var empty = new ConfigurationBuilder().Build();

        // Mặc định phải là KHÔNG nạp. Mặc định ngược lại thì hàng rào thứ hai chỉ còn hình thức,
        // vì phần lớn bản triển khai không khai gì về khóa này cả.
        Assert.False(DemoSeedPolicy.ShouldSeedDemoData(new FakeEnvironment("Development"), empty));
        Assert.False(DemoSeedPolicy.ShouldSeedDemoData(new FakeEnvironment("Production"), empty));
    }

    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "NailManagement.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
