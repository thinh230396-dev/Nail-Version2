using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Id).HasMaxLength(64).IsRequired();
        builder.Property(branch => branch.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(branch => branch.Name).HasMaxLength(80).IsRequired();
        builder.Property(branch => branch.Code).HasMaxLength(32);
        builder.Property(branch => branch.Address).HasMaxLength(300);
        builder.Property(branch => branch.Phone).HasMaxLength(20);
        builder.Property(branch => branch.IsPrimary).IsRequired();
        builder.Property(branch => branch.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(branch => branch.CreatedAt).IsRequired();
        builder.Property(branch => branch.UpdatedAt).IsRequired();

        builder.HasOne(branch => branch.Tenant)
            .WithMany()
            .HasForeignKey(branch => branch.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-VAL-001 — tên chi nhánh duy nhất TRONG MỘT tiệm, không phải toàn hệ thống:
        // hai tiệm khác nhau đều có quyền đặt tên chi nhánh là "Quận 1".
        builder.HasIndex(branch => new { branch.TenantId, branch.Name }).IsUnique();

        // BR-ISO-001 — index theo tiệm cho mọi bảng nghiệp vụ; kèm trạng thái vì
        // BR-BRANCH-005 đếm số chi nhánh đang hoạt động mỗi lần tạo chi nhánh mới.
        builder.HasIndex(branch => new { branch.TenantId, branch.Status });
    }
}
