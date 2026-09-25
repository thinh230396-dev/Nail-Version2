using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class InvoiceCounterConfiguration : IEntityTypeConfiguration<InvoiceCounter>
{
    public void Configure(EntityTypeBuilder<InvoiceCounter> builder)
    {
        builder.ToTable("InvoiceCounters");

        // BR-INV-016 — một dòng cho mỗi cặp tiệm và ngày. Khóa chính ghép chính là thứ
        // ngăn hai quầy cùng tạo bộ đếm của cùng một ngày.
        builder.HasKey(counter => new { counter.TenantId, counter.BusinessDate });

        builder.Property(counter => counter.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(counter => counter.BusinessDate).IsRequired();
        builder.Property(counter => counter.LastNumber).IsRequired();
    }
}
