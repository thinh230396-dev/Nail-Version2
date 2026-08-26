using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Staff;

/// <summary>
/// Sửa hồ sơ nhân viên, kể cả chuyển chi nhánh — BR-EMP-003.
/// <para>
/// Chuyển chi nhánh chỉ là sửa một trường, không phải một nghiệp vụ riêng: rule nói thẳng
/// như vậy, và lịch hẹn cũ vẫn giữ chi nhánh của chính nó nên không có gì phải dời theo.
/// </para>
/// <para>
/// <b>Đổi vai trò nghiệp vụ bị chặn khi hồ sơ đã có tài khoản đăng nhập.</b> Cho qua thì
/// hệ thống còn lại một tài khoản lễ tân trỏ tới hồ sơ kỹ thuật viên — thứ mà BR-AUTH-002
/// nói không tồn tại, và <c>AppUser.AttachStaff</c> đã từ chối ngay từ lúc cấp. Chặn ở đây
/// để không có cửa sau đi vòng qua phép chặn đó.
/// </para>
/// <para>
/// Không đụng tới trạng thái. Nghỉ việc và đi làm lại đi qua endpoint đổi trạng thái riêng,
/// để việc cho một người nghỉ không bao giờ xảy ra như tác dụng phụ của một lần sửa số
/// điện thoại.
/// </para>
/// </summary>
public sealed class UpdateStaffUseCase(
    IStaffRepository staffMembers,
    IBranchRepository branches,
    IUserRepository users,
    IClock clock)
{
    public async Task<StaffDto> ExecuteAsync(
        UpdateStaffCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var staff = await staffMembers.FindByIdAsync(command.StaffId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhân viên.");

        var branch = await branches.FindByIdAsync(command.BranchId ?? string.Empty, cancellationToken)
            ?? throw DomainException.ForField("branchId", "Không tìm thấy chi nhánh đã chọn.");

        var role = StaffMapper.ParseRole(command.Role);
        var shiftStart = StaffMapper.ParseShift(command.ShiftStart, "shiftStart", "Giờ bắt đầu ca");
        var shiftEnd = StaffMapper.ParseShift(command.ShiftEnd, "shiftEnd", "Giờ kết thúc ca");

        var account = await users.FindByStaffIdAsync(staff.Id, cancellationToken);

        if (role != staff.Role && account is not null)
        {
            throw DomainException.ForField(
                "role",
                "Hồ sơ này đang có tài khoản đăng nhập nên không đổi được vai trò. "
                + "Vô hiệu hóa tài khoản trước, hoặc lập hồ sơ mới cho vai trò kia.");
        }

        staff.UpdateProfile(
            Guard.NotEmpty(command.FullName, "fullName", "Tên nhân viên", ValidationPolicy.NameMaxLength),
            command.Phone,
            command.Email,
            shiftStart,
            shiftEnd,
            command.CommissionRate,
            StaffMapper.ToSkillsJson(command.Skills),
            now);

        if (branch.Id != staff.BranchId) staff.MoveToBranch(branch.Id, now);

        if (role != staff.Role) staff.ChangeRole(role, now);

        await staffMembers.UpdateAsync(staff, cancellationToken);

        return StaffMapper.ToDto(staff, account);
    }
}
