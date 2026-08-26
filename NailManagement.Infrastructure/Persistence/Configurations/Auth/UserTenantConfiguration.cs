using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Entities.Platform;

namespace NailManagement.Infrastructure.Persistence.Configurations.Auth;

public sealed class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("UserTenants");

        // Khóa chính ghép hai cột: một tài khoản không thể được gán hai lần vào cùng một
        // tiệm, và ràng buộc đó do database giữ chứ không phụ thuộc vào code nhớ kiểm tra.
        builder.HasKey(link => new { link.UserId, link.TenantId });

        builder.Property(link => link.UserId).HasMaxLength(64).IsRequired();
        builder.Property(link => link.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(link => link.CreatedAt).IsRequired();

        builder.HasOne(link => link.User)
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Tenant)
            .WithMany()
            .HasForeignKey(link => link.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-ISO-003 bước 2 — mỗi request của chủ tiệm đều tra bảng này để xác nhận tài
        // khoản có quyền với tiệm đang làm việc, nên chiều tra theo tiệm cũng cần index.
        builder.HasIndex(link => link.TenantId);
    }
}
