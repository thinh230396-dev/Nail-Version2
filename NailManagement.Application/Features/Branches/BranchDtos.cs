namespace NailManagement.Application.Features.Branches;

/// <summary>
/// Một chi nhánh của tiệm đang làm việc.
/// <para>
/// <paramref name="TenantId"/> luôn bằng tiệm trong phiên: bộ lọc toàn cục ở tầng lưu trữ
/// không cho chi nhánh của tiệm khác lọt tới đây (BR-ISO-002). Nó vẫn được trả ra để màn
/// hình đối chiếu được khi người dùng đổi tiệm giữa chừng.
/// </para>
/// <para>
/// Chi nhánh đã ngừng hoạt động vẫn nằm trong danh sách — BR-DEL-003 yêu cầu tên của nó
/// tiếp tục hiện đúng trong lịch hẹn và hóa đơn cũ.
/// </para>
/// </summary>
public sealed record BranchDto(
    string Id,
    string TenantId,
    string Name,
    string? Code,
    string? Address,
    string? Phone,
    bool IsPrimary,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Thêm chi nhánh — BR-BRANCH-005 đối chiếu số chi nhánh đang hoạt động với hạn mức gói.
/// <para>
/// Không có ô <c>isPrimary</c>: BR-BRANCH-001 quy định chi nhánh chính sinh ra cùng tiệm và
/// mỗi tiệm có đúng một. Để lộ ô đó ra API là mở đường cho tiệm có hai chi nhánh chính,
/// hoặc không còn cái nào.
/// </para>
/// <para>
/// Cũng không có ô <c>tenantId</c>: tiệm đến từ phiên đăng nhập (BR-AUTH-024). Nhận nó từ
/// thân request là tin vào một lời khai của trình duyệt.
/// </para>
/// </summary>
public sealed record CreateBranchCommand(
    string Name,
    string? Code,
    string? Address,
    string? Phone);

public sealed record UpdateBranchCommand(
    string BranchId,
    string Name,
    string? Code,
    string? Address,
    string? Phone);

/// <param name="Status">
/// <c>ACTIVE</c> hoặc <c>INACTIVE</c>. BR-BRANCH-004 — "xóa chi nhánh" chính là chuyển sang
/// <c>INACTIVE</c>; hệ thống không có lệnh xóa cứng nào (BR-DEL-001).
/// </param>
public sealed record ChangeBranchStatusCommand(string BranchId, string Status);
