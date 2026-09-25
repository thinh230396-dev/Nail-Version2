using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Staff;
using NailManagement.Domain.Salon.Branches;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

using StaffEntity = NailManagement.Domain.Salon.StaffMembers.Staff;

namespace NailManagement.Application.Features.Staff.UseCases;

/// <summary>
/// Thêm hồ sơ nhân viên — BR-EMP-008.
/// <para>
/// Đếm nhân viên <i>chưa nghỉ việc</i>, không đếm tất cả: BR-EMP-006 giữ hồ sơ người đã
/// nghỉ ở lại để tên họ vẫn hiện đúng trong lịch hẹn và hóa đơn cũ, và bắt tiệm trả hạn mức
/// cho những bản ghi đó là phạt họ vì chuyện nhân viên đã nghỉ việc. Phép đếm nằm ở
/// <see cref="StaffQuotaGuard"/> vì nó dùng chung với đường nhận lại người cũ.
/// </para>
/// <para>
/// Hồ sơ mới <b>không</b> kèm tài khoản đăng nhập, kể cả với lễ tân. BR-AUTH-013 quy định
/// đúng một quy trình: tạo hồ sơ trước, rồi bấm "Cấp tài khoản đăng nhập" trên hồ sơ đó.
/// </para>
/// </summary>
public sealed class CreateStaffUseCase(
    IStaffRepository staffMembers,
    IBranchRepository branches,
    StaffQuotaGuard quota,
    ITenantContext tenantContext,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<StaffDto> ExecuteAsync(
        CreateStaffCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        // Chi nhánh của tiệm khác không tìm thấy được ở đây — bộ lọc toàn cục lo phần đó —
        // nên một mã chi nhánh lạ chỉ ra được lỗi gắn vào ô chọn chi nhánh, không ra được
        // một hồ sơ nhân viên nằm ở tiệm người khác.
        var branch = await branches.FindByIdAsync(command.BranchId ?? string.Empty, cancellationToken)
            ?? throw DomainException.ForField("branchId", "Không tìm thấy chi nhánh đã chọn.");

        var role = StaffMapper.ParseRole(command.Role);
        var shiftStart = StaffMapper.ParseShift(command.ShiftStart, "shiftStart", "Giờ bắt đầu ca");
        var shiftEnd = StaffMapper.ParseShift(command.ShiftEnd, "shiftEnd", "Giờ kết thúc ca");

        await quota.EnsureRoomForOneMoreAsync(tenantId, isReactivating: false, cancellationToken);

        var staff = StaffEntity.Create(
            ids.NewId("STF"),
            tenantId,
            branch.Id,
            Guard.NotEmpty(command.FullName, "fullName", "Tên nhân viên", ValidationPolicy.NameMaxLength),
            command.Phone,
            command.Email,
            role,
            shiftStart,
            shiftEnd,
            command.CommissionRate,
            StaffMapper.ToSkillsJson(command.Skills),
            now);

        await staffMembers.AddAsync(staff, cancellationToken);

        // Hồ sơ vừa tạo chắc chắn chưa có tài khoản đăng nhập, nên không cần một lượt tra nữa.
        return StaffMapper.ToDto(staff, null);
    }
}
