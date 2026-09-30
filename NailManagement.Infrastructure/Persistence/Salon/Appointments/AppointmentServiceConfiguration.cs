using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Salon.Services;
using NailManagement.Domain.Salon.Appointments;

namespace NailManagement.Infrastructure.Persistence.Salon.Appointments;

public sealed class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> builder)
    {
        builder.ToTable("AppointmentServices");
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasMaxLength(64).IsRequired();
        builder.Property(line => line.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(line => line.AppointmentId).HasMaxLength(64).IsRequired();
        builder.Property(line => line.ServiceId).HasMaxLength(64).IsRequired();
        builder.Property(line => line.ServiceName).HasMaxLength(160).IsRequired();
        builder.Property(line => line.DurationMinutes).IsRequired();
        builder.Property(line => line.BufferMinutes).IsRequired();

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(line => line.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-ISO-001 — bảng con cũng mang mã tiệm và cũng được lọc, chứ không dựa vào việc
        // "đằng nào cũng phải đi qua lịch hẹn". Truy vấn báo cáo hoàn toàn có thể đọc thẳng
        // bảng này, và khi đó điều kiện lọc phải có sẵn ở đây.
        builder.HasIndex(line => line.TenantId);
        builder.HasIndex(line => line.AppointmentId);
    }
}
