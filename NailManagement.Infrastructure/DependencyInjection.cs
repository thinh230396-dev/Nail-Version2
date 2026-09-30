using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.Auditing;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Platform.Subscriptions;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Salon.Invoices;
using NailManagement.Domain.Salon.Revenue;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Infrastructure.Auditing;
using NailManagement.Infrastructure.Persistence;
using NailManagement.Infrastructure.Persistence.Auditing;
using NailManagement.Infrastructure.Persistence.Auth;
using NailManagement.Infrastructure.Persistence.Platform.Packages;
using NailManagement.Infrastructure.Persistence.Platform.Subscriptions;
using NailManagement.Infrastructure.Persistence.Platform.Tenants;
using NailManagement.Infrastructure.Persistence.Salon.Appointments;
using NailManagement.Infrastructure.Persistence.Salon.Branches;
using NailManagement.Infrastructure.Persistence.Salon.Customers;
using NailManagement.Infrastructure.Persistence.Salon.Invoices;
using NailManagement.Infrastructure.Persistence.Salon.Revenue;
using NailManagement.Infrastructure.Persistence.Salon.Services;
using NailManagement.Infrastructure.Persistence.Salon.StaffMembers;
using NailManagement.Infrastructure.Persistence.Seed;
using NailManagement.Infrastructure.Persistence.TenantScope;
using NailManagement.Infrastructure.Security;
using NailManagement.Infrastructure.SystemServices;

namespace NailManagement.Infrastructure;

/// <summary>
/// Đăng ký mọi bản cài đặt cụ thể của tầng Infrastructure.
/// <para>
/// <b>Đây là nơi duy nhất quyết định cổng nào được cắm bởi lớp nào.</b> Tầng Application chỉ
/// khai báo mình cần <c>IPasswordHasher</c>; việc đó hóa ra là PBKDF2 hay bcrypt thì nó
/// không biết và không cần biết.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Thiếu chuỗi kết nối 'ConnectionStrings:Default' trong appsettings.");

        services.AddDbContext<NailDbContext>(options => options
            .UseSqlServer(connectionString)

            // Ba bảng nền tảng — hóa đơn đăng ký, yêu cầu nâng gói, liên kết tài khoản với
            // tiệm — trỏ tới một tiệm có thể đã bị xóa mềm. EF Core cảnh báo rằng khi đó
            // phép nối theo quan hệ ấy sẽ không thấy tiệm dù khóa ngoại vẫn có giá trị. Ở đây điều đó
            // ĐÚNG như thiết kế: BR-TENANT-022 giữ lại hóa đơn của tiệm đã xóa, và chính
            // vì vậy các bảng đó chép sẵn tên tiệm thành cột riêng thay vì đọc qua điều
            // hướng. Tắt cảnh báo để nhật ký lúc chạy không bị lấp bởi ba dòng đã biết.
            .ConfigureWarnings(warnings => warnings.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        // Phạm vi tiệm đang làm việc sống theo từng request, và CÙNG một đối tượng phải
        // được cả DbContext lẫn middleware phiên nhìn thấy — nếu hai bên cầm hai bản khác
        // nhau thì bộ lọc cách ly tenant sẽ đọc một giá trị đã cũ.
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<AmbientTenantContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserTenantRepository, UserTenantRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<ISubscriptionInvoiceRepository, SubscriptionInvoiceRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();

        // Đọc cùng bảng hóa đơn nhưng theo GIỜ THU chứ không theo giờ lập — xem IRevenueRepository.
        services.AddScoped<IRevenueRepository, RevenueRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddScoped<IAuditLogger, AuditLogger>();

        // Ranh giới giao dịch. Theo vòng đời request vì nó bọc quanh chính DbContext của
        // request đó; một bản dùng chung sẽ mở giao dịch trên một kết nối khác với kết nối
        // mà các repository đang ghi, và khi đó nó không gom được gì cả.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Kiểm tra sẵn sàng: máy chủ chỉ nhận lưu lượng khi nối được database. Gắn nhãn "ready"
        // để phân biệt với kiểm tra sống — tiến trình còn chạy nhưng database tạm mất thì nên
        // rút khỏi bộ cân bằng tải, không nên bị khởi động lại.
        services.AddHealthChecks()
            .AddDbContextCheck<NailDbContext>("database", tags: [HealthCheckTags.Ready]);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordGenerator, RandomPasswordGenerator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();

        services.AddScoped<DemoAccountSeeder>();
        services.AddScoped<DemoDataSeeder>();

        // Bộ nạp của môi trường KHÔNG phải Development: một tài khoản quản trị duy nhất, mật
        // khẩu đến từ cấu hình. Xem DemoSeedPolicy để biết vì sao hai bộ nạp không cùng chạy.
        services.AddScoped<BootstrapAdminSeeder>();

        return services;
    }
}
