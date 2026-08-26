using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;
using NailManagement.Domain.Entities.Auditing;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Entities.Platform;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Enums.Platform;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.ValueObjects;

namespace NailManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp dữ liệu mẫu cho toàn bộ nghiệp vụ: bảng giá, sáu tiệm, chi nhánh, nhân viên, dịch
/// vụ, khách hàng, và ba mươi ngày lịch hẹn kèm hóa đơn đã thanh toán.
/// <para>
/// Có dữ liệu ba mươi ngày là điều kiện để báo cáo doanh thu (BR-REV-004) hiện số thật khi
/// demo. Một database trống thì mọi biểu đồ đều bằng 0, và người xem không phân biệt được
/// giữa "chưa có dữ liệu" với "tính sai".
/// </para>
/// <para>
/// Bộ sinh số ngẫu nhiên dùng hạt giống CỐ ĐỊNH, nên hai máy nạp lần đầu sẽ ra cùng một
/// bộ dữ liệu. Nhờ vậy con số trên slide bảo vệ khớp với con số trên máy chấm.
/// </para>
/// </summary>
public sealed class DemoDataSeeder(NailDbContext db, IPasswordHasher hasher, IClock clock)
{
    /// <summary>Giờ Việt Nam. Lịch hẹn phải nằm đúng khung giờ mở cửa khi nhìn trên giao diện.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const string SharedAdminPassword = "Tenant@2026";

    private readonly Random _random = new(20260824);

    /// <summary>Các khoảng giờ đã bị chiếm, theo từng kỹ thuật viên — dùng để dữ liệu mẫu không tự vi phạm BR-APT-011.</summary>
    private readonly Dictionary<string, List<(DateTimeOffset Start, DateTimeOffset End)>> _busySlots = [];

    private int _auditSequence;
    private int _subscriptionInvoiceSequence;

    /// <summary>
    /// Chỉ nạp khi bảng gói còn trống — cùng nguyên tắc với bộ nạp tài khoản: chạy lại máy
    /// chủ không được ghi đè dữ liệu mà người dùng đã tạo trong lúc demo.
    /// </summary>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Packages.AnyAsync(cancellationToken)) return false;

        var now = clock.UtcNow.ToOffset(VietnamOffset);

        var packages = SeedPackages(now);
        var tenants = SeedTenants(packages, now);

        await SeedTenantAdminAccountsAsync(now, cancellationToken);
        SeedUserTenantLinks(now);

        SeedLumiere(now);
        SeedMuse(now);

        SeedSubscriptionInvoices(tenants, packages, now);
        SeedUpgradeRequest(tenants, packages, now);
        SeedAuditTrail(tenants, now);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Nền tảng ──────────────────────────────────────────────────────────────

    private Dictionary<string, Package> SeedPackages(DateTimeOffset now)
    {
        var packages = new Dictionary<string, Package>();

        foreach (var seed in DemoSeedCatalog.Packages)
        {
            var package = Package.Create(
                seed.Id,
                seed.Name,
                seed.Description,
                seed.Price,
                seed.BillingCycle,
                seed.MaxSalons,
                seed.MaxStaff,
                DemoSeedCatalog.CapabilitiesJson(seed.EnabledCapabilities),
                DemoSeedCatalog.FeaturesJson(seed.Features),
                seed.LimitsJson,
                seed.Color,
                now);

            db.Add(package);
            packages[seed.Id] = package;
        }

        return packages;
    }

