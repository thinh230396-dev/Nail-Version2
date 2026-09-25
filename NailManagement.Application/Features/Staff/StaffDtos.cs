using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Application.Features.Staff;

/// <summary>
/// Tài khoản đăng nhập gắn với một hồ sơ nhân viên — BR-AUTH-013.
/// <para>
/// Chỉ lễ tân mới có (BR-AUTH-002), và mỗi hồ sơ nhiều nhất một tài khoản. Rỗng nghĩa là
/// hồ sơ đó chưa được cấp quyền đăng nhập, và đó chính là thứ màn quản lý nhân viên dùng
/// để quyết định nút "Cấp tài khoản đăng nhập" hiện ở dòng nào.
/// </para>
/// </summary>
public sealed record StaffAccountDto(string Id, string Email, string? Username, string Status);

/// <summary>
/// Một hồ sơ nhân viên của tiệm đang làm việc.
/// <para>
/// Trả <paramref name="BranchId"/> chứ không trả tên chi nhánh. Màn hình nào cần tên thì đã
/// có sẵn danh sách từ <c>GET /api/branches</c> để ghép — gửi kèm tên ở đây là chép cùng
/// một sự thật ra hai chỗ, rồi có ngày hai chỗ lệch nhau sau một lần đổi tên chi nhánh.
/// </para>
/// <para>
/// Không có chấm công, nghỉ phép, doanh số hay xếp hạng. BR-EMP-009 chỉ giữ <b>một ca cố
/// định</b>; toàn bộ phần còn lại là một module nhân sự đã bị loại khỏi MVP.
/// </para>
/// </summary>
/// <param name="ShiftStart">Giờ bắt đầu ca ở dạng <c>HH:mm</c>.</param>
/// <param name="CommissionRate">
/// BR-EMP-011 — tỷ lệ hoa hồng dạng 0–1 (0,15 là 15%). Số tiền hoa hồng được nhân ra lúc
/// hiển thị báo cáo, không lưu sẵn ở đâu cả.
/// </param>
/// <param name="Skills">
/// BR-EMP-010 — kỹ năng chỉ để hiển thị. Hệ thống không cưỡng chế khi phân công: bất kỳ kỹ
/// thuật viên nào cũng gán được cho bất kỳ dịch vụ nào.
/// </param>
public sealed record StaffDto(
    string Id,
    string TenantId,
    string BranchId,
    string FullName,
    string? Phone,
    string? Email,
    string Role,
    string Status,
    string ShiftStart,
    string ShiftEnd,
    decimal CommissionRate,
    IReadOnlyList<string> Skills,
    StaffAccountDto? Account,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Thêm hồ sơ nhân viên — BR-EMP-008 đối chiếu số nhân viên chưa nghỉ việc với hạn mức gói.
/// <para>
/// Hồ sơ mới luôn bắt đầu ở <c>WORKING</c> nên không có ô <c>status</c>, và <b>không</b> có
/// ô nào về tài khoản đăng nhập: BR-AUTH-013 quy định quy trình đúng là tạo hồ sơ trước rồi
/// mới bấm "Cấp tài khoản đăng nhập" trên chính hồ sơ đó.
/// </para>
/// </summary>
/// <param name="Role"><c>TECHNICIAN</c> hoặc <c>RECEPTIONIST</c> — BR-EMP-002.</param>
public sealed record CreateStaffCommand(
    string BranchId,
    string FullName,
    string? Phone,
    string? Email,
    string Role,
    string ShiftStart,
    string ShiftEnd,
    decimal CommissionRate,
    IReadOnlyList<string>? Skills);

/// <summary>
/// Sửa hồ sơ nhân viên, bao gồm cả chuyển chi nhánh (BR-EMP-003).
/// <para>
/// Đổi vai trò nghiệp vụ được, nhưng <b>không</b> khi hồ sơ đã có tài khoản đăng nhập: một
/// tài khoản trỏ tới hồ sơ kỹ thuật viên là thứ BR-AUTH-002 nói không tồn tại.
/// </para>
/// </summary>
public sealed record UpdateStaffCommand(
    string StaffId,
    string BranchId,
    string FullName,
    string? Phone,
    string? Email,
    string Role,
    string ShiftStart,
    string ShiftEnd,
    decimal CommissionRate,
    IReadOnlyList<string>? Skills);

/// <param name="Status">
/// <c>WORKING</c>, <c>OFF_SHIFT</c>, <c>LEAVE</c> hoặc <c>INACTIVE</c> — BR-EMP-005.
/// "Nghỉ việc" chính là <c>INACTIVE</c>, và nó kéo theo việc vô hiệu hóa tài khoản đăng
/// nhập nếu hồ sơ có một cái.
/// </param>
public sealed record ChangeStaffStatusCommand(string StaffId, string Status);

/// <summary>
/// Cấp tài khoản đăng nhập cho một hồ sơ lễ tân đã tồn tại — BR-AUTH-013.
/// <para>
/// Không có ô <c>role</c>: vai trò đăng nhập luôn là <c>RECEPTIONIST</c> và suy ra từ chính
/// hồ sơ. Cũng không có ô <c>tenantId</c> — tiệm đến từ phiên của chủ tiệm đang thao tác.
/// </para>
/// </summary>
/// <param name="Email">
/// Bỏ trống thì lấy email trên hồ sơ nhân viên. Không có cả hai thì từ chối: BR-VAL-001 đòi
/// email duy nhất toàn hệ thống và đó cũng là thứ người ta gõ vào ô đăng nhập.
/// </param>
/// <param name="Password">
/// Bỏ trống thì máy chủ sinh và trả về <b>đúng một lần</b> — hệ thống không gửi email và
/// không có đường đọc lại.
/// </param>
public sealed record GrantStaffAccountCommand(
    string StaffId,
    string? Email,
    string? Username,
    string? DisplayName,
    string? Password);

public sealed record GrantStaffAccountResult(StaffDto Staff, string? GeneratedPassword);
