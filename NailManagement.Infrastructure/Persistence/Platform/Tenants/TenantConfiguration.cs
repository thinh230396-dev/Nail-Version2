using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Platform.Packages;
using NailManagement.Domain.Platform.Tenants;

namespace NailManagement.Infrastructure.Persistence.Platform.Tenants;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id).HasMaxLength(64).IsRequired();
        builder.Property(tenant => tenant.Code).HasMaxLength(32).IsRequired();
        builder.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
        builder.Property(tenant => tenant.Address).HasMaxLength(300);
        builder.Property(tenant => tenant.Phone).HasMaxLength(20);
        builder.Property(tenant => tenant.ContactEmail).HasMaxLength(254);
        builder.Property(tenant => tenant.Timezone).HasMaxLength(64).IsRequired();

        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(tenant => tenant.BillingCycle).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(tenant => tenant.IsTrial).IsRequired();
        builder.Property(tenant => tenant.ExpiresAt).IsRequired();
        builder.Property(tenant => tenant.SubscriptionPrice).IsRequired();
        builder.Property(tenant => tenant.SubscriptionPackageVersion).IsRequired();
        builder.Property(tenant => tenant.SubscriptionStartedAt).IsRequired();
        builder.Property(tenant => tenant.DeletedAt);
        builder.Property(tenant => tenant.CreatedAt).IsRequired();
        builder.Property(tenant => tenant.UpdatedAt).IsRequired();

        builder.Property(tenant => tenant.PackageId).HasMaxLength(64).IsRequired();

        // BR-SUB-003 — gói không xóa được khi còn tiệm dùng, nên quan hệ này cố ý KHÔNG
        // xóa lan: cơ sở dữ liệu từ chối luôn thay vì âm thầm kéo theo dữ liệu tiệm.
        builder.HasOne<Package>()
            .WithMany()
            .HasForeignKey(tenant => tenant.PackageId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-VAL-001 — mã tiệm duy nhất toàn hệ thống.
        builder.HasIndex(tenant => tenant.Code).IsUnique();

        // Hai cột này là đầu vào của phép tính trạng thái hiển thị ở BR-TENANT-002, chạy
        // trên mọi màn hình danh sách tenant của Superadmin.
        builder.HasIndex(tenant => new { tenant.Status, tenant.ExpiresAt });
        builder.HasIndex(tenant => tenant.DeletedAt);
    }
}
