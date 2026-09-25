namespace NailManagement.Domain.Platform.Packages;

/// <summary>
/// BR-SUB-001 — bốn trạng thái của gói dịch vụ.
/// BR-SUB-002: gói <c>Deprecated</c> không nhận đăng ký mới nhưng tenant đang dùng vẫn giữ nguyên.
/// BR-SUB-003: gói không xóa được khi còn tenant sử dụng, chỉ chuyển sang <c>Archived</c>.
/// </summary>
public enum PackageStatus
{
    Draft = 1,
    Active = 2,
    Deprecated = 3,
    Archived = 4
}
