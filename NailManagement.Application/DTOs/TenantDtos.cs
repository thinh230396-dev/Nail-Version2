namespace NailManagement.Application.DTOs;

/// <summary>
/// Một tiệm trong màn chọn tiệm — BR-AUTH-025.
/// <para>
/// <paramref name="DisplayStatus"/> là kết quả TÍNH lúc đọc theo BR-TENANT-002, không phải
/// cột trong database. Nhờ vậy tiệm vừa quá hạn hiện đúng trạng thái ngay lần bấm kế tiếp
/// mà hệ thống không cần một tiến trình chạy nền nào.
/// </para>
/// </summary>
public sealed record TenantSummaryDto(
    string Id,
    string Code,
    string Name,
    string DisplayStatus,
    bool IsReadOnly,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Tiệm đang làm việc của phiên, kèm những thứ mà tầng phân quyền cần ở mỗi request.
/// <para>
/// <paramref name="Capabilities"/> là danh sách quyền mà gói của tiệm mở (BR-SUB-007). Nó
/// đi kèm ở đây thay vì để tầng trên tự đi đọc bảng gói, vì bước 2 của BR-TENANT-013 chạy
/// trên MỌI request — thêm một lượt truy vấn nữa là thêm một lượt cho mỗi lần bấm chuột.
/// </para>
/// </summary>
public sealed record TenantScopeDto(
    string Id,
    string Code,
    string Name,
    string DisplayStatus,
    bool IsReadOnly,
    string PackageId,
    string PackageName,
    IReadOnlyList<string> Capabilities);
