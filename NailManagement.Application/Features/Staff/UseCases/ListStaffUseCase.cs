using NailManagement.Application.Common;
using NailManagement.Application.Features.Staff;
using NailManagement.Domain.Auth;
using NailManagement.Domain.Salon.StaffMembers;

namespace NailManagement.Application.Features.Staff.UseCases;

/// <summary>
/// Danh sách nhân viên của tiệm đang làm việc.
/// <para>
/// Ma trận mục 3.4 cho chủ tiệm xem cả tiệm, còn lễ tân chỉ xem <i>chi nhánh mình</i>. Bộ
/// lọc toàn cục ở <c>NailDbContext</c> không làm được việc này vì nó lọc theo tiệm cho mọi
/// vai trò như nhau, nên phép thu hẹp nằm ở <see cref="Common.BranchScope"/> — chỗ dùng
/// chung với lịch hẹn và hóa đơn bán hàng, đúng ba dòng cuối của bảng BR-ISO-004.
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
        var branchId = BranchScope.Resolve(actor, "danh sách nhân viên");

        var members = await staffMembers.ListAsync(branchId, cancellationToken);

        // Một lượt tra cho cả danh sách. Hỏi lẻ từng hồ sơ là mỗi lần mở màn hình lại tốn
        // đúng bằng số nhân viên lượt truy vấn, và màn này là màn chủ tiệm mở thường xuyên.
        var accounts = await users.ListByStaffIdsAsync(
            [.. members.Select(member => member.Id)], cancellationToken);

        return [.. members.Select(member =>
            StaffMapper.ToDto(member, accounts.TryGetValue(member.Id, out var account) ? account : null))];
    }
}
