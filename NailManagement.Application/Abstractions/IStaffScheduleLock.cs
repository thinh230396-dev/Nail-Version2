namespace NailManagement.Application.Abstractions;

/// <summary>
/// Khóa lịch làm việc của <b>một</b> kỹ thuật viên cho tới hết giao dịch hiện tại.
/// <para>
/// BR-APT-011 là phép "kiểm khung giờ còn trống rồi mới ghi" — hai bước. Không khóa thì hai quầy
/// đặt cùng người cùng giờ đều qua phép kiểm và cùng ghi: khách bị xếp chồng lên nhau, và không
/// ràng buộc nào ở database bắt được hai <i>khoảng</i> thời gian chồng nhau.
/// </para>
/// <para>
/// Khóa theo từng người chứ không khóa cả bảng: đặt lịch cho hai kỹ thuật viên khác nhau vẫn chạy
/// song song; chỉ các lần đặt cho cùng một người mới xếp hàng, mỗi lượt vài mili giây.
/// </para>
/// </summary>
public interface IStaffScheduleLock
{
    /// <summary>
    /// Chờ tới lượt rồi giữ khóa. Phải gọi <b>bên trong</b> <see cref="IUnitOfWork"/>: khóa gắn với
    /// giao dịch và tự nhả khi giao dịch kết thúc. Gọi ngoài giao dịch là lỗi lập trình và ném
    /// ngoại lệ ngay, thay vì lặng lẽ không khóa gì.
    /// </summary>
    Task AcquireAsync(string staffId, CancellationToken cancellationToken = default);
}
