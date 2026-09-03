namespace NailManagement.Application.Common;

/// <summary>
/// Giờ theo đồng hồ treo tường của tiệm.
/// <para>
/// Hệ thống lưu mọi mốc thời gian kèm phần bù múi giờ (<c>datetimeoffset</c>) nên bản thân dữ
/// liệu không cần tới lớp này. Nó chỉ trả lời đúng một câu hỏi mà dữ liệu không tự trả lời
/// được: <b>"hôm nay" là ngày nào</b> — dùng khi người gọi không nói rõ khoảng ngày, và khi
/// cấp số hóa đơn theo ngày làm việc (BR-INV-016).
/// </para>
/// <para>
/// Gắn cứng múi giờ Việt Nam vì toàn hệ thống phục vụ tiệm nail trong nước: tiền tệ là VND số
/// nguyên (BR-VAL-003), số điện thoại và ngày sinh đều theo quy ước trong nước. Một tham số
/// cấu hình cho việc này là dựng sẵn hạ tầng cho một tình huống chưa tồn tại.
/// </para>
/// <para>
/// Vì sao không để mỗi use case tự viết: dùng UTC ở đây là một lỗi <b>im lặng</b> và chỉ hiện
/// ra trong bảy tiếng mỗi ngày. Từ 0 giờ tới 7 giờ sáng giờ Việt Nam, UTC vẫn còn ở ngày hôm
/// trước, nên bảng lịch sẽ hiện lịch của hôm qua và số hóa đơn sẽ mang ngày hôm qua — không
/// ai để ý cho tới đúng ca sáng.
/// </para>
/// </summary>
public static class SalonTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    /// <summary>Ngày làm việc đang diễn ra, theo giờ tiệm.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).Date);

    /// <summary>0 giờ của ngày làm việc đang diễn ra, giữ nguyên phần bù múi giờ của tiệm.</summary>
    public static DateTimeOffset StartOfToday(DateTimeOffset now) => new(now.ToOffset(Offset).Date, Offset);
}
