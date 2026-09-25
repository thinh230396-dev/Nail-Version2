using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Platform.Tenants;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Configurations.Salon;

public sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(staff => staff.Id);

        // Khóa phụ cho khóa ngoại ghép — xem chú thích ở AppointmentConfiguration.
        builder.HasAlternateKey(staff => new { staff.Id, staff.TenantId });

        builder.Property(staff => staff.Id).HasMaxLength(64).IsRequired();
        builder.Property(staff => staff.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(staff => staff.BranchId).HasMaxLength(64).IsRequired();
        builder.Property(staff => staff.FullName).HasMaxLength(160).IsRequired();

        builder.Property(staff => staff.Phone)
            .HasConversion(
                phone => phone!.Value,
                value => PhoneNumber.FromPersistence(value))
            .HasMaxLength(PhoneNumber.MaxLength);

        builder.Property(staff => staff.Email).HasMaxLength(254);
        builder.Property(staff => staff.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(staff => staff.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // BR-EMP-009 — một ca cố định. Kiểu time của SQL Server đủ diễn đạt, không cần
        // lưu thành chuỗi hay thành số phút.
        builder.Property(staff => staff.ShiftStart).IsRequired();
        builder.Property(staff => staff.ShiftEnd).IsRequired();

        // BR-EMP-011 — tỷ lệ hoa hồng, ví dụ 0,1500 là 15%. Bốn chữ số thập phân là đủ
        // cho mọi mức hoa hồng thực tế mà vẫn tránh sai số của kiểu dấu phẩy động.
        builder.Property(staff => staff.CommissionRate).HasPrecision(5, 4).IsRequired();

        builder.Property(staff => staff.SkillsJson).IsRequired();
        builder.Property(staff => staff.CreatedAt).IsRequired();
        builder.Property(staff => staff.UpdatedAt).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(staff => staff.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(staff => staff.Branch)
            .WithMany()
            .HasForeignKey(staff => staff.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-EMP-008 đếm nhân viên chưa nghỉ việc theo tiệm; BR-ISO-004 lọc theo chi nhánh
        // cho tài khoản lễ tân. Hai chiều tra cứu, hai index.
        builder.HasIndex(staff => new { staff.TenantId, staff.Status });
        builder.HasIndex(staff => new { staff.BranchId, staff.Status });
    }
}
