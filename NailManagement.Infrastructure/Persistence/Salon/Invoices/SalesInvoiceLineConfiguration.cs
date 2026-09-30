using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Salon.Invoices;

namespace NailManagement.Infrastructure.Persistence.Salon.Invoices;

public sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ToTable("SalesInvoiceLines");
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasMaxLength(64).IsRequired();
        builder.Property(line => line.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(line => line.InvoiceId).HasMaxLength(64).IsRequired();

        // BR-INV-012 — rỗng nghĩa là dòng nhập tay, không gắn dịch vụ nào trong danh mục.
        builder.Property(line => line.ServiceId).HasMaxLength(64);

        builder.Property(line => line.Name).HasMaxLength(160).IsRequired();
        builder.Property(line => line.UnitPrice).IsRequired();
        builder.Property(line => line.Quantity).IsRequired();

        // Thành tiền là phép nhân của hai cột đã có. Lưu thêm một cột nữa là tạo ra chỗ
        // để ba con số nói ba chuyện khác nhau.
        builder.Ignore(line => line.LineTotal);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(line => line.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(line => line.TenantId);
        builder.HasIndex(line => line.InvoiceId);

        // BR-REV-004 — chiều "dịch vụ" của báo cáo doanh thu gom nhóm trên bảng này.
        builder.HasIndex(line => new { line.TenantId, line.ServiceId });
    }
}
