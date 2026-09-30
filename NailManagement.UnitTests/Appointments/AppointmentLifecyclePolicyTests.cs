using NailManagement.Domain.Salon.Appointments;

namespace NailManagement.UnitTests.Appointments;

/// <summary>Bảng chuyển trạng thái của lịch hẹn — BR-APT-040, BR-APT-041.</summary>
public sealed class AppointmentLifecyclePolicyTests
{
    [Theory]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.InService)]
    [InlineData(AppointmentStatus.InService, AppointmentStatus.Completed)]
    public void Chuyen_duoc_theo_dung_thu_tu(AppointmentStatus from, AppointmentStatus to)
        => Assert.True(AppointmentLifecyclePolicy.CanTransition(from, to));

    [Theory]
    // BR-APT-040 — đang phục vụ dở thì không hủy, không đánh dấu vắng mặt.
    [InlineData(AppointmentStatus.InService, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.InService, AppointmentStatus.NoShow)]
    // Không nhảy cóc.
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Completed)]
    // Không đi lùi.
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Pending)]
    public void Khong_chuyen_ngoai_bang(AppointmentStatus from, AppointmentStatus to)
        => Assert.False(AppointmentLifecyclePolicy.CanTransition(from, to));

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Ba_trang_thai_cuoi_khong_di_dau_duoc_nua(AppointmentStatus final)
    {
        Assert.True(AppointmentLifecyclePolicy.IsFinal(final));
        Assert.All(Enum.GetValues<AppointmentStatus>(),
            target => Assert.False(AppointmentLifecyclePolicy.CanTransition(final, target)));
    }

    [Fact]
    public void Chi_ba_trang_thai_cuoi_la_ngo_cut()
    {
        // Thêm một trạng thái mới mà quên khai bảng chuyển thì nó thành ngõ cụt im lặng: lịch
        // hẹn vào đó rồi không ra được, và không ai được báo lỗi.
        foreach (var status in Enum.GetValues<AppointmentStatus>())
        {
            var isKnownFinal = status is AppointmentStatus.Completed
                or AppointmentStatus.Cancelled or AppointmentStatus.NoShow;

            Assert.Equal(isKnownFinal, AppointmentLifecyclePolicy.IsFinal(status));
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending, true)]
    [InlineData(AppointmentStatus.Confirmed, true)]
    [InlineData(AppointmentStatus.CheckedIn, false)]
    [InlineData(AppointmentStatus.InService, false)]
    [InlineData(AppointmentStatus.Completed, false)]
    public void Chi_doi_lich_khi_khach_chua_toi(AppointmentStatus status, bool expected)
        => Assert.Equal(expected, AppointmentLifecyclePolicy.CanReschedule(status));

    [Theory]
    [InlineData(AppointmentStatus.CheckedIn, true)]
    [InlineData(AppointmentStatus.InService, true)]
    [InlineData(AppointmentStatus.Confirmed, false)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.NoShow, false)]
    public void Thu_du_tien_chi_tu_hoan_tat_lich_cua_khach_dang_o_tiem(AppointmentStatus status, bool expected)
        => Assert.Equal(expected, AppointmentLifecyclePolicy.CanCompleteFromPayment(status));
}