    private Dictionary<string, Tenant> SeedTenants(Dictionary<string, Package> packages, DateTimeOffset now)
    {
        var tenants = new Dictionary<string, Tenant>();

        Tenant Add(
            string id, string name, string packageId, bool isTrial, int expiresInDays,
            int createdDaysAgo, string address, string phone, string email)
        {
            var package = packages[packageId];

            var tenant = Tenant.Create(
                id,
                id.Replace("TEN-", string.Empty),
                name,
                package.Id,
                package.Price,
                package.Version,
                package.BillingCycle,
                isTrial,
                now.AddDays(expiresInDays),
                address,
                phone,
                email,
                now);

            db.Add(tenant);

            // Lùi ngày tạo về quá khứ. Tiệm "mới tạo hôm nay" mà lại có ba mươi ngày lịch
            // hẹn phía sau là dữ liệu tự mâu thuẫn, và đó là thứ người chấm nhìn ra ngay.
            Backdate(tenant, nameof(Tenant.CreatedAt), now.AddDays(-createdDaysAgo));
            Backdate(tenant, nameof(Tenant.UpdatedAt), now.AddDays(-createdDaysAgo));
            Backdate(tenant, nameof(Tenant.SubscriptionStartedAt), now.AddDays(-createdDaysAgo));

            tenants[id] = tenant;
            return tenant;
        }

        Add(DemoIds.LumiereTenant, "Nailé Studio", "PKG-PREMIUM", false, 90, 210,
            "95 Võ Văn Tần, Quận 3, TP. Hồ Chí Minh", "0283930001", "tenantadmin@lumierehair.vn");

        Add(DemoIds.MuseTenant, "Muse Nail Lab", "PKG-BASIC", false, 45, 120,
            "43 Lê Văn Sỹ, Quận 3, TP. Hồ Chí Minh", "0283930002", "linh.do@musenail.vn");

        // Đang dùng thử: BR-TENANT-002 sẽ hiển thị TRIAL vì còn hạn và cờ dùng thử đang bật.
        Add(DemoIds.BloomTenant, "Bloom Salon", "PKG-PREMIUM", true, 12, 18,
            "12 Nguyễn Văn Trỗi, Phú Nhuận, TP. Hồ Chí Minh", "0283930003", "ha.vu@bloomsalon.vn");

        // Quá hạn: entity CỐ Ý từ chối tạo tiệm với hạn dùng đã trôi qua (BR-VAL-001), nên
        // phải tạo với hạn tương lai rồi kéo ngược cột lại. Đây là thao tác chỉ dành cho
        // dữ liệu mẫu — nó mô phỏng một tiệm đã hết hạn từ năm ngày trước, thứ mà trong
        // đời thực xảy ra do thời gian trôi chứ không do ai bấm nút.
        var oasis = Add(DemoIds.OasisTenant, "Oasis Wellness", "PKG-PREMIUM", false, 30, 150,
            "181 Hai Bà Trưng, Quận 1, TP. Hồ Chí Minh", "0283930004", "ngoc.trinh@oasiswellness.vn");
        Backdate(oasis, nameof(Tenant.ExpiresAt), now.AddDays(-5));

        // Bị khóa tay bởi Superadmin — BR-TENANT-002 hiển thị SUSPENDED bất kể còn hạn.
        var morning = Add(DemoIds.MorningTenant, "Morning Dew Spa", "PKG-BASIC", false, 30, 95,
            "57 Phan Xích Long, Phú Nhuận, TP. Hồ Chí Minh", "0283930005", "uyen.hoang@morningdew.vn");
        morning.Suspend(now.AddDays(-9));

        Add(DemoIds.AuroraTenant, "Aurora Beauty & Spa", "PKG-ENTERPRISE", false, 300, 380,
            "28 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh", "0283930006", "tu.pham@aurorabeauty.vn");

        return tenants;
    }

    /// <summary>
    /// Bốn tiệm phụ mỗi tiệm một tài khoản chủ tiệm riêng. Hai tiệm chính cố ý KHÔNG có
    /// thêm tài khoản nào: chúng dùng chung tài khoản đã có từ ngày 1, và đó chính là ca
    /// demo cho BR-AUTH-023 — một người quản lý nhiều tiệm.
    /// </summary>
    private async Task SeedTenantAdminAccountsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        (string TenantId, string UserId, string Email, string Username, string DisplayName)[] owners =
        [
            (DemoIds.BloomTenant, "USR-TENANT-BLOOM", "ha.vu@bloomsalon.vn", "havu", "Vũ Thu Hà"),
            (DemoIds.OasisTenant, "USR-TENANT-OASIS", "ngoc.trinh@oasiswellness.vn", "ngoctrinh", "Trịnh Bảo Ngọc"),
            (DemoIds.MorningTenant, "USR-TENANT-MORNING", "uyen.hoang@morningdew.vn", "uyenhoang", "Hoàng Ngọc Uyên"),
            (DemoIds.AuroraTenant, "USR-TENANT-AURORA", "tu.pham@aurorabeauty.vn", "tupham", "Phạm Thanh Tú")
        ];

