using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Staff;
using NailManagement.Domain.Auditing;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Application.Features.Staff.UseCases;

/// <summary>
/// Đổi trạng thái một nhân viên — BR-EMP-005, và đây cũng là thứ giao diện gọi là "cho nghỉ việc".
/// <para>
/// Không có lệnh xóa cứng nào (BR-DEL-001): hồ sơ người đã nghỉ vẫn ở lại để tên họ hiện
/// đúng trong lịch hẹn và hóa đơn cũ (BR-EMP-006, BR-DEL-003), họ chỉ không nhận lịch hẹn
/// mới nữa (BR-EMP-007).
/// </para>
/// <para>
/// <b>Nghỉ việc kéo theo vô hiệu hóa tài khoản đăng nhập</b> — quyết định 32 chốt ngày
/// 26/08. Không rule nào viết sẵn điều này, nhưng để trống thì một lễ tân đã nghỉ vẫn đăng
/// nhập và thu tiền được ở quầy; đó là lỗ hổng chứ không phải một cách hiểu khác. Nhờ
/// BR-AUTH-022, phiên đang mở của họ chết ngay ở request kế tiếp.
/// </para>
/// <para>
/// Chiều ngược lại <b>không</b> tự động: nhận lại một người cũ khôi phục hồ sơ nhân sự chứ
/// không khôi phục quyền đăng nhập. Cấp lại quyền là một quyết định riêng và phải được bấm
/// riêng qua lệnh cấp tài khoản.
/// </para>
/// <para>
/// Nhận lại người cũ phải qua hạn mức <c>max_staff</c> — cùng một phép đếm với lúc thêm mới
/// (<see cref="StaffQuotaGuard"/>). Ngày 5 đã cho thấy bỏ phép kiểm ở đường "bật lại" là đủ
/// để vượt hạn mức bằng cách tắt rồi bật.
/// </para>
/// </summary>
public sealed class ChangeStaffStatusUseCase(
    IStaffRepository staffMembers,
    IUserRepository users,
    StaffQuotaGuard quota,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    public async Task<StaffDto> ExecuteAsync(
        ChangeStaffStatusCommand command, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var status = StaffMapper.ParseStatus(command.Status);

        var staff = await staffMembers.FindByIdAsync(command.StaffId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhân viên.");

        var account = await users.FindByStaffIdAsync(staff.Id, cancellationToken);

        // Chỉ kiểm hạn mức khi đây thật sự là một lần NHẬN LẠI. Gửi WORKING cho người vốn
        // đang đi làm là thao tác không đổi gì, mà phép đếm khi đó lại tính cả chính họ —
        // tiệm đang dùng vừa đủ hạn mức sẽ bị từ chối một việc họ không hề làm.
        if (status != StaffStatus.Inactive && staff.Status == StaffStatus.Inactive)
        {
            var tenantId = tenantContext.ActiveTenantId ?? throw new TenantNotSelectedException();
            await quota.EnsureRoomForOneMoreAsync(tenantId, isReactivating: true, cancellationToken);
        }

        // Tài khoản chỉ bị đụng tới khi đây là một lần nghỉ việc THẬT. Gửi INACTIVE cho hồ
        // sơ vốn đã nghỉ không nên vô hiệu hóa lại một tài khoản mà ai đó vừa cố ý cấp lại.
        var deactivatesAccount =
            status == StaffStatus.Inactive
            && staff.Status != StaffStatus.Inactive
            && account is not null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            staff.ChangeStatus(status, now);
            await staffMembers.UpdateAsync(staff, ct);

            if (deactivatesAccount)
            {
                account!.Deactivate(now);
                await users.UpdateAsync(account, ct);
            }

            return true;
        }, cancellationToken);

        // Nhật ký ghi SAU khi giao dịch chốt — nằm trong giao dịch thì nó bị hủy theo khi
        // có lỗi, và một dòng "đã khóa tài khoản" cho việc chưa xảy ra còn tệ hơn không có.
        if (deactivatesAccount)
        {
            await audit.RecordAsync(
                new AuditEntry(
                    AuditEvent.AccountLocked,
                    actor.UserId,
                    actor.Role,
                    staff.TenantId,
                    nameof(AppUser),
                    account!.Id,
                    actor.Ip,
                    new Dictionary<string, string>
                    {
                        ["reason"] = "Nhân viên nghỉ việc",
                        ["staffId"] = staff.Id,
                        ["staffName"] = staff.FullName
                    }),
                cancellationToken);
        }

        return StaffMapper.ToDto(staff, account);
    }
}
