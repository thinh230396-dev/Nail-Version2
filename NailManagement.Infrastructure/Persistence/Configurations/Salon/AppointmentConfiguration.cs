using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Entities.Salon;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.Id).HasMaxLength(64).IsRequired();
        builder.Property(appointment => appointment.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(appointment => appointment.BranchId).HasMaxLength(64).IsRequired();
        builder.Property(appointment => appointment.CustomerId).HasMaxLength(64).IsRequired();
        builder.Property(appointment => appointment.StaffId).HasMaxLength(64).IsRequired();
        builder.Property(appointment => appointment.StartAt).IsRequired();
        builder.Property(appointment => appointment.EndAt).IsRequired();
        builder.Property(appointment => appointment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(appointment => appointment.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(appointment => appointment.Station).HasMaxLength(80);
        builder.Property(appointment => appointment.Note).HasMaxLength(1000);
        builder.Property(appointment => appointment.Deposit).IsRequired();
        builder.Property(appointment => appointment.CompletedWithUnpaidBalance).IsRequired();
        builder.Property(appointment => appointment.CreatedByUserId).HasMaxLength(64);
        builder.Property(appointment => appointment.CreatedAt).IsRequired();
        builder.Property(appointment => appointment.UpdatedAt).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(appointment => appointment.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(appointment => appointment.Branch)
            .WithMany()
            .HasForeignKey(appointment => appointment.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(appointment => appointment.Customer)
            .WithMany()
            .HasForeignKey(appointment => appointment.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(appointment => appointment.Staff)
            .WithMany()
            .HasForeignKey(appointment => appointment.StaffId)
            .OnDelete(DeleteBehavior.NoAction);

        // Các dòng dịch vụ thuộc hẳn về lịch hẹn: chúng không có đời sống riêng, nên đây là
        // chỗ hiếm hoi dùng xóa lan — và cũng chỉ có tác dụng khi lịch hẹn bị xóa cứng,
        // điều mà BR-APT-024 không cho phép xảy ra qua giao diện.
        builder.HasMany(appointment => appointment.Services)
            .WithOne(line => line.Appointment)
            .HasForeignKey(line => line.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(appointment => appointment.Services).AutoInclude(false);

        // BR-APT-011 — index cốt lõi của phép chống trùng lịch: tra theo kỹ thuật viên rồi
        // quét theo mốc thời gian. Thiếu index này thì mỗi lần đặt lịch phải quét cả bảng.
        builder.HasIndex(appointment => new { appointment.StaffId, appointment.StartAt });

        // Hai chiều xem lịch của giao diện: cả tiệm theo ngày, và theo từng chi nhánh.
        builder.HasIndex(appointment => new { appointment.TenantId, appointment.StartAt });
        builder.HasIndex(appointment => new { appointment.BranchId, appointment.StartAt });
        builder.HasIndex(appointment => new { appointment.CustomerId, appointment.StartAt });
    }
}
