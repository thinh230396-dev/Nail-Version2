using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Platform;

namespace NailManagement.Infrastructure.Persistence.Configurations.Platform;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");
        builder.HasKey(package => package.Id);

        builder.Property(package => package.Id).HasMaxLength(64).IsRequired();
        builder.Property(package => package.Name).HasMaxLength(80).IsRequired();
        builder.Property(package => package.Description).HasMaxLength(500);

        // BR-VAL-003 — tiền là VND số nguyên, nên bigint chứ không phải decimal.
        builder.Property(package => package.Price).IsRequired();

        builder.Property(package => package.BillingCycle).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(package => package.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(package => package.MaxSalons).IsRequired();
        builder.Property(package => package.MaxStaff).IsRequired();
        builder.Property(package => package.Version).IsRequired();

        // BR-SUB-007 — quyền tính năng theo gói. Lưu JSON vì danh sách này luôn được đọc
        // nguyên khối; chưa có màn hình nào cần truy vấn "gói nào có quyền X".
        builder.Property(package => package.CapabilitiesJson).IsRequired();
        builder.Property(package => package.FeaturesJson).IsRequired();
        builder.Property(package => package.LimitsJson).IsRequired();

        builder.Property(package => package.Color).HasMaxLength(20);
        builder.Property(package => package.CreatedAt).IsRequired();
        builder.Property(package => package.UpdatedAt).IsRequired();

        builder.HasIndex(package => package.Name).IsUnique();
        builder.HasIndex(package => package.Status);
    }
}
