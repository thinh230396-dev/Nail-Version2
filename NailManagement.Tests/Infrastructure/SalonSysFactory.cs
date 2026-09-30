using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NailManagement.API.Startup;
using NailManagement.Application.Abstractions;
using NailManagement.Infrastructure.Persistence;

namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Dựng lại <b>đúng máy chủ thật</b> trong bộ nhớ, trên một database riêng.
/// <para>
/// Vì sao đi qua HTTP chứ không gọi thẳng use case: bốn trong sáu kịch bản mà lộ trình §5 liệt
/// kê đều nằm ở tầng HTTP — <c>RequirePermission</c> là một bộ lọc của MVC,
/// <c>TenantWriteGuardMiddleware</c> và <c>SessionMiddleware</c> là middleware. Gọi thẳng
/// <c>ExecuteAsync</c> thì cả ba đều không chạy, và bộ kiểm thử sẽ xanh trong khi hệ thống thật
/// vẫn hở. Chuỗi bốn bước của BR-TENANT-013 chỉ tồn tại khi request đi trọn đường ống.
/// </para>
/// <para>
/// Database nằm trên cùng máy chủ SQL Server với lúc chạy thật, chỉ khác tên. Không dùng EF Core
/// InMemory vì nó <b>không cưỡng chế chỉ số duy nhất và không có giao dịch thật</b> — bộ kiểm
/// thử sẽ xanh ở đúng những chỗ đáng lẽ phải đỏ. Không dùng SQLite vì nó khác provider ở những
/// chỗ mà mã này thật sự dùng tới: <c>datetimeoffset</c>, <c>AsSplitQuery</c>, và vài phép dịch
/// LINQ.
/// </para>
/// <para>
/// Database bị xóa trước mỗi lần chạy, rồi máy chủ tự dựng lại từ migration và nạp bộ dữ liệu
/// mẫu ngay lúc khởi động — cùng đoạn mã ở <c>Program.cs</c> mà lần chạy thật dùng. Nhờ vậy bộ
/// kiểm thử có sẵn <b>hai tiệm và ba vai trò</b> mà không cần một bộ nạp thứ hai để phải giữ cho
/// khớp với bộ nạp thật.
/// </para>
/// </summary>
public class SalonSysFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Trần đăng nhập theo IP mà bộ kiểm thử dùng — cố tình đặt rất cao.
    /// <para>
    /// Máy chủ dựng trong bộ nhớ không có địa chỉ IP thật, nên <b>mọi</b> request của cả lần
    /// chạy rơi vào cùng một ngăn đếm. Một lần chạy đăng nhập hơn trăm lượt; để nguyên trần
    /// mặc định 30 thì hàng loạt lớp kiểm thử sẽ đỏ vì <c>429</c>, và đỏ vì một lý do không
    /// liên quan gì tới thứ chúng kiểm.
    /// </para>
    /// <para>
    /// Nới trần chứ không tắt hẳn bộ giới hạn: chuỗi middleware vẫn phải chạy đúng như lúc
    /// thật. Riêng hàng rào ấy được kiểm bằng <see cref="ThrottledLoginFactory"/>, nơi trần
    /// được siết xuống vừa đủ để chạm tới.
    /// </para>
    /// </summary>
    private const string TestPermitLimit = "100000";

    /// <summary>
    /// Tên database cố ý khác hẳn database chạy thật. Trùng tên là mỗi lần chạy kiểm thử lại xóa
    /// sạch dữ liệu mà người dùng đang thao tác dở trên trình duyệt.
    /// <para>
    /// ⚠️ <b>Ghi đè chuỗi kết nối bằng <c>ConfigureAppConfiguration</c> là KHÔNG ĐỦ</b>, và đó là
    /// một cái bẫy đã thật sự sập suốt hai ngày: <c>Program.cs</c> gọi
    /// <c>AddInfrastructure(builder.Configuration)</c> ngay ở dòng thứ hai, và
    /// <c>Infrastructure/DependencyInjection.cs</c> đọc <c>GetConnectionString("Default")</c>
    /// <b>ngay lúc đăng ký service</b> rồi giữ luôn chuỗi ấy trong closure của
    /// <c>UseSqlServer</c>. Callback của <c>ConfigureAppConfiguration</c> thì bị hoãn tới
    /// <c>builder.Build()</c> — tức là chạy SAU khi giá trị đã bị đọc xong. Lệnh ghi đè vì vậy
    /// không bao giờ có tác dụng, và máy chủ kiểm thử nối thẳng vào database demo.
    /// </para>
    /// <para>
    /// Cách chữa là đặt <b>biến môi trường</b> trong hàm khởi tạo, tức trước cả khi host được
    /// dựng. <c>WebApplication.CreateBuilder</c> luôn nạp sẵn nguồn biến môi trường, và nguồn đó
    /// xếp trên <c>appsettings.json</c>, nên giá trị đã sẵn sàng đúng lúc dòng đọc kia chạy.
    /// </para>
    /// <para>
    /// Máy khác đặt biến <c>NAILMANAGEMENT_TEST_DB</c> để thay cả chuỗi — CI chạy SQL Server
    /// trong container Linux, nơi không có đăng nhập Windows. Tên database trong chuỗi thay thế
    /// cũng phải khác database thật, vì cùng lý do ở trên.
    /// </para>
    /// </summary>
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("NAILMANAGEMENT_TEST_DB") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : "Server=localhost;Database=NailManagementTests;"
              + "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public SalonSysFactory() : this(dropDatabase: true)
    {
    }

    /// <param name="dropDatabase">
    /// Chỉ factory dùng chung của cả lần chạy mới được xóa database. Factory phụ — như
    /// <see cref="ThrottledLoginFactory"/> — dựng thêm một máy chủ trên <b>cùng</b> database đã
    /// có sẵn, và nếu nó cũng xóa thì nó vừa dọn sạch dữ liệu mà mọi lớp kiểm thử khác đang dùng.
    /// </param>
    protected SalonSysFactory(bool dropDatabase)
    {
        // Thứ tự bắt buộc: đặt biến môi trường TRƯỚC. DropDatabase ngay dưới đây dùng thẳng
        // hằng số nên nó luôn xóa đúng chỗ, nhưng máy chủ thì đọc cấu hình — và nếu dòng này
        // chạy sau, máy chủ đã kịp nối vào database demo.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);

        if (dropDatabase) DropDatabase();
    }

    /// <summary>
    /// Trần đăng nhập theo IP mà máy chủ này chạy với. Lớp con ghi đè để siết lại.
    /// </summary>
    protected virtual string LoginPermitLimit => TestPermitLimit;

    /// <summary>
    /// Tên môi trường. Lớp con ghi đè để kiểm những nhánh chỉ chạy ngoài Development.
    /// </summary>
    protected virtual string EnvironmentName => "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Môi trường Development vì Program.cs chỉ bỏ qua chuyển hướng HTTPS ở môi trường đó.
        // Ở môi trường khác, mọi request http:// của bộ kiểm thử sẽ nhận 307 và cookie phiên
        // không bao giờ được gửi kèm.
        builder.UseEnvironment(EnvironmentName);

        // Giữ lại phép ghi đè này dù biến môi trường ở hàm khởi tạo mới là thứ thật sự có tác
        // dụng: nó phủ nốt những chỗ đọc cấu hình MUỘN hơn — sau khi host đã dựng xong — và khi
        // đó hai nguồn nói cùng một giá trị. Bỏ đi thì một lần đọc muộn nào đó về sau sẽ lặng lẽ
        // rơi về appsettings.json. Xem chú thích ở ConnectionString để biết vì sao một mình nó
        // không đủ: câu "nguồn này chạy sau nên nó thắng" đúng về thứ tự nguồn, sai về thời điểm.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Auth:LoginRateLimit:PermitLimit"] = LoginPermitLimit,

                // Bộ kiểm thử SỐNG BẰNG dữ liệu mẫu: hai tiệm, ba vai trò, lịch hẹn và hóa đơn
                // đều do bộ nạp demo dựng. Từ ngày 24 bộ nạp ấy đòi một cờ bật tường minh
                // (DemoSeedPolicy), nên harness phải tự khai ra thứ mình cần thay vì trông vào
                // appsettings.Development.json tình cờ được nạp kèm.
                [DemoSeedPolicy.EnabledKey] = "true"
            }));
    }

    /// <summary>
    /// Xóa database <b>trước khi</b> máy chủ khởi động, vì chính lúc khởi động nó mới chạy
    /// migration và nạp dữ liệu mẫu. Xóa sau là xóa mất thứ vừa nạp.
    /// <para>
    /// Mỗi lần chạy bắt đầu từ số 0 nên kết quả không phụ thuộc vào lần chạy trước — kể cả lần
    /// trước có dừng giữa chừng ngay sau một phép thử vừa sửa dữ liệu.
    /// </para>
    /// </summary>
    private static void DropDatabase()
    {
        var options = new DbContextOptionsBuilder<NailDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        // Bộ lọc theo tiệm ở NailDbContext hỏi cổng này ở mọi truy vấn. Ở đây không có phiên nào
        // nên nó trả rỗng, và điều đó vô hại: lệnh xóa database không đi qua bộ lọc nào.
        using var db = new NailDbContext(options, new NoTenantContext());

        db.Database.EnsureDeleted();
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public string? ActiveTenantId => null;
    }
}
