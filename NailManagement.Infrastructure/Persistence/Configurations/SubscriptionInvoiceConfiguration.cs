using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities;

namespace NailManagement.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionInvoiceConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
    {
        builder.ToTable("SubscriptionInvoices");
        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.Code).HasMaxLength(40).IsRequired();
        builder.Property(invoice => invoice.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.TenantName).HasMaxLength(200).IsRequired();
        builder.Property(invoice => invoice.PackageId).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.PackageName).HasMaxLength(80).IsRequired();
        builder.Property(invoice => invoice.Amount).IsRequired();
        builder.Property(invoice => invoice.BillingCycle).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.Reason).HasMaxLength(80).IsRequired();
        builder.Property(invoice => invoice.PeriodStart).IsRequired();
        builder.Property(invoice => invoice.PeriodEnd).IsRequired();
        builder.Property(invoice => invoice.DueAt).IsRequired();
        builder.Property(invoice => invoice.PaymentReference).HasMaxLength(120);
        builder.Property(invoice => invoice.PaymentNote).HasMaxLength(1000);
        builder.Property(invoice => invoice.ConfirmedByUserId).HasMaxLength(64);
        builder.Property(invoice => invoice.CreatedAt).IsRequired();
        builder.Property(invoice => invoice.UpdatedAt).IsRequired();

        // BR-TENANT-022 — hóa đơn của tiệm đã xóa mềm vẫn ở lại và vẫn tính vào doanh thu
        // nền tảng, nên quan hệ này không bao giờ được xóa lan.
        builder.HasOne(invoice => invoice.Tenant)
            .WithMany()
            .HasForeignKey(invoice => invoice.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(invoice => invoice.Code).IsUnique();
        builder.HasIndex(invoice => new { invoice.TenantId, invoice.CreatedAt });
        builder.HasIndex(invoice => new { invoice.Status, invoice.DueAt });
    }
}
