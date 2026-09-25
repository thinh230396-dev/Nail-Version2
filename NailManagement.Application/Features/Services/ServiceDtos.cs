namespace NailManagement.Application.Features.Services;

/// <summary>
/// Một dịch vụ của tiệm đang làm việc — BR-SVC-001, dùng chung cho mọi chi nhánh.
/// <para>
/// Không có <c>branches</c>: dịch vụ thuộc tiệm chứ không thuộc chi nhánh, nên một danh
/// sách chi nhánh ở đây sẽ là một sự thật không có thật.
/// </para>
/// <para>
/// Cũng không có <c>memberPrice</c> (BR-SVC-002), <c>taxRate</c> (BR-SVC-009), giá vốn hay
/// tiền cọc theo dịch vụ. Những ô đó có trong màn hình cũ dựng bằng dữ liệu mẫu, nhưng
/// không có trong lược đồ và không có rule nào định nghĩa chúng.
/// </para>
/// <para>
/// Dịch vụ đã ngừng bán vẫn nằm trong danh sách — BR-DEL-003 yêu cầu tên của nó tiếp tục
/// hiện đúng trong hóa đơn cũ, và màn quản lý cần thấy nó thì mới bật lại được.
/// </para>
/// </summary>
public sealed record ServiceDto(
    string Id,
    string TenantId,
    string Name,
    string? Category,
    long Price,
    int DurationMinutes,
    int BufferMinutes,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Thêm dịch vụ.
/// <para>
/// Không có ô <c>tenantId</c>: tiệm đến từ phiên đăng nhập (BR-AUTH-024). Cũng không có ô
/// <c>status</c> — dịch vụ mới luôn bắt đầu ở <c>ACTIVE</c>; tạo thẳng một dịch vụ đã ngừng
/// bán là một thao tác không ai cần.
/// </para>
/// </summary>
/// <param name="Category">
/// Nhóm dịch vụ là <b>chuỗi tự do</b>, không phải danh mục cố định: tiệm tự đặt tên nhóm
/// theo cách họ bán hàng ("Sơn gel", "Đắp bột", "Combo"). BR-SVC-008 — một combo chỉ là một
/// dịch vụ có giá riêng chứ không phải một cấu trúc riêng, nên nó cũng chỉ là một nhóm.
/// </param>
public sealed record CreateServiceCommand(
    string Name,
    string? Category,
    long Price,
    int DurationMinutes,
    int BufferMinutes,
    string? Description);

/// <summary>
/// Sửa dịch vụ, bao gồm cả đổi giá.
/// <para>
/// BR-SVC-006 — đổi giá <b>không</b> ảnh hưởng hóa đơn đã lập: dòng hóa đơn giữ
/// <c>unit_price</c> của chính nó tại thời điểm lập và không tham chiếu ngược về bảng dịch
/// vụ. Vì vậy ở đây không có bảng lịch sử giá và cũng không cần lý do đổi giá.
/// </para>
/// </summary>
public sealed record UpdateServiceCommand(
    string ServiceId,
    string Name,
    string? Category,
    long Price,
    int DurationMinutes,
    int BufferMinutes,
    string? Description);

/// <param name="Status">
/// <c>ACTIVE</c> hoặc <c>INACTIVE</c>. BR-SVC-005 — "ngừng dịch vụ" chính là chuyển sang
/// <c>INACTIVE</c>; hệ thống không có lệnh xóa cứng nào (BR-DEL-001).
/// </param>
public sealed record ChangeServiceStatusCommand(string ServiceId, string Status);
