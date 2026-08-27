using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Auth;
using StaffEntity = NailManagement.Domain.Entities.Salon.Staff;

namespace NailManagement.Application.UseCases.Appointments;

/// <summary>
/// BR-APT-002 — lễ tân chỉ thao tác lịch hẹn thuộc chi nhánh mình, chủ tiệm thao tác cả tiệm.
/// <para>
/// Gom về một chỗ vì phép thu hẹp này lặp lại ở cả năm use case của lát cắt, và bốn trong số
/// đó là lệnh ghi. Để mỗi use case tự viết là để ngỏ khả năng một cái quên, rồi lễ tân chi
/// nhánh Quận 3 hủy được lịch của Quận 1 mà không ai thấy — bộ lọc toàn cục ở
/// <c>NailDbContext</c> không bắt được ca này, vì hai chi nhánh ấy cùng một tiệm.
/// </para>
/// <para>
/// Là lớp tĩnh vì nó không cần tới kho dữ liệu nào: mọi thứ để quyết định đã nằm sẵn trong
/// <see cref="ActorContext"/> và trong chính bản ghi.
/// </para>
/// </summary>
public static class AppointmentScope
{
    /// <summary>
    /// Chi nhánh dùng để thu hẹp danh sách, hoặc <c>null</c> khi người gọi được xem cả tiệm.
    /// <para>
    /// Giống hệt phép thu hẹp ở <c>ListStaffUseCase</c>, kể cả cách xử lý ca hỏng: lễ tân mà
    /// phiên không dựng được chi nhánh thì bị từ chối thẳng, chứ không mặc định cho xem cả
    /// tiệm. Một lỗi dữ liệu không được biến thành một lần nới quyền.
    /// </para>
    /// </summary>
    public static string? ResolveBranchScope(ActorContext actor)
    {
        if (actor.Role != UserRole.Receptionist) return null;

        return actor.BranchId ?? throw new ForbiddenException(
            "Tài khoản lễ tân này chưa gắn với chi nhánh nào nên không xem được lịch hẹn.");
    }

    /// <summary>
    /// Bản ghi mà người gọi không được chạm tới thì coi như không tồn tại.
    /// <para>
    /// Trả <see cref="NotFoundException"/> chứ không phải lỗi phân quyền, cùng lý do đã ghi ở
    /// chính lớp đó: một câu "bạn không có quyền với lịch hẹn này" là lời xác nhận rằng lịch
    /// hẹn ấy có thật, và ghép nhiều câu như vậy lại là dò ra được lịch làm việc của chi
    /// nhánh khác.
    /// </para>
    /// </summary>
    public static Appointment EnsureInScope(Appointment? appointment, ActorContext actor)
    {
        if (appointment is null) throw new NotFoundException("Không tìm thấy lịch hẹn.");

        var branchId = ResolveBranchScope(actor);

        if (branchId is not null && appointment.BranchId != branchId)
            throw new NotFoundException("Không tìm thấy lịch hẹn.");

        return appointment;
    }

    /// <summary>
    /// Kỹ thuật viên mà lễ tân được phép xếp lịch cho — cũng chỉ trong chi nhánh mình.
    /// <para>
    /// Phải kiểm riêng khỏi <see cref="EnsureInScope"/>: chi nhánh của một lịch hẹn là chi
    /// nhánh của người làm (BR-EMP-003), nên nếu chỉ kiểm bản ghi cũ thì lễ tân vẫn dời được
    /// một lịch của mình sang cho người của chi nhánh khác, và lịch ấy lập tức biến mất khỏi
    /// tầm nhìn của chính họ.
    /// </para>
    /// </summary>
    public static StaffEntity EnsureAssignable(StaffEntity staff, ActorContext actor)
    {
        var branchId = ResolveBranchScope(actor);

        if (branchId is not null && staff.BranchId != branchId)
        {
            throw new NotFoundException(
                "Không tìm thấy kỹ thuật viên này trong chi nhánh của bạn.");
        }

        return staff;
    }
}
