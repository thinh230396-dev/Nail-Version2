using System.Reflection;
using Microsoft.EntityFrameworkCore;
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
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// Ngữ cảnh EF Core của hệ thống.
/// <para>
/// Đặt ở tầng Infrastructure chứ không phải Application: <c>DbContext</c> là chi tiết kỹ
/// thuật của việc lưu trữ. Tầng trong chỉ biết tới các interface repository ở
/// <c>NailManagement.Domain/Repositories</c>.
/// </para>
/// <para>
/// <b>Đây cũng là lớp truy vấn dùng chung mà BR-ISO-002 yêu cầu.</b> Xem
/// <see cref="ApplyTenantFilter{TEntity}"/> — mọi entity mang <see cref="ITenantOwned"/>
/// đều được gắn sẵn điều kiện lọc theo tiệm đang làm việc, nên không endpoint nào phải tự
/// viết điều kiện đó, và cũng không endpoint nào quên viết được.
/// </para>
/// </summary>
public class NailDbContext(DbContextOptions<NailDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    private static readonly MethodInfo TenantFilterMethod =
        typeof(NailDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    // ── Xác thực ──────────────────────────────────────────────────────────────
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();

    // ── Nền tảng (Superadmin) ─────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();
    public DbSet<PackageUpgradeRequest> PackageUpgradeRequests => Set<PackageUpgradeRequest>();

    // ── Nghiệp vụ tiệm ────────────────────────────────────────────────────────
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<InvoiceCounter> InvoiceCounters => Set<InvoiceCounter>();

    // ── Hệ thống ──────────────────────────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Tiệm đang làm việc, đọc lại ở mỗi câu truy vấn.
    /// <para>
    /// Phải là một thuộc tính của chính <c>DbContext</c> thì EF Core mới đưa được nó vào bộ
    /// lọc dưới dạng tham số và đọc lại giá trị mới ở từng lần chạy. Nhét thẳng giá trị vào
    /// biểu thức lọc sẽ khiến giá trị của request đầu tiên bị nhớ rồi dùng lại cho các
    /// request sau — đúng kiểu lỗi lộ dữ liệu chéo tiệm mà lộ trình cảnh báo.
    /// </para>
    /// </summary>
    private string? ActiveTenantId => tenantContext.ActiveTenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NailDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;

            TenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }

        // BR-DEL-002 — tiệm đã xóa mềm biến mất khỏi mọi danh sách. Màn hình nào của
        // Superadmin muốn xem lại thì phải gọi IgnoreQueryFilters một cách có chủ đích.
        modelBuilder.Entity<Tenant>().HasQueryFilter(tenant => tenant.DeletedAt == null);
    }

    /// <summary>
    /// BR-ISO-001/002 — gắn điều kiện lọc theo tiệm cho một bảng nghiệp vụ.
    /// <para>
    /// Vế đầu <c>ActiveTenantId != null</c> làm cho bộ lọc <b>đóng lại khi không rõ phạm vi</b>:
    /// request chưa có tiệm đang làm việc thì không đọc được dòng nào, thay vì đọc được tất
    /// cả. Đây chính là điều BR-AUTH-030 đòi hỏi — Superadmin không chạm được vào dữ liệu
    /// nghiệp vụ bên trong tiệm — và nó vẫn đúng cả khi lập trình viên quên đặt phạm vi.
    /// </para>
    /// </summary>
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
        => modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => ActiveTenantId != null && entity.TenantId == ActiveTenantId);
}
