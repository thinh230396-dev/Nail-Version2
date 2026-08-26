using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Staff;

/// <summary>
/// Danh sách nhân viên của tiệm đang làm việc.
/// <para>
/// <b>Đây là endpoint đọc duy nhất tới giờ có hai phạm vi khác nhau theo vai trò.</b> Ma
/// trận mục 3.4 cho chủ tiệm xem cả tiệm, còn lễ tân chỉ xem <i>chi nhánh mình</i>. Bộ lọc
/// toàn cục ở <c>NailDbContext</c> không làm được việc này vì nó lọc theo tiệm cho mọi vai
/// trò như nhau, nên phép thu hẹp nằm ở đây.
/// </para>
/// <para>
/// Chi nhánh lấy từ <c>ActorContext.BranchId</c> — dựng từ phiên đăng nhập qua hồ sơ nhân
/// viên (BR-EMP-004), không bao giờ từ thân request. Nhận nó từ client là để lễ tân tự khai
/// mình thuộc chi nhánh nào.
/// </para>
/// </summary>
public sealed class ListStaffUseCase(
    IStaffRepository staffMembers,
    IUserRepository users)
{
    public async Task<IReadOnlyList<StaffDto>> ExecuteAsync(
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        var branchId = ResolveBranchScope(actor);

        var members = await staffMembers.ListAsync(branchId, cancellationToken);

        // Một lượt tra cho cả danh sách. Hỏi lẻ từng hồ sơ là mỗi lần mở màn hình lại tốn
        // đúng bằng số nhân viên lượt truy vấn, và màn này là màn chủ tiệm mở thường xuyên.
        var accounts = await users.ListByStaffIdsAsync(
            [.. members.Select(member => member.Id)], cancellationToken);

        return [.. members.Select(member =>
            StaffMapper.ToDto(member, accounts.TryGetValue(member.Id, out var account) ? account : null))];
    }

    /// <summary>
    /// Chi nhánh dùng để thu hẹp danh sách, hoặc <c>null</c> khi người gọi được xem cả tiệm.
    /// <para>
    /// Lễ tân mà phiên không dựng được chi nhánh là một trạng thái hỏng — hồ sơ nhân viên
    /// của họ bị gỡ, hoặc thuộc tiệm khác. Từ chối thẳng thay vì mặc định cho xem cả tiệm:
    /// một lỗi dữ liệu không được biến thành một lần nới quyền.
    /// </para>
    /// </summary>
    private static string? ResolveBranchScope(ActorContext actor)
    {
        if (actor.Role != UserRole.Receptionist) return null;

        return actor.BranchId ?? throw new ForbiddenException(
            "Tài khoản lễ tân này chưa gắn với chi nhánh nào nên không xem được danh sách nhân viên.");
    }
}
