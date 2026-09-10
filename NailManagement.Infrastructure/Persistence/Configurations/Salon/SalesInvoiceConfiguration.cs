using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.BranchId).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.CustomerId).HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.AppointmentId).HasMaxLength(64);
        builder.Property(invoice => invoice.StaffId).HasMaxLength(64);
        builder.Property(invoice => invoice.Code).HasMaxLength(40).IsRequired();
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.Subtotal).IsRequired();
        builder.Property(invoice => invoice.Discount).IsRequired();
        builder.Property(invoice => invoice.DiscountReason).HasMaxLength(1000);
        builder.Property(invoice => invoice.Tip).IsRequired();
        builder.Property(invoice => invoice.Total).IsRequired();
        builder.Property(invoice => invoice.Note).HasMaxLength(1000);
        builder.Property(invoice => invoice.CreatedByUserId).HasMaxLength(64);
        builder.Property(invoice => invoice.CreatedAt).IsRequired();
        builder.Property(invoice => invoice.UpdatedAt).IsRequired();
        builder.Property(invoice => invoice.CancelledAt);
        builder.Property(invoice => invoice.RefundedAt);

        // Ba thuộc tính tính ra từ các dòng con nên không có cột nào cả.
        builder.Ignore(invoice => invoice.Collected);
        builder.Ignore(invoice => invoice.Remaining);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(invoice => invoice.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // Bốn khóa ngoại ghép, kèm cột tiệm — xem chú thích dài ở AppointmentConfiguration. Ở
        // bảng này ràng buộc còn đáng giá hơn: một hóa đơn gắn nhầm sang khách hoặc kỹ thuật
        // viên của tiệm khác là con số doanh thu của hai tiệm cùng sai một lúc (BR-REV-004).
        builder.HasOne(invoice => invoice.Branch)
            .WithMany()
            .HasForeignKey(invoice => new { invoice.BranchId, invoice.TenantId })
            .HasPrincipalKey(branch => new { branch.Id, branch.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(invoice => invoice.Customer)
            .WithMany()
            .HasForeignKey(invoice => new { invoice.CustomerId, invoice.TenantId })
            .HasPrincipalKey(customer => new { customer.Id, customer.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(invoice => invoice.Appointment)
            .WithMany()
            .HasForeignKey(invoice => new { invoice.AppointmentId, invoice.TenantId })
            .HasPrincipalKey(appointment => new { appointment.Id, appointment.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(invoice => invoice.Staff)
            .WithMany()
            .HasForeignKey(invoice => new { invoice.StaffId, invoice.TenantId })
            .HasPrincipalKey(staff => new { staff.Id, staff.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(invoice => invoice.Lines)
            .WithOne(line => line.Invoice)
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(invoice => invoice.Payments)
            .WithOne(payment => payment.Invoice)
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(invoice => invoice.Lines).AutoInclude(false);
        builder.Navigation(invoice => invoice.Payments).AutoInclude(false);

        // BR-INV-016 — số hóa đơn duy nhất trong một tiệm. Ràng buộc này là hàng rào cuối
        // cùng nếu bộ đếm ở bảng InvoiceCounters có sai sót.
        builder.HasIndex(invoice => new { invoice.TenantId, invoice.Code }).IsUnique();

        // BR-REV-004 — ba trong bốn chiều của báo cáo doanh thu đọc theo các index này.
        builder.HasIndex(invoice => new { invoice.TenantId, invoice.CreatedAt });
        builder.HasIndex(invoice => new { invoice.BranchId, invoice.CreatedAt });
        builder.HasIndex(invoice => new { invoice.StaffId, invoice.CreatedAt });
        builder.HasIndex(invoice => new { invoice.CustomerId, invoice.Status });
    }
}
