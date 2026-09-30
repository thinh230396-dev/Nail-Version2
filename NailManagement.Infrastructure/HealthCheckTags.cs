namespace NailManagement.Infrastructure;

/// <summary>Nhãn phân loại kiểm tra sức khỏe, dùng chung giữa nơi đăng ký và nơi mở endpoint.</summary>
public static class HealthCheckTags
{
    /// <summary>Phụ thuộc bên ngoài phải sẵn sàng thì máy chủ mới nhận request.</summary>
    public const string Ready = "ready";
}
