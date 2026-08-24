namespace NailManagement.Domain.Common;

/// <summary>
/// Các phép kiểm tra dùng chung cho hàm khởi tạo của entity.
/// <para>
/// Gom về một chỗ vì cùng một ràng buộc ở BR-VAL-001 xuất hiện trên nhiều entity: tên
/// không rỗng, tiền không âm, số phút nằm trong khoảng. Viết lại ở từng entity thì sớm
/// muộn cũng có chỗ ghi thông báo khác chỗ kia.
/// </para>
/// <para>
/// Mọi phép kiểm tra ném <see cref="DomainException"/> kèm tên ô nhập, nên tầng API chỉ
/// việc chuyển thẳng sang <c>fields</c> trong contract lỗi mà không phải đoán.
/// </para>
/// </summary>
public static class Guard
{
    /// <summary>Chuỗi bắt buộc: cắt khoảng trắng hai đầu, không được rỗng, không vượt độ dài.</summary>
    public static string NotEmpty(string? value, string field, string label, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            throw DomainException.ForField(field, $"{label} không được để trống.");

        if (trimmed.Length > maxLength)
            throw DomainException.ForField(field, $"{label} không được dài quá {maxLength} ký tự.");

        return trimmed;
    }

    /// <summary>Chuỗi bắt buộc có cả độ dài tối thiểu, ví dụ tên chi nhánh 3–80 ký tự.</summary>
    public static string Length(string? value, string field, string label, int minLength, int maxLength)
    {
        var trimmed = NotEmpty(value, field, label, maxLength);

        if (trimmed.Length < minLength)
            throw DomainException.ForField(field, $"{label} phải có ít nhất {minLength} ký tự.");

        return trimmed;
    }

    /// <summary>Chuỗi tùy chọn: rỗng thì thành null để database không lưu chuỗi trắng.</summary>
    public static string? Optional(string? value, string field, string label, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0) return null;

        if (trimmed.Length > maxLength)
            throw DomainException.ForField(field, $"{label} không được dài quá {maxLength} ký tự.");

        return trimmed;
    }

    /// <summary>
    /// Số tiền. BR-VAL-003 — tiền tệ là VND số nguyên nên kiểu dữ liệu là <c>long</c>,
    /// không phải <c>decimal</c>: không có phần thập phân nào để mà làm tròn.
    /// </summary>
    public static long Money(long value, string field, string label)
    {
        if (value < 0)
            throw DomainException.ForField(field, $"{label} không được là số âm.");

        return value;
    }

    public static int Between(int value, int min, int max, string field, string label)
    {
        if (value < min || value > max)
            throw DomainException.ForField(field, $"{label} phải nằm trong khoảng {min}–{max}.");

        return value;
    }

    /// <summary>Tỷ lệ hoa hồng nằm trong khoảng 0–1 (0,15 nghĩa là 15%).</summary>
    public static decimal Rate(decimal value, string field, string label)
    {
        if (value < 0m || value > 1m)
            throw DomainException.ForField(field, $"{label} phải nằm trong khoảng 0 đến 1.");

        return value;
    }

    /// <summary>Khóa ngoại bắt buộc. Sai ở đây là lỗi lập trình, nhưng bắt sớm vẫn hơn.</summary>
    public static string Reference(string? value, string field, string label)
        => NotEmpty(value, field, label, 64);
}
