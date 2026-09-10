using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);

        // Khóa phụ cho khóa ngoại ghép — xem chú thích ở AppointmentConfiguration.
        builder.HasAlternateKey(customer => new { customer.Id, customer.TenantId });

        builder.Property(customer => customer.Id).HasMaxLength(64).IsRequired();
        builder.Property(customer => customer.TenantId).HasMaxLength(64).IsRequired();

        // ⚠️ Khi tra cứu khách theo số điện thoại, phải dựng PhoneNumber.FromPersistence(...)
        // TRƯỚC rồi so sánh cả đối tượng. Viết `customer.Phone.Value == "090..."` ngay trong
        // biểu thức LINQ sẽ ném lỗi lúc chạy: EF Core không dịch được lời gọi vào bên trong
        // một value object đã đi qua bộ chuyển đổi. UserRepository đang làm đúng cách này
        // với Email.
        builder.Property(customer => customer.Phone)
            .HasConversion(
                phone => phone.Value,
                value => PhoneNumber.FromPersistence(value))
            .HasMaxLength(PhoneNumber.MaxLength)
            .IsRequired();

        builder.Property(customer => customer.FullName).HasMaxLength(160);
        builder.Property(customer => customer.Email).HasMaxLength(254);
        builder.Property(customer => customer.BirthDate);
        builder.Property(customer => customer.Note).HasMaxLength(1000);
        builder.Property(customer => customer.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(customer => customer.CreatedAt).IsRequired();
        builder.Property(customer => customer.UpdatedAt).IsRequired();

        builder.HasOne(customer => customer.Tenant)
            .WithMany()
            .HasForeignKey(customer => customer.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-CUS-002 — số điện thoại duy nhất TRONG MỘT tiệm. Ràng buộc ghép hai cột chứ
        // không phải ràng buộc trên riêng cột số điện thoại: một người được là khách của
        // nhiều tiệm, và chặn điều đó là chặn nhầm.
        builder.HasIndex(customer => new { customer.TenantId, customer.Phone }).IsUnique();
        builder.HasIndex(customer => new { customer.TenantId, customer.Status });
    }
}
