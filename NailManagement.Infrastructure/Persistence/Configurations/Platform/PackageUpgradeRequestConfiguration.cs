using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Platform.Subscriptions;

namespace NailManagement.Infrastructure.Persistence.Configurations.Platform;

public sealed class PackageUpgradeRequestConfiguration : IEntityTypeConfiguration<PackageUpgradeRequest>
{
    public void Configure(EntityTypeBuilder<PackageUpgradeRequest> builder)
    {
        builder.ToTable("PackageUpgradeRequests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id).HasMaxLength(64).IsRequired();
        builder.Property(request => request.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(request => request.TenantName).HasMaxLength(200).IsRequired();
        builder.Property(request => request.RequestedByUserId).HasMaxLength(64);
        builder.Property(request => request.RequestedByName).HasMaxLength(160).IsRequired();
        builder.Property(request => request.RequestedByEmail).HasMaxLength(254).IsRequired();
        builder.Property(request => request.CurrentPackageId).HasMaxLength(64);
        builder.Property(request => request.CurrentPackageName).HasMaxLength(80).IsRequired();
        builder.Property(request => request.RequestedPackageId).HasMaxLength(64).IsRequired();
        builder.Property(request => request.RequestedPackageName).HasMaxLength(80).IsRequired();
        builder.Property(request => request.BillingCycle).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.EffectiveDate).IsRequired();
        builder.Property(request => request.QuotedAmount).IsRequired();
        builder.Property(request => request.Note).HasMaxLength(1000);
        builder.Property(request => request.ReviewNote).HasMaxLength(1000);
        builder.Property(request => request.ReviewedByUserId).HasMaxLength(64);
        builder.Property(request => request.InvoiceId).HasMaxLength(64);
        builder.Property(request => request.RequestedAt).IsRequired();

        builder.HasOne(request => request.Tenant)
            .WithMany()
            .HasForeignKey(request => request.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-SUB-009 — mỗi tiệm chỉ có tối đa một yêu cầu đang chờ. Phép kiểm tra đó là
        // một câu đếm chạy trước khi ghi, và đây là index nó dựa vào.
        builder.HasIndex(request => new { request.TenantId, request.Status });
        builder.HasIndex(request => new { request.Status, request.RequestedAt });
    }
}
