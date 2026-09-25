using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.Appointments;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(appointment => appointment.Id);

        // Khóa phụ để hóa đơn bán hàng trỏ tới bằng khóa ngoại ghép — cùng lý do đã ghi ở phần
        // khóa ngoại bên dưới.
        builder.HasAlternateKey(appointment => new { appointment.Id, appointment.TenantId });

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

        /*
          Ba khóa ngoại dưới đây là khóa GHÉP, kèm luôn cột tiệm — và đó là toàn bộ ý nghĩa của
          chúng.

          Một lịch hẹn mang sẵn TenantId của chính nó, đồng thời trỏ tới chi nhánh, khách hàng và
          kỹ thuật viên. Với khóa ngoại một cột, database chỉ hỏi "chi nhánh này có thật không?"
          chứ không hỏi "nó có thuộc đúng tiệm ấy không" — nên một lịch hẹn của tiệm A gắn vào chi
          nhánh của tiệm B là hoàn toàn hợp lệ ở tầng dữ liệu.

          Tầng Application hiện ngăn được chuyện đó, nhưng nó ngăn bằng cách nhớ kiểm tra ở từng
          use case. Khóa ghép biến phép kiểm ấy thành thứ không thể quên: mọi đường ghi đều đi qua
          nó, kể cả một lệnh UPDATE gõ tay hay một lần nhập dữ liệu.

          Cần khóa phụ (Id, TenantId) ở phía được trỏ tới, khai trong ba configuration tương ứng.
        */
        builder.HasOne(appointment => appointment.Branch)
            .WithMany()
            .HasForeignKey(appointment => new { appointment.BranchId, appointment.TenantId })
            .HasPrincipalKey(branch => new { branch.Id, branch.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(appointment => appointment.Customer)
            .WithMany()
            .HasForeignKey(appointment => new { appointment.CustomerId, appointment.TenantId })
            .HasPrincipalKey(customer => new { customer.Id, customer.TenantId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(appointment => appointment.Staff)
            .WithMany()
            .HasForeignKey(appointment => new { appointment.StaffId, appointment.TenantId })
            .HasPrincipalKey(staff => new { staff.Id, staff.TenantId })
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
