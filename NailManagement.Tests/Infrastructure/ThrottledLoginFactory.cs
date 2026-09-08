namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Một máy chủ thứ hai, giống hệt máy chủ dùng chung nhưng <b>trần đăng nhập siết xuống 3</b>.
/// <para>
/// Có mặt vì hàng rào chống dò mật khẩu không kiểm được trên máy chủ dùng chung: ở đó trần
/// được nới lên rất cao để một lần chạy hơn trăm lượt đăng nhập không tự khóa mình. Muốn thật
/// sự chạm tới hàng rào thì phải có một máy chủ mà trần nằm trong tầm với.
/// </para>
/// <para>
/// <b>Không xóa database.</b> Nó dùng lại đúng database mà factory dùng chung đã dựng và nạp —
/// bộ nạp dữ liệu mẫu chỉ chạy khi bảng gói còn rỗng nên máy chủ này khởi động và bỏ qua bước
/// nạp. Xóa ở đây là dọn sạch dữ liệu mà mọi lớp kiểm thử khác đang dùng dở.
/// </para>
/// <para>
/// Bộ đếm của bộ giới hạn nằm trong bộ nhớ của từng máy chủ, nên hai máy chủ có hai bộ đếm
/// riêng: lượt đăng nhập của các lớp khác không đẩy lớp này tới trần, và ngược lại.
/// </para>
/// </summary>
public sealed class ThrottledLoginFactory : SalonSysFactory
{
    /// <summary>Số lần đăng nhập cho phép trong một cửa sổ, ở máy chủ này.</summary>
    public const int PermitLimit = 3;

    public ThrottledLoginFactory() : base(dropDatabase: false)
    {
    }

    protected override string LoginPermitLimit => PermitLimit.ToString();
}
