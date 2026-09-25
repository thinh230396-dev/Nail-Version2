using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Ánh xạ <see cref="AppUser"/> xuống bảng.
/// <para>
/// Dùng Fluent API thay vì attribute để entity ở tầng Domain không phải mang bất kỳ
/// annotation nào của EF Core — giữ Domain sạch khỏi công nghệ lưu trữ.
/// </para>
/// </summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasMaxLength(64).IsRequired();

        // Value object Email được lưu thành một cột chuỗi bình thường.
        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.FromPersistence(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.Property(u => u.Username).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordSalt).HasMaxLength(128).IsRequired();

        // Lưu vai trò và trạng thái dưới dạng CHUỖI, không phải số: khi mở bảng ra xem
        // trong SQL Server Object Explorer lúc demo thì đọc được ngay, không phải tra
        // xem 2 nghĩa là gì.
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(u => u.FailedAttempts).IsRequired();
        builder.Property(u => u.LockedUntil);
        builder.Property(u => u.StaffId).HasMaxLength(64);

        // BR-AUTH-013 — tài khoản lễ tân bắt buộc trỏ tới một hồ sơ nhân viên đã tồn tại.
        // Khóa ngoại này (thêm ở ngày 2) là thứ khiến quy tắc đó không thể lách được bằng
        // cách ghi thẳng vào database. Không xóa lan: BR-DEL-001 cấm xóa cứng.
        builder.HasOne<Staff>()
            .WithMany()
            .HasForeignKey(u => u.StaffId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        // BR-AUTH-013 — mỗi hồ sơ nhân viên có NHIỀU NHẤT một tài khoản đăng nhập.
        //
        // Khóa ngoại phía trên bảo đảm hồ sơ được trỏ tới là có thật, nhưng không nói gì về số
        // lượng: hai tài khoản cùng trỏ một hồ sơ vẫn hợp lệ với nó. Tầng Application có kiểm,
        // song hai request cấp tài khoản chạy sát nhau thì cả hai cùng thấy "chưa có" rồi cùng
        // ghi. Hậu quả không nằm ở chỗ cấp thừa một tài khoản: ListByStaffIdsAsync gom kết quả
        // bằng ToDictionary theo mã hồ sơ, nên từ lúc đó MÀN DANH SÁCH NHÂN VIÊN ném lỗi và
        // không ai mở được nữa.
        //
        // Lọc bỏ NULL vì phần lớn tài khoản không gắn hồ sơ nào — cùng lý do với chỉ số trên
        // cột Username ngay dưới đây.
        builder.HasIndex(u => u.StaffId)
            .IsUnique()
            .HasFilter("[StaffId] IS NOT NULL");

        // BR-VAL-001 — email duy nhất toàn hệ thống.
        builder.HasIndex(u => u.Email).IsUnique();

        // Username cũng duy nhất, nhưng được phép null. SQL Server coi nhiều NULL là
        // trùng nhau trong unique index, nên phải lọc bỏ NULL ra.
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("[Username] IS NOT NULL");

        builder.HasIndex(u => new { u.Role, u.Status });
    }
}
