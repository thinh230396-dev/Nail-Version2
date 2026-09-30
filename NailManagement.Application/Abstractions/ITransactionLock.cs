namespace NailManagement.Application.Abstractions;

/// <summary>
/// Khóa theo tên cho tới hết giao dịch hiện tại — lời đáp cho mọi phép "kiểm rồi mới ghi".
/// <para>
/// Kiểm và ghi là hai bước; không khóa thì hai request cùng qua phép kiểm rồi cùng ghi. Nơi dùng:
/// </para>
/// <list type="bullet">
///   <item><c>staff-schedule:{staffId}</c> — BR-APT-011, không đặt trùng lịch một kỹ thuật viên.</item>
///   <item><c>quota:branches:{tenantId}</c> — BR-BRANCH-005, không vượt hạn mức chi nhánh.</item>
///   <item><c>quota:staff:{tenantId}</c> — BR-EMP-008, không vượt hạn mức nhân viên.</item>
/// </list>
/// <para>
/// Khóa theo đúng thứ đang được bảo vệ chứ không khóa cả bảng: đặt lịch cho hai người khác nhau,
/// hay thêm chi nhánh cho hai tiệm khác nhau, vẫn chạy song song.
/// </para>
/// </summary>
public interface ITransactionLock
{
    /// <summary>
    /// Chờ tới lượt rồi giữ khóa tới hết giao dịch. Trả <c>false</c> khi hết giờ chờ — nơi gọi
    /// quyết định nói gì với người dùng.
    /// <para>
    /// Phải gọi <b>bên trong</b> <see cref="IUnitOfWork"/>; gọi ngoài giao dịch là lỗi lập trình và
    /// ném ngoại lệ ngay, thay vì lặng lẽ không khóa gì.
    /// </para>
    /// </summary>
    Task<bool> TryAcquireAsync(string resource, CancellationToken cancellationToken = default);
}
