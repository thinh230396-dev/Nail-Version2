using NailManagement.Application.Features.Branches;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.Features.Branches.UseCases;

/// <summary>
/// Danh sách chi nhánh của tiệm đang làm việc.
/// <para>
/// Không nhận tham số nào, kể cả mã tiệm: phạm vi đến từ phiên đăng nhập (BR-AUTH-024) và
/// bộ lọc toàn cục ở tầng lưu trữ áp nó vào truy vấn (BR-ISO-002). Đó là lý do use case này
/// ngắn tới mức trông như thừa — phần khó đã được cưỡng chế ở chỗ không ai quên được.
/// </para>
/// <para>
/// Lễ tân cũng gọi được, ở mức chỉ xem theo ma trận mục 3.4: họ cần biết tiệm có những chi
/// nhánh nào để đọc đúng tên trên lịch hẹn.
/// </para>
/// </summary>
public sealed class ListBranchesUseCase(IBranchRepository branches)
{
    public async Task<IReadOnlyList<BranchDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var all = await branches.ListAsync(cancellationToken);

        return [.. all.Select(BranchMapper.ToDto)];
    }
}
