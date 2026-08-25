namespace NailManagement.Application.DTOs;

/// <summary>
/// Một gói dịch vụ trên bảng giá.
/// <para>
/// Chỉ phục vụ việc đọc. Module quản lý gói đã bị cắt khỏi MVP, nhưng bảng giá thì vẫn phải
/// đọc được: BR-TENANT-004 bắt chọn gói ngay lúc tạo tiệm, và giá cùng số phiên bản được
/// chốt tại đúng thời điểm đó theo BR-SUB-004.
/// </para>
/// <para>
/// <paramref name="MaxSalons"/> và <paramref name="MaxStaff"/> là hai hạn mức được cưỡng
/// chế thật (BR-SUB-005). Những hạn mức còn lại nằm trong <paramref name="Limits"/> và chỉ
/// để trưng bày — BR-SUB-006 nói rõ MVP không có gì để đo chúng.
/// </para>
/// <para>
/// <paramref name="Features"/> và <paramref name="Limits"/> đi ra ngoài ở dạng JSON thô
/// đúng như cột trong database, vì frontend đã có sẵn cách đọc hai khối này từ dữ liệu mẫu.
/// Dựng lại chúng thành kiểu C# rồi tuần tự hóa lần nữa chỉ thêm một chỗ để lệch nhau.
/// </para>
/// </summary>
public sealed record PackageDto(
    string Id,
    string Name,
    string? Description,
    long Price,
    string BillingCycle,
    int MaxSalons,
    int MaxStaff,
    int Version,
    string Status,
    string? Color,
    IReadOnlyList<string> Capabilities,
    string Features,
    string Limits);
