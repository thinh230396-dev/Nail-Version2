using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities;

namespace NailManagement.Infrastructure.Persistence.Configurations;

public sealed class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("InvoicePayments");
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id).HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.InvoiceId).HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Cột này nhận cả số âm — BR-PAY-006 quy định hoàn tiền là một dòng mang dấu trừ.
        builder.Property(payment => payment.Amount).IsRequired();

        builder.Property(payment => payment.PaidAt).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(120);
        builder.Property(payment => payment.Reason).HasMaxLength(1000);
        builder.Property(payment => payment.CreatedByUserId).HasMaxLength(64);

        builder.HasIndex(payment => payment.InvoiceId);

        // BR-REV-001 — doanh thu tính theo tiền thực thu, nên báo cáo quét bảng này theo
        // tiệm và theo ngày trả tiền, không phải theo ngày lập hóa đơn.
        builder.HasIndex(payment => new { payment.TenantId, payment.PaidAt });
    }
}
