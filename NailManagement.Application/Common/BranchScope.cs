using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Domain.Common;
using NailManagement.Domain.Enums.Auth;

namespace NailManagement.Application.Common;

/// <summary>
/// BR-ISO-004 — lễ tân chỉ thao tác lịch hẹn, nhân viên và hóa đơn bán hàng <b>thuộc chi
/// nhánh mình</b>; chủ tiệm thao tác cả tiệm.
/// <para>
/// Đây là chỗ <b>duy nhất</b> viết luật đó. Bộ lọc toàn cục ở <c>NailDbContext</c> không làm
/// thay được: nó lọc theo tiệm như nhau cho mọi vai trò, nên không biểu diễn nổi một phép thu
/// hẹp chỉ áp cho một vai. Trước khi gom về đây, luật này đã có hai bản chép ở
/// <c>ListStaffUseCase</c> và <c>AppointmentScope</c>, và lát cắt hóa đơn sắp thành bản thứ
/// ba — trong khi báo cáo doanh thu còn cần bản thứ tư.
/// </para>
/// <para>
/// Là lớp tĩnh vì nó không cần tới kho dữ liệu nào: mọi thứ để quyết định đã nằm trong
/// <see cref="ActorContext"/> và trong chính bản ghi.
/// </para>
/// </summary>
public static class BranchScope
{
    /// <summary>
    /// Chi nhánh dùng để thu hẹp một danh sách, hoặc <c>null</c> khi người gọi được xem cả tiệm.
    /// <para>
    /// Lễ tân mà phiên không dựng được chi nhánh là một trạng thái hỏng — hồ sơ nhân viên của
    /// họ bị gỡ, hoặc thuộc tiệm khác. Từ chối thẳng thay vì mặc định cho xem cả tiệm: một lỗi
    /// dữ liệu không được biến thành một lần nới quyền.
    /// </para>
    /// </summary>
    /// <param name="subject">
    /// Thứ đang bị từ chối, để câu thông báo nói đúng việc người dùng vừa làm — ví dụ
    /// <c>"danh sách nhân viên"</c>, <c>"lịch hẹn"</c>, <c>"hóa đơn"</c>.
    /// </param>
    public static string? Resolve(ActorContext actor, string subject)
    {
        if (actor.Role != UserRole.Receptionist) return null;

        return actor.BranchId ?? throw new ForbiddenException(
            $"Tài khoản lễ tân này chưa gắn với chi nhánh nào nên không xem được {subject}.");
    }

    /// <summary>
    /// Bản ghi mà người gọi không được chạm tới thì coi như không tồn tại.
    /// <para>
    /// Trả <see cref="NotFoundException"/> chứ không phải lỗi phân quyền, cùng lý do đã ghi ở
    /// chính lớp đó: một câu "bạn không có quyền với bản ghi này" là lời xác nhận rằng nó có
    /// thật, và ghép nhiều câu như vậy lại là dò ra được hoạt động của chi nhánh khác.
    /// </para>
    /// </summary>
    /// <param name="notFound">Câu chữ gọi đúng tên loại bản ghi, ví dụ <c>"Không tìm thấy lịch hẹn."</c></param>
    public static TEntity EnsureInScope<TEntity>(TEntity? entity, ActorContext actor, string notFound)
        where TEntity : class, IBranchOwned
    {
        if (entity is null) throw new NotFoundException(notFound);

        var branchId = Resolve(actor, "bản ghi này");

        if (branchId is not null && entity.BranchId != branchId) throw new NotFoundException(notFound);

        return entity;
    }
}
