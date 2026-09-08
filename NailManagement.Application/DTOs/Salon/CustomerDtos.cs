namespace NailManagement.Application.DTOs.Salon;

/// <summary>
/// Một khách hàng của tiệm đang làm việc — BR-CUS-001, dùng chung cho mọi chi nhánh.
/// <para>
/// Không có <c>branch</c>: khách thuộc tiệm chứ không thuộc chi nhánh, nên lễ tân chi nhánh
/// nào cũng tra được toàn bộ khách của tiệm (BR-ISO-004). Một trường chi nhánh ở đây sẽ dựng
/// ra một cách chia dữ liệu mà nghiệp vụ không có.
/// </para>
/// <para>
/// Cũng không có <c>points</c> (BR-CUS-008 — không có điểm thưởng), nguồn khách, kỹ thuật
/// viên yêu thích, sở thích, dị ứng, kênh liên lạc hay nhãn phân loại. Những ô đó có trong
/// màn hình cũ dựng bằng dữ liệu mẫu nhưng không có cột nào trong lược đồ; thứ cần ghi nhớ
/// về một khách nay nằm ở <see cref="Note"/>.
/// </para>
/// </summary>
/// <param name="BirthDate">Dạng <c>yyyy-MM-dd</c>, hoặc rỗng — BR-CUS-003 chỉ bắt buộc số điện thoại.</param>
/// <param name="Tier">
/// BR-CUS-007 — <b>suy ra lúc đọc</b> từ <paramref name="TotalSpent"/>, không có cột nào
/// trong database và không có nghiệp vụ nâng hạ hạng. Vì vậy nó không xuất hiện trong lệnh
/// tạo hay lệnh sửa: gửi hạng lên là gửi một thứ máy chủ sẽ bỏ qua.
/// </param>
/// <param name="Visits">
/// Số hóa đơn đã trả đủ của khách — quyết định 42. Một lần ghé có trả tiền là một lượt.
/// </param>
/// <param name="LastVisitAt">Ngày lập hóa đơn gần nhất, rỗng nếu khách chưa từng trả tiền lần nào.</param>
public sealed record CustomerDto(
    string Id,
    string TenantId,
    string Phone,
    string? FullName,
    string? Email,
    string? BirthDate,
    string? Note,
    string Status,
    string Tier,
    int Visits,
    long TotalSpent,
    DateTimeOffset? LastVisitAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Một lần khách ghé tiệm và trả tiền, đọc từ hóa đơn đã thanh toán.
/// <para>
/// Cố ý không dựng từ lịch hẹn: lịch hẹn có thể bị hủy hoặc khách không đến (BR-APT-012),
/// còn một hóa đơn đã trả đủ thì chắc chắn là một lần khách được phục vụ thật.
/// </para>
/// </summary>
public sealed record CustomerVisitDto(
    string InvoiceId,
    string InvoiceCode,
    DateTimeOffset IssuedAt,
    string BranchName,
    string? StaffName,
    long Total,
    IReadOnlyList<string> ServiceNames);

/// <summary>
/// Hồ sơ khách kèm những lần ghé gần nhất — thứ ngăn chi tiết ở màn khách hàng cần.
/// <para>
/// Tách khỏi <see cref="CustomerDto"/> thay vì nhét thêm một mảng vào đó: danh sách khách
/// hiện hai mươi dòng một lúc, và kéo theo lịch sử của cả hai mươi người là hai mươi phép
/// nối cho một màn hình chỉ mở chi tiết đúng một hồ sơ.
/// </para>
/// </summary>
public sealed record CustomerDetailDto(CustomerDto Customer, IReadOnlyList<CustomerVisitDto> Visits);

/// <summary>
/// Thêm khách hàng.
/// <para>
/// Không có ô <c>tenantId</c>: tiệm đến từ phiên đăng nhập (BR-AUTH-024). Không có ô
/// <c>status</c> — khách mới luôn bắt đầu ở <c>ACTIVE</c>. Không có ô <c>tier</c> — hạng
/// khách suy ra từ chi tiêu (BR-CUS-007).
/// </para>
/// <para>
/// BR-CUS-003 — chỉ <paramref name="Phone"/> là bắt buộc. Lúc quầy đông, bắt lễ tân nhập đủ
/// thông tin là cách nhanh nhất để họ bỏ qua phần mềm và ghi ra giấy.
/// </para>
/// </summary>
public sealed record CreateCustomerCommand(
    string Phone,
    string? FullName,
    string? Email,
    string? BirthDate,
    string? Note);

/// <summary>
/// Sửa hồ sơ khách, bao gồm cả đổi số điện thoại.
/// <para>
/// Là phép <b>thay trọn hồ sơ</b> giống <c>PUT /api/staff/{id}</c>: bỏ trống <c>email</c>
/// trong thân request là xóa email trên hồ sơ. Biểu mẫu sửa vì vậy phải gửi đủ mọi trường,
/// kể cả những trường người dùng không đụng tới.
/// </para>
/// </summary>
public sealed record UpdateCustomerCommand(
    string CustomerId,
    string Phone,
    string? FullName,
    string? Email,
    string? BirthDate,
    string? Note);

/// <param name="Status">
/// <c>ACTIVE</c> hoặc <c>INACTIVE</c> — BR-CUS-005 chỉ có hai, đã bỏ <c>CARE</c> của bản
/// frontend cũ. BR-CUS-006: xóa khách hàng chính là chuyển sang <c>INACTIVE</c>, và lịch sử
/// dịch vụ cùng hóa đơn của họ giữ nguyên.
/// </param>
public sealed record ChangeCustomerStatusCommand(string CustomerId, string Status);
