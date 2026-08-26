namespace NailManagement.Domain.Enums.Platform;

/// <summary>
/// BR-SUB-010 — yêu cầu nâng cấp gói không xóa được, chỉ chuyển sang duyệt hoặc từ chối.
/// BR-SUB-009: mỗi tenant chỉ có tối đa một yêu cầu đang <c>Pending</c>.
/// </summary>
public enum UpgradeRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}