        foreach (var owner in owners)
        {
            if (await db.AppUsers.AnyAsync(user => user.Id == owner.UserId, cancellationToken)) continue;

            var hashed = hasher.Hash(RawPassword.Create(SharedAdminPassword));

            var account = AppUser.Create(
                owner.UserId,
                Email.Create(owner.Email),
                owner.Username,
                hashed.Hash,
                hashed.Salt,
                UserRole.TenantAdmin,
                owner.DisplayName,
                now);

            db.Add(account);
            db.Add(UserTenant.Link(owner.UserId, owner.TenantId, now));
        }
    }

    /// <summary>
    /// BR-AUTH-023 — tài khoản chủ tiệm có sẵn được nối với CẢ HAI tiệm chính. Đây là dữ
    /// liệu để kiểm chứng BR-ISO-006: đăng nhập một lần, chọn tiệm, và không nhìn thấy gì
    /// của tiệm còn lại.
    /// </summary>
    private void SeedUserTenantLinks(DateTimeOffset now)
    {
        db.Add(UserTenant.Link(DemoIds.LumiereAdminUser, DemoIds.LumiereTenant, now));
        db.Add(UserTenant.Link(DemoIds.LumiereAdminUser, DemoIds.MuseTenant, now));

        // Lễ tân chỉ thuộc một tiệm, nhưng vẫn cần một dòng ở đây: BR-ISO-003 bắt kiểm tra
        // liên kết trước khi đặt tiệm đang làm việc cho phiên, không phân biệt vai trò.
        db.Add(UserTenant.Link(DemoIds.NaileReceptionUser, DemoIds.LumiereTenant, now));
    }

    // ── Tiệm chính: đầy đủ dữ liệu ────────────────────────────────────────────

    private void SeedLumiere(DateTimeOffset now)
    {
        var tenantId = DemoIds.LumiereTenant;

        // BR-BRANCH-001 — chi nhánh chính tạo cùng tiệm và không xóa được.
        db.Add(Branch.Create(DemoIds.LumiereBranchQ3, tenantId, "Chi nhánh Quận 3", "Q3",
            "95 Võ Văn Tần, Quận 3, TP. Hồ Chí Minh", "0283930001", true, now.AddDays(-210)));

        db.Add(Branch.Create(DemoIds.LumiereBranchQ1, tenantId, "Chi nhánh Quận 1", "Q1",
            "28 Lê Lợi, Quận 1, TP. Hồ Chí Minh", "0283930011", false, now.AddDays(-120)));

        var staff = SeedStaff(tenantId, DemoSeedCatalog.LumiereStaff, now.AddDays(-200));
        var services = SeedServices(tenantId, DemoSeedCatalog.LumiereServices, now.AddDays(-205));
        var customers = SeedCustomers(tenantId, DemoSeedCatalog.LumiereCustomers, now.AddDays(-180));

        // BR-AUTH-013 — tài khoản lễ tân có sẵn được gắn vào hồ sơ nhân viên tương ứng.
        // Từ đây trở đi, chi nhánh của tài khoản đó đọc qua hồ sơ chứ không phải cột riêng
        // (BR-EMP-004), và ngày 3 sẽ gỡ nốt bốn cột tạm trên bảng tài khoản.
        var receptionAccount = db.AppUsers.Local.FirstOrDefault(user => user.Id == DemoIds.NaileReceptionUser)
            ?? db.AppUsers.FirstOrDefault(user => user.Id == DemoIds.NaileReceptionUser);

        receptionAccount?.AttachStaff("STF-LUM-05", now);

        var technicians = staff.Where(member => member.Role == StaffRole.Technician).ToList();

        // Ba mươi ngày gần nhất, tính cả hôm nay.
        for (var dayOffset = 29; dayOffset >= 0; dayOffset--)
        {
            var day = now.AddDays(-dayOffset);
            var appointmentsToday = _random.Next(4, 8);

            for (var index = 0; index < appointmentsToday; index++)
            {
                var technician = technicians[_random.Next(technicians.Count)];
                var chosenServices = PickServices(services);
                var start = FindFreeSlot(technician, day, chosenServices);

                if (start is null) continue;

                var customer = customers[_random.Next(customers.Count)];
                var appointment = CreateAppointment(tenantId, technician, customer, chosenServices, start.Value, now);

                PlayOutAppointment(appointment, chosenServices, technician, dayOffset, now);
            }
        }
    }

    // ── Tiệm thứ hai: đủ để kiểm chứng cách ly ────────────────────────────────

    private void SeedMuse(DateTimeOffset now)
    {
        var tenantId = DemoIds.MuseTenant;

        db.Add(Branch.Create(DemoIds.MuseBranchMain, tenantId, "Chi nhánh Lê Văn Sỹ", "LVS",
            "43 Lê Văn Sỹ, Quận 3, TP. Hồ Chí Minh", "0283930002", true, now.AddDays(-120)));

        var staff = SeedStaff(tenantId, DemoSeedCatalog.MuseStaff, now.AddDays(-115));
        var services = SeedServices(tenantId, DemoSeedCatalog.MuseServices, now.AddDays(-118));
        var customers = SeedCustomers(tenantId, DemoSeedCatalog.MuseCustomers, now.AddDays(-100));

        var technician = staff.First(member => member.Role == StaffRole.Technician);

        for (var dayOffset = 6; dayOffset >= 0; dayOffset--)
        {
            var day = now.AddDays(-dayOffset);
            var chosenServices = PickServices(services);
            var start = FindFreeSlot(technician, day, chosenServices);

            if (start is null) continue;

            var customer = customers[_random.Next(customers.Count)];
            var appointment = CreateAppointment(tenantId, technician, customer, chosenServices, start.Value, now);

            PlayOutAppointment(appointment, chosenServices, technician, dayOffset, now);
        }
    }

    // ── Khối dựng dùng chung ──────────────────────────────────────────────────

    private List<Staff> SeedStaff(string tenantId, DemoSeedCatalog.StaffSeed[] seeds, DateTimeOffset createdAt)
    {
        var result = new List<Staff>();

        foreach (var seed in seeds)
        {
            var member = Staff.Create(
                seed.Id,
                tenantId,
                seed.BranchId,
                seed.FullName,
                seed.Phone,
                null,
                seed.Role,
                new TimeOnly(seed.ShiftStartHour, 0),
                new TimeOnly(seed.ShiftEndHour, 0),
                seed.CommissionRate,
                DemoSeedCatalog.SkillsJson(seed.Skills),
                createdAt);

            db.Add(member);
            result.Add(member);
        }

        return result;
    }

    private List<Service> SeedServices(string tenantId, DemoSeedCatalog.ServiceSeed[] seeds, DateTimeOffset createdAt)
    {
        var result = new List<Service>();

        foreach (var seed in seeds)
        {
            var service = Service.Create(
                seed.Id,
                tenantId,
                seed.Name,
                seed.Category,
                seed.Price,
                seed.DurationMinutes,
                seed.BufferMinutes,
                null,
                createdAt);

            db.Add(service);
            result.Add(service);
        }

        return result;
    }

    private List<Customer> SeedCustomers(
        string tenantId, (string Phone, string Name)[] seeds, DateTimeOffset createdAt)
    {
        var result = new List<Customer>();
        var index = 0;

        foreach (var seed in seeds)
        {
            var customer = Customer.Create(
                $"CUS-{tenantId.Replace("TEN-", string.Empty)}-{++index:D3}",
                tenantId,
                seed.Phone,
                seed.Name,
                null,
                null,
                null,
                createdAt.AddDays(_random.Next(0, 60)));

            db.Add(customer);
            result.Add(customer);
        }

        return result;
    }

    private List<Service> PickServices(List<Service> services)
    {
        var count = _random.Next(1, 3);

        return [.. services.OrderBy(_ => _random.Next()).Take(count)];
    }

    /// <summary>
    /// Tìm một khung giờ trống cho kỹ thuật viên trong ca của họ.
    /// <para>
    /// Dữ liệu mẫu phải TỰ TUÂN THỦ BR-APT-011. Nếu bộ nạp sinh ra hai lịch chồng giờ của
    /// cùng một người, thì mọi phép kiểm tra chống trùng viết ở ngày 11 sẽ đỏ ngay từ dữ
    /// liệu khởi tạo, và tệ hơn là màn hình lịch lúc demo trông như bị lỗi.
    /// </para>
    /// </summary>
    private DateTimeOffset? FindFreeSlot(Staff technician, DateTimeOffset day, List<Service> services)
    {
        var totalMinutes = services.Sum(service => service.DurationMinutes + service.BufferMinutes);
        var busy = _busySlots.TryGetValue(technician.Id, out var slots) ? slots : _busySlots[technician.Id] = [];

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var startHour = technician.ShiftStart.Hour + _random.Next(0, Math.Max(1, technician.ShiftEnd.Hour - technician.ShiftStart.Hour - 2));
            var startMinute = _random.Next(0, 2) * 30;

            var start = new DateTimeOffset(day.Year, day.Month, day.Day, startHour, startMinute, 0, VietnamOffset);
            var end = start.AddMinutes(totalMinutes);

            if (end.TimeOfDay > technician.ShiftEnd.ToTimeSpan()) continue;
            if (busy.Any(slot => start < slot.End && end > slot.Start)) continue;

            busy.Add((start, end));
            return start;
        }

        return null;
    }

    private Appointment CreateAppointment(
        string tenantId,
        Staff technician,
        Customer customer,
        List<Service> services,
        DateTimeOffset start,
        DateTimeOffset now)
    {
        // BR-APT-030 — một phần lịch hẹn có tiền cọc, để màn thu tiền demo được đường
        // chuyển cọc thành dòng đã thu ở BR-APT-031.
        var deposit = _random.Next(0, 5) == 0 ? 100_000L : 0L;

        var appointment = Appointment.Create(
            $"APT-{tenantId.Replace("TEN-", string.Empty)}-{start:yyyyMMddHHmm}-{technician.Id[^2..]}",
            tenantId,
            technician.BranchId,
            customer.Id,
            technician.Id,
            start,
            [.. services.Select(service =>
                (service.Id, service.Name, service.DurationMinutes, service.BufferMinutes))],
            AppointmentStatus.Pending,
            RandomSource(),
            null,
            null,
            deposit,
            DemoIds.NaileReceptionUser,
            start.AddDays(-1));

        db.Add(appointment);
        return appointment;
    }

    /// <summary>
    /// Đưa lịch hẹn đi hết vòng đời của nó, đúng theo sơ đồ ở mục 16.1.
    /// <para>
    /// Cố ý KHÔNG ghi thẳng trạng thái cuối vào cột: đi qua từng bước chuyển là cách kiểm
    /// chứng luôn rằng sơ đồ trạng thái ở <c>AppointmentLifecyclePolicy</c> chấp nhận
    /// những đường đi mà nghiệp vụ thực tế cần.
    /// </para>
    /// </summary>
    private void PlayOutAppointment(
        Appointment appointment,
        List<Service> services,
        Staff technician,
        int dayOffset,
        DateTimeOffset now)
    {
        var start = appointment.StartAt;

        // Lịch của hôm nay: để rải trên các trạng thái đang diễn ra, cho màn hình lễ tân
        // lúc demo có việc để làm chứ không phải toàn dòng đã hoàn tất.
        if (dayOffset == 0)
        {
            appointment.ChangeStatus(AppointmentStatus.Confirmed, now);

            var stage = _random.Next(0, 4);

            if (stage >= 1) appointment.ChangeStatus(AppointmentStatus.CheckedIn, now);
            if (stage >= 2) appointment.ChangeStatus(AppointmentStatus.InService, now);

            // BR-PAY-003 — một hóa đơn mới thu một phần sẽ nằm ở trạng thái PARTIAL, đúng
            // ca mà BR-APT-027 nói tới: chủ tiệm được đóng lịch khi chưa thu đủ.
            if (stage == 3)
            {
                var invoice = CreateInvoice(appointment, services, technician, now);
                invoice.RegisterPayment(
                    $"{invoice.Id}-P1", PaymentType.Payment, PaymentMethod.Cash,
                    Math.Max(50_000, invoice.Total / 2), now, null, DemoIds.NaileReceptionUser, now);
            }

            return;
        }

        var outcome = _random.Next(0, 100);

        // Tám phần trăm hủy, bảy phần trăm khách không đến — hai con số này chỉ để dữ liệu
        // trông giống một tiệm thật; BR-APT-012 loại cả hai khỏi phép chống trùng lịch.
        if (outcome < 8)
        {
            appointment.ChangeStatus(AppointmentStatus.Cancelled, start);
            return;
        }

        appointment.ChangeStatus(AppointmentStatus.Confirmed, start.AddDays(-1));

        if (outcome < 15)
        {
            appointment.ChangeStatus(AppointmentStatus.NoShow, start.AddMinutes(30));
            return;
        }

        appointment.ChangeStatus(AppointmentStatus.CheckedIn, start);
        appointment.ChangeStatus(AppointmentStatus.InService, start.AddMinutes(5));

        var paidInvoice = CreateInvoice(appointment, services, technician, appointment.EndAt);
        SettleInvoice(paidInvoice, appointment);

        // BR-APT-026 — lịch hẹn tự hoàn tất khi hóa đơn đã thu đủ.
        appointment.CompleteFromPaidInvoice(appointment.EndAt);
    }

    private SalesInvoice CreateInvoice(
        Appointment appointment, List<Service> services, Staff technician, DateTimeOffset issuedAt)
    {
        var businessDate = DateOnly.FromDateTime(issuedAt.DateTime);
        var counter = GetCounter(appointment.TenantId, businessDate);
        var code = InvoiceCounter.FormatCode(businessDate, counter.NextNumber());

        var invoice = SalesInvoice.Create(
            $"INV-{appointment.Id[4..]}",
            appointment.TenantId,
            appointment.BranchId,
            appointment.CustomerId,
            appointment.Id,
            technician.Id,
            code,
            null,
            DemoIds.NaileReceptionUser,
            issuedAt);

        db.Add(invoice);

        // BR-SVC-007 — hóa đơn lấy giá HIỆN TẠI của dịch vụ, rồi chốt lại trên dòng.
        var lineIndex = 0;
        foreach (var service in services)
        {
            invoice.AddLine($"{invoice.Id}-L{++lineIndex}", service.Id, service.Name, service.Price, 1, issuedAt);
        }

        // BR-INV-021 — giảm giá là số tiền nhập tay kèm lý do, không có mã voucher.
        if (_random.Next(0, 10) == 0)
        {
            invoice.ApplyDiscount(50_000, "Khách quen giới thiệu bạn mới", issuedAt);
        }

        // BR-INV-022 — tip cộng vào tiền khách trả nhưng không thuộc doanh thu tiệm.
        if (_random.Next(0, 4) == 0)
        {
            invoice.SetTip(_random.Next(1, 6) * 20_000, issuedAt);
        }

        return invoice;
    }

    private void SettleInvoice(SalesInvoice invoice, Appointment appointment)
    {
        var paidAt = appointment.EndAt;
        var paymentIndex = 0;

        // BR-APT-031 — tiền cọc trở thành một dòng đã thu loại DEPOSIT.
        if (appointment.Deposit > 0)
        {
            invoice.RegisterPayment(
                $"{invoice.Id}-P{++paymentIndex}", PaymentType.Deposit, PaymentMethod.Bank,
                appointment.Deposit, appointment.StartAt.AddDays(-1), "COC-" + appointment.Id[^6..],
                DemoIds.NaileReceptionUser, paidAt);
        }

        var remaining = invoice.Remaining;
        if (remaining <= 0) return;

        // BR-PAY-004 — thỉnh thoảng khách chia hai phương thức trong một lần thu. Đây là
        // ca mà mô hình "mỗi phương thức một dòng" xử lý được mà không cần trường hợp riêng.
        if (_random.Next(0, 6) == 0 && remaining > 100_000)
        {
            var firstHalf = remaining / 2;

            invoice.RegisterPayment(
                $"{invoice.Id}-P{++paymentIndex}", PaymentType.Payment, PaymentMethod.Cash,
                firstHalf, paidAt, null, DemoIds.NaileReceptionUser, paidAt);

            invoice.RegisterPayment(
                $"{invoice.Id}-P{++paymentIndex}", PaymentType.Payment, PaymentMethod.Momo,
                remaining - firstHalf, paidAt, "MOMO-" + invoice.Id[^6..], DemoIds.NaileReceptionUser, paidAt);

            return;
        }

        var method = RandomMethod();

        invoice.RegisterPayment(
            $"{invoice.Id}-P{++paymentIndex}", PaymentType.Payment, method, remaining, paidAt,
            method == PaymentMethod.Cash ? null : method.ToString().ToUpperInvariant() + "-" + invoice.Id[^6..],
            DemoIds.NaileReceptionUser, paidAt);
    }

    private InvoiceCounter GetCounter(string tenantId, DateOnly businessDate)
    {
        var counter = db.InvoiceCounters.Local
            .FirstOrDefault(candidate => candidate.TenantId == tenantId && candidate.BusinessDate == businessDate);

        if (counter is not null) return counter;

        counter = InvoiceCounter.StartOfDay(tenantId, businessDate);
        db.Add(counter);

        return counter;
    }

    // ── Hóa đơn đăng ký, yêu cầu nâng gói, nhật ký ────────────────────────────

    private void SeedSubscriptionInvoices(
        Dictionary<string, Tenant> tenants, Dictionary<string, Package> packages, DateTimeOffset now)
    {
        foreach (var (tenantId, tenant) in tenants)
        {
            // BR-SUB-011 — tiệm đang dùng thử chưa phải trả tiền, nên chưa có hóa đơn nào.
            // Sinh sẵn một hóa đơn cho họ là dựng ra một khoản nợ không có thật.
            if (tenant.IsTrial) continue;

            var package = packages[tenant.PackageId];
            var issuedAt = now.AddDays(-20);

            var invoice = SubscriptionInvoice.Issue(
                $"SUB-{tenantId.Replace("TEN-", string.Empty)}-{++_subscriptionInvoiceSequence:D3}",
                $"DK-{issuedAt:yyyyMM}-{_subscriptionInvoiceSequence:D3}",
                tenantId,
                tenant.Name,
                package.Id,
                package.Name,
                tenant.SubscriptionPrice,
                tenant.BillingCycle,
                issuedAt,
                issuedAt.AddDays(30),
                issuedAt.AddDays(7),
                "Gia hạn định kỳ",
                issuedAt);

            db.Add(invoice);

            // Tiệm quá hạn thì hóa đơn của nó vẫn đang chờ thanh toán — đó chính là lý do
            // vì sao tiệm bị chuyển sang chế độ chỉ đọc (BR-TENANT-010).
            if (tenantId == DemoIds.OasisTenant)
            {
                invoice.SubmitPaymentProof("VCB-20260810-77421", "Đã chuyển khoản, chờ đối soát", now.AddDays(-6));
                continue;
            }

            invoice.ConfirmPaid(DemoIds.SuperAdminUser, issuedAt.AddDays(2));
        }
    }

    /// <summary>
    /// BR-SUB-008/009 — một yêu cầu nâng gói đang chờ duyệt, để demo được cả năm bước mà
    /// không phải tự tạo dữ liệu lúc bảo vệ.
    /// </summary>
    private void SeedUpgradeRequest(
        Dictionary<string, Tenant> tenants, Dictionary<string, Package> packages, DateTimeOffset now)
    {
        var muse = tenants[DemoIds.MuseTenant];
        var premium = packages["PKG-PREMIUM"];

        db.Add(PackageUpgradeRequest.Submit(
            "PUR-MUSE-001",
            muse.Id,
            muse.Name,
            DemoIds.LumiereAdminUser,
            "Nguyễn Văn Boss",
            "tenantadmin@lumierehair.vn",
            muse.PackageId,
            "Basic",
            premium.Id,
            premium.Name,
            premium.BillingCycle,
            now.AddDays(7),
            premium.Price,
            "Tiệm sắp mở thêm chi nhánh, cần vượt hạn mức 1 chi nhánh của gói Basic.",
            now.AddDays(-3)));
    }

    /// <summary>
    /// Nhật ký kiểm toán cho chính các thao tác mà bộ nạp vừa thực hiện.
    /// <para>
    /// Chỉ ghi những sự kiện CÓ THẬT — tiệm được tạo, tài khoản được tạo — và đánh dấu
    /// nguồn là bộ nạp mẫu trong phần dữ liệu kèm theo. Bịa thêm các dòng đăng nhập hay
    /// thu tiền cho danh sách trông dài là làm hỏng đúng thứ mà nhật ký sinh ra để bảo vệ.
    /// </para>
    /// </summary>
    private void SeedAuditTrail(Dictionary<string, Tenant> tenants, DateTimeOffset now)
    {
        foreach (var (tenantId, tenant) in tenants)
        {
            db.Add(AuditLog.Record(
                $"AUD-SEED-{++_auditSequence:D4}",
                AuditEvent.TenantCreated,
                now.AddDays(-1),
                DemoIds.SuperAdminUser,
                UserRole.SuperAdmin,
                tenantId,
                "Tenant",
                tenantId,
                null,
                $"{{\"source\":\"seed\",\"name\":\"{tenant.Name}\"}}"));
        }
    }

    // ── Tiện ích ──────────────────────────────────────────────────────────────

    private AppointmentSource RandomSource()
    {
        // Phần lớn lịch hẹn đặt tại quầy hoặc qua điện thoại — BR-APT-007 nói rõ nguồn chỉ
        // là ghi nhận thủ công, không có hệ thống nào tự sinh lịch.
        var roll = _random.Next(0, 100);

        return roll switch
        {
            < 45 => AppointmentSource.Reception,
            < 75 => AppointmentSource.Phone,
            < 92 => AppointmentSource.Zalo,
            _ => AppointmentSource.Online
        };
    }

    private PaymentMethod RandomMethod()
    {
        var roll = _random.Next(0, 100);

        return roll switch
        {
            < 45 => PaymentMethod.Cash,
            < 70 => PaymentMethod.Bank,
            < 85 => PaymentMethod.Momo,
            < 95 => PaymentMethod.Card,
            _ => PaymentMethod.ZaloPay
        };
    }

    /// <summary>
    /// Ghi thẳng vào một cột mà entity không cho sửa.
    /// <para>
    /// CHỈ dùng cho dữ liệu mẫu, và chỉ với các cột thời gian. Entity cố tình không mở
    /// đường sửa ngày tạo hay hạn dùng đã trôi qua, vì trong hệ thống thật không ai được
    /// làm điều đó; nhưng dữ liệu demo thì cần một quá khứ để báo cáo có gì mà tính.
    /// </para>
    /// </summary>
    private void Backdate<TEntity>(TEntity entity, string propertyName, DateTimeOffset value)
        where TEntity : class
        => db.Entry(entity).Property(propertyName).CurrentValue = value;
}
