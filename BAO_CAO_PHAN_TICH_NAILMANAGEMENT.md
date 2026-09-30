# BÁO CÁO PHÂN TÍCH DỰ ÁN NAILMANAGEMENT

**Ngày đánh giá:** 25/08/2026  
**Thư mục:** `C:\Users\letru\source\repos\NailManagement`  
**Nhánh Git:** `main`  
**Commit gần nhất:** `986fa13` — “Xong ngày 4 và 5”  
**Phạm vi:** mã nguồn backend, kiến trúc, API, domain, persistence, bảo mật, khả năng kiểm thử và mức sẵn sàng triển khai.

> Lưu ý: tại thời điểm đánh giá, working tree có thay đổi chưa commit liên quan đến chức năng liệt kê tài khoản quản trị tenant. Báo cáo phản ánh đúng trạng thái hiện có trên máy, không chỉ trạng thái của commit gần nhất. Không có tệp nguồn hiện hữu nào bị sửa trong quá trình đánh giá.

> **Tình trạng tới 30/09/2026.** Báo cáo dưới đây giữ nguyên như lúc viết (25/08) để làm mốc so sánh. Từ đó đến nay:
>
> | Mục | Tình trạng |
> |---|---|
> | P0.1 — Cookie phiên `Secure = false` | **Đã xử lý.** `Secure` bật ở mọi môi trường trừ Development; có phép thử ở môi trường Staging. |
> | P0.2 — Tài khoản demo nạp ở mọi môi trường | **Đã xử lý.** Chỉ nạp ở Development và khi cờ `DemoSeed:Enabled` bật; nơi khác dùng `Bootstrap:AdminEmail`/`AdminPassword`. |
> | P1.1 — Tự migrate lúc khởi động ở mọi môi trường | **Đã xử lý.** Chỉ tự migrate ở Development (`Database:MigrateOnStartup`); nơi khác dừng ngay nếu còn migration chưa áp. |
> | Thiếu rate limit đăng nhập | **Đã xử lý.** Giới hạn theo IP cộng khóa tạm theo tài khoản. |
> | Chưa có test project, CI | **Đã xử lý.** 136 phép thử tích hợp trên SQL Server thật; GitHub Actions chạy build, kiểm model–migration và toàn bộ phép thử. |
> | Thiếu README vận hành | **Đã xử lý.** `README.md` §1–§6. |
> | Các miền cốt lõi chưa có use case | **Đã xử lý.** Nhân viên, dịch vụ, khách hàng, lịch hẹn, hóa đơn, thu tiền, báo cáo doanh thu đều đã có API. |

---

## 1. Kết luận điều hành

NailManagement là backend ASP.NET Core chạy trên .NET 10, tổ chức theo Clean Architecture với bốn project: API, Application, Domain và Infrastructure. Hệ thống hướng đến mô hình SaaS đa tenant cho chuỗi tiệm nail/salon, dùng SQL Server và Entity Framework Core, xác thực bằng phiên lưu trong cookie.

Nền tảng kỹ thuật được xây khá chắc: chiều phụ thuộc giữa các tầng đúng, Domain không phụ thuộc framework, nghiệp vụ được đóng gói bằng entity/policy/value object, dữ liệu tenant được bảo vệ bằng global query filter, mật khẩu dùng PBKDF2 và API có hợp đồng lỗi nhất quán. Solution hiện build Release thành công với **0 warning, 0 error**; API đang chạy trả `/api/health` HTTP 200; kiểm tra gói NuGet không phát hiện advisory bảo mật đã biết.

Tuy nhiên, đây chưa phải backend quản lý tiệm hoàn chỉnh. API hiện mới bao phủ xác thực, tenant, gói dịch vụ, chi nhánh, audit log và danh sách tài khoản. Các miền cốt lõi như nhân viên, dịch vụ, khách hàng, lịch hẹn, hóa đơn và thanh toán mới chủ yếu có mô hình Domain/database, chưa có use case và controller tương ứng.

Hệ thống **chưa nên triển khai production** trước khi xử lý hai rủi ro nghiêm trọng:

1. Cookie phiên luôn đặt `Secure = false`.
2. Ứng dụng tự chạy migration và tự nạp tài khoản/dữ liệu demo có mật khẩu cố định trong mọi môi trường.

Ngoài ra, dự án chưa có test project, CI/CD, tài liệu vận hành hoặc cấu hình triển khai production.

### Đánh giá tổng quát

| Hạng mục | Đánh giá | Nhận xét ngắn |
|---|---:|---|
| Kiến trúc và phân tầng | Tốt | Ranh giới bốn tầng rõ, chiều phụ thuộc hợp lý |
| Mô hình nghiệp vụ | Tốt | Domain giàu quy tắc, entity bảo vệ trạng thái |
| Multi-tenant | Khá tốt | Có request tenant scope và global query filter fail-closed |
| Chất lượng API hiện có | Khá | Contract lỗi, phân quyền và middleware có chủ đích |
| Độ đầy đủ chức năng | Đang phát triển | Nhiều miền quan trọng chưa được mở qua Application/API |
| Bảo mật production | Chưa đạt | Cookie không Secure, seed tài khoản mặc định, thiếu rate limit |
| Kiểm thử và quality gate | Yếu | Không có test project/CI; format verification đang lỗi |
| Sẵn sàng triển khai | Chưa đạt | LocalDB, tự migrate/seed, thiếu tài liệu và cấu hình vận hành |

---

## 2. Ảnh chụp kỹ thuật của dự án

### 2.1. Quy mô

- 4 project C# trong một solution `.slnx`.
- 169 tệp `.cs`, khoảng 13.014 dòng vật lý, không tính `bin/obj`.
- Nếu loại thư mục migration sinh tự động: 162 tệp `.cs`, khoảng 8.936 dòng.
- 18 entity nghiệp vụ và 18 bảng ứng dụng.
- 3 migration EF Core.
- 19 action controller, cộng 1 health endpoint: tổng cộng 20 thao tác HTTP.
- 0 test project.

### 2.2. Công nghệ chính

| Thành phần | Công nghệ |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| ORM | Entity Framework Core 10.0.11 |
| Database | SQL Server; cấu hình phát triển dùng LocalDB |
| API | Controller API, JSON camelCase, OpenAPI trong Development |
| Xác thực | Phiên tùy biến lưu trong database, ID phiên đặt trong cookie |
| Băm mật khẩu | PBKDF2-HMAC-SHA256, 210.000 vòng |
| DI | Microsoft.Extensions.DependencyInjection |

`Nullable` và `ImplicitUsings` đều được bật ở các project. Domain không dùng package bên ngoài; Application chỉ phụ thuộc abstraction DI; EF Core nằm ở Infrastructure/API đúng vai trò.

### 2.3. Trạng thái Git

Working tree chưa sạch. Có 8 tệp tracked đang sửa và 2 đường dẫn untracked, tập trung vào chức năng danh sách tài khoản quản trị tenant:

- `NailManagement.API/Controllers/AccountsController.cs` — mới, chưa tracked.
- `NailManagement.Application/UseCases/Accounts/` — mới, chưa tracked.
- Các thay đổi ở DTO, mapper, DI, permission attribute và repository tài khoản/user-tenant.
- Phần diff tracked hiện có khoảng 130 dòng thêm mới.

Điều này không làm build Release thất bại, nhưng nên hoàn thiện kiểm thử và commit thành một thay đổi nguyên tử trước khi ghép nhánh hoặc triển khai.

---

## 3. Kiến trúc và chiều phụ thuộc

### 3.1. Bốn tầng

```text
HTTP request
    |
    v
NailManagement.API
    |  controller, middleware, error contract, request scope
    +----------------------+
    v                      v
NailManagement.Application   NailManagement.Infrastructure
    |  use case, DTO, port       | EF Core, repository, security, seed
    +--------------+-------------+
                   v
          NailManagement.Domain
          entity, enum, policy,
          value object, repository contract
```

Kiểm tra namespace cho thấy:

- Domain không tham chiếu Application, Infrastructure, API hoặc namespace công nghệ Microsoft.
- Application không tham chiếu Infrastructure/API.
- Infrastructure hiện thực các repository contract và dịch vụ kỹ thuật.
- API là composition root, chỉ ráp Application và Infrastructure.

Đây là điểm mạnh rõ ràng, giúp thay persistence, viết unit test và phát triển nghiệp vụ mà không kéo framework vào lõi.

### 3.2. Chuỗi xử lý request

Luồng chính được sắp xếp có chủ đích:

1. `ApiExceptionHandler` chuẩn hóa lỗi.
2. Routing xác định endpoint.
3. `SessionMiddleware` đọc phiên ở mỗi request và nạp `RequestScope`.
4. `TenantWriteGuardMiddleware` chặn thao tác ghi khi tenant chỉ được đọc.
5. `RequirePermissionAttribute` kiểm tra đăng nhập, tenant, feature/capability và role.
6. Global query filter trong `NailDbContext` giới hạn dữ liệu theo tenant.
7. Controller gọi use case; controller nhìn chung mỏng.

Thứ tự này tạo nhiều lớp phòng vệ: trạng thái tenant, quyền tính năng, quyền vai trò và cô lập dữ liệu.

---

## 4. Phạm vi chức năng hiện có

### 4.1. API đã triển khai

| Nhóm | Endpoint | Mục đích |
|---|---|---|
| Auth | `POST /api/auth/login` | Đăng nhập bằng email/username |
| Auth | `GET /api/auth/session` | Lấy tài khoản/phiên hiện tại |
| Auth | `GET /api/auth/my-tenants` | Danh sách tenant tài khoản được phép truy cập |
| Auth | `POST /api/auth/session/tenant` | Chọn tenant đang làm việc |
| Auth | `POST /api/auth/logout` | Thu hồi phiên và xóa cookie |
| Tenant | `GET /api/tenants` | Danh sách tenant |
| Tenant | `GET /api/tenants/{id}` | Chi tiết tenant |
| Tenant | `POST /api/tenants` | Tạo tenant, chi nhánh chính, owner và hóa đơn |
| Tenant | `PUT /api/tenants/{id}` | Cập nhật tenant |
| Tenant | `POST /api/tenants/{id}/renew` | Gia hạn tenant |
| Tenant | `PATCH /api/tenants/{id}/status` | Đổi trạng thái tenant |
| Tenant | `DELETE /api/tenants/{id}` | Xóa mềm tenant |
| Package | `GET /api/packages` | Danh sách gói |
| Branch | `GET /api/branches` | Danh sách chi nhánh tenant hiện tại |
| Branch | `POST /api/branches` | Tạo chi nhánh |
| Branch | `PUT /api/branches/{id}` | Cập nhật chi nhánh |
| Branch | `PATCH /api/branches/{id}/status` | Bật/tắt chi nhánh |
| Audit | `GET /api/audit-logs` | Tra cứu nhật ký audit |
| Account | `GET /api/accounts` | Danh sách tài khoản tenant admin; đang là thay đổi chưa commit |
| System | `GET /api/health` | Kiểm tra tiến trình API |

Đường dẫn `/api/**` không khớp route được trả về 404 theo cùng contract lỗi JSON thay vì body rỗng. Đây là chi tiết tốt cho frontend.

### 4.2. Những miền đã có model nhưng chưa có API/use case đầy đủ

- Nhân viên và liên kết tài khoản nhân viên.
- Danh mục dịch vụ.
- Khách hàng và phân hạng khách.
- Lịch hẹn, dịch vụ trong lịch và kiểm tra trùng ca.
- Hóa đơn bán hàng, dòng hóa đơn và thanh toán.
- Yêu cầu nâng cấp gói.
- Quản trị sâu tài khoản/role ngoài danh sách tenant admin.
- Báo cáo, dashboard và các truy vấn tổng hợp nghiệp vụ.

Vì vậy, trạng thái phù hợp nhất hiện nay là **backend nền tảng quản trị tenant và xác thực**, chưa phải bản nghiệp vụ salon hoàn chỉnh.

---

## 5. Mô hình Domain và quy tắc nghiệp vụ

### 5.1. Nhóm thực thể

| Nhóm | Entity |
|---|---|
| Danh tính và phiên | `AppUser`, `AppSession`, `UserTenant` |
| Nền tảng SaaS | `Tenant`, `Package`, `SubscriptionInvoice`, `PackageUpgradeRequest` |
| Vận hành salon | `Branch`, `Staff`, `Service`, `Customer` |
| Lịch hẹn | `Appointment`, `AppointmentService` |
| Bán hàng | `SalesInvoice`, `SalesInvoiceLine`, `InvoicePayment`, `InvoiceCounter` |
| Hệ thống | `AuditLog` |

### 5.2. Điểm mạnh trong Domain

- Entity dùng private setter và factory/method nghiệp vụ thay cho việc sửa trạng thái tùy ý.
- Có value object `Email`, `PhoneNumber`, `RawPassword` cho các dữ liệu cần quy tắc riêng.
- `AppointmentSchedulePolicy` và `AppointmentLifecyclePolicy` gom quy tắc thời gian/trạng thái lịch hẹn.
- `InvoiceMoneyPolicy` bảo vệ phép tính tiền và trạng thái thanh toán.
- `CustomerTierPolicy` suy ra hạng khách từ dữ liệu thay vì cho phép nhập tùy tiện.
- `PermissionMatrix` tách ma trận role/feature khỏi controller.
- `FeatureCapabilityPolicy` nối feature với capability của gói dịch vụ.
- Giá tiền VND dùng số nguyên `long`, tránh sai số số thực.
- Tenant dùng xóa mềm và phân biệt trạng thái lưu trữ với trạng thái hiển thị Trial/Overdue được tính theo thời gian.

### 5.3. Một số điểm cần tinh chỉnh

- `Tenant.ContactEmail`, số điện thoại tenant/branch mới chỉ kiểm tra rỗng/độ dài bằng `Guard.Optional`, chưa dùng value object để kiểm tra đúng định dạng ở server.
- Time zone được lưu như chuỗi nhưng chưa thấy bước xác minh bằng danh sách time zone hợp lệ.
- `Tenant.DisplayStatusAt` dùng `ExpiresAt < now`; tại đúng thời điểm bằng nhau tenant vẫn chưa được xem là quá hạn. Nên chốt quy ước và thường sẽ dùng `<=`.
- Các chuỗi JSON capability/configuration trong Package cần được xác thực cấu trúc trước khi lưu nếu sau này cho phép quản trị qua API.

---

## 6. Dữ liệu và multi-tenant

### 6.1. Lược đồ

Ba migration hiện có:

1. `20260824125511_InitialAuth`
2. `20260824141554_SalonSchema`
3. `20260825015547_DropLegacyAccountScope`

18 bảng ứng dụng:

`AppUsers`, `AppSessions`, `AuditLogs`, `InvoiceCounters`, `Packages`, `Tenants`, `Branches`, `Customers`, `PackageUpgradeRequests`, `Services`, `SubscriptionInvoices`, `UserTenants`, `Staff`, `Appointments`, `AppointmentServices`, `SalesInvoices`, `InvoicePayments`, `SalesInvoiceLines`.

EF configuration có index, unique constraint, giới hạn độ dài và chuyển enum thành chuỗi. Mã tenant đã xóa mềm vẫn được xem là đã chiếm dụng, tránh tái sử dụng định danh nhạy cảm.

### 6.2. Cô lập tenant

`NailDbContext` áp global query filter cho các entity triển khai `ITenantOwned`:

```text
ActiveTenantId phải khác null
AND entity.TenantId phải bằng ActiveTenantId
```

Thiết kế này **fail-closed**: nếu request chưa có tenant, truy vấn dữ liệu tenant không tự rơi về chế độ “xem tất cả”. Đây là lựa chọn an toàn.

Một số truy vấn dùng `IgnoreQueryFilters()` có chủ đích, chủ yếu để:

- Superadmin thống kê tenant.
- Kiểm tra mã tenant kể cả bản ghi đã xóa mềm.
- Tìm hồ sơ nhân viên trong giai đoạn dựng phiên, sau đó use case đối chiếu lại tenant.

Các vị trí bypass filter cần luôn được xem là điểm nhạy cảm và nên có integration test chống rò dữ liệu chéo tenant.

### 6.3. Giao dịch

`CreateTenantUseCase` dùng transaction để tạo đồng thời tenant, chi nhánh chính, owner/link tenant và hóa đơn đăng ký. Đây là đúng yêu cầu nhất quán nghiệp vụ.

Repository hiện tự gọi `SaveChangesAsync`; `EfUnitOfWork` mở transaction bao quanh nên nhiều lần save vẫn tham gia cùng transaction. Cách này hoạt động, nhưng làm ranh giới transaction phân tán và khiến unit test khó hơn so với mô hình repository chỉ theo dõi thay đổi, còn Unit of Work commit một lần.

---

## 7. Xác thực, phân quyền và bảo mật

### 7.1. Những cơ chế đã làm tốt

- Cookie phiên có `HttpOnly = true`, `SameSite = Strict`, path `/` và thời hạn rõ ràng.
- Phiên nằm trong database nên có thể thu hồi khi logout.
- Mật khẩu dùng PBKDF2-HMAC-SHA256, 210.000 vòng, salt 16 byte, key 32 byte.
- So sánh hash dùng `CryptographicOperations.FixedTimeEquals`.
- Khóa tài khoản sau 5 lần sai trong 15 phút.
- Phiên thường 8 giờ, “remember” 30 ngày.
- Phản hồi đăng nhập sai không làm lộ trực tiếp tài khoản có tồn tại hay không.
- Có audit cho đăng nhập, thất bại, khóa tài khoản và các thao tác quản trị.
- Contract lỗi không trả stack trace nội bộ cho client.
- Phân quyền kết hợp tenant, package capability, feature và role.

### 7.2. Phát hiện theo mức ưu tiên

#### P0 — phải sửa trước production

**P0.1. Cookie phiên luôn có `Secure = false`.**  
`NailManagement.API/Controllers/AuthController.cs:146` đặt cứng `Secure = false`. Dù production có HTTPS redirect, browser vẫn có thể gửi cookie qua HTTP trước khi nhận redirect hoặc khi cấu hình proxy sai. ID phiên có nguy cơ bị lộ trên đường truyền.

Khuyến nghị: đặt `Secure = true` ngoài Development, cấu hình forwarded headers đúng khi đứng sau reverse proxy và buộc HTTPS/HSTS ở biên.

**P0.2. Tự tạo tài khoản demo với mật khẩu cố định trong mọi môi trường.**  
`Program.cs` luôn gọi `DemoAccountSeeder` và `DemoDataSeeder`. Mã nguồn chứa các mật khẩu biết trước như `Super@2026`, `Lumiere@2026`, `Reception@2026`, `Tenant@2026`. Seeder chỉ chạy theo điều kiện dữ liệu trống, nhưng một database production mới vẫn thỏa điều kiện đó và có thể sinh tài khoản quản trị mặc định.

Khuyến nghị: chỉ đăng ký/chạy demo seed trong Development hoặc môi trường demo rõ ràng; production phải bootstrap admin qua secret dùng một lần hoặc quy trình quản trị riêng. Đổi ngay mọi mật khẩu đã từng dùng ngoài máy phát triển.

#### P1 — ưu tiên cao

**P1.1. Ứng dụng tự chạy migration khi khởi động ở mọi môi trường.**  
Điều này tiện cho local nhưng có thể khiến nhiều replica tranh migration, kéo dài startup, thay schema ngoài cửa sổ bảo trì hoặc làm toàn bộ dịch vụ không khởi động khi migration lỗi. Nên tách migration thành deployment job có quyền database riêng.

**P1.2. Chưa có rate limiting cho endpoint đăng nhập.**  
Khóa theo tài khoản không thay thế giới hạn theo IP/device/toàn hệ thống; kẻ xấu vẫn có thể credential stuffing hoặc cố tình khóa tài khoản người dùng. Nên thêm ASP.NET Core rate limiter, log và cảnh báo bất thường.

**P1.3. Chưa có test tự động.**  
Không có unit test, integration test hoặc authorization/tenant isolation test. Đây là rủi ro lớn vì hệ thống có quy tắc tiền, lịch, role và bypass query filter. Build thành công không chứng minh đúng nghiệp vụ.

**P1.4. Sinh mã hóa đơn đăng ký có race condition.**  
`CreateTenantUseCase` lấy `CountAsync() + 1` để tạo sequence. Hai request đồng thời có thể nhận cùng số và va unique constraint. Nên dùng database sequence, bảng counter với khóa/transaction hoặc định danh không phụ thuộc số lượng bản ghi.

**P1.5. Cấu hình mới phù hợp local.**  
Connection string LocalDB nằm trong `appsettings.json`, `AllowedHosts` là `*`; trong mã chưa thấy HSTS, forwarded headers hoặc security header. Cần tách `appsettings.Production`, secret store, host allowlist, logging/telemetry và chính sách proxy trước triển khai.

#### P2 — nên xử lý trong các vòng tiếp theo

**P2.1. Validation server chưa đồng đều.**  
Email/số điện thoại/time zone ở tenant và branch mới kiểm tra độ dài, chưa kiểm tra định dạng hoặc giá trị hợp lệ.

**P2.2. Không dùng abstraction tạo ID ở một vị trí.**  
`CreateTenantUseCase` gọi trực tiếp `Guid.NewGuid()` khi tạo owner, dù kiến trúc đã có các system service có thể trừu tượng hóa. Điều này làm test xác định khó hơn.

**P2.3. Quy ước thời điểm hết hạn có sai lệch biên nhỏ.**  
So sánh `< now` thay vì `<= now` cần được xác nhận bằng business rule và test.

**P2.4. Hợp đồng HTTP 423 cần đồng bộ frontend.**  
Backend trả `423 Locked` cho tài khoản bị khóa. Client cần phân loại đây là lỗi xác thực/nghiệp vụ, không phải lỗi server chung.

**P2.5. Tệp thử API đã lỗi thời.**  
`NailManagement.API.http` vẫn gọi `/weatherforecast/`, endpoint mẫu không còn tồn tại.

---

## 8. Chất lượng mã và khả năng bảo trì

### 8.1. Kết quả kiểm tra

| Kiểm tra | Kết quả |
|---|---|
| Restore package | Thành công |
| Build Release, no incremental | Thành công — 0 warning, 0 error |
| Build Debug | Không hoàn tất do tiến trình API đang chạy khóa DLL Debug, không phải lỗi mã nguồn |
| Health endpoint | HTTP 200, body có `status: ok` |
| NuGet vulnerability audit | Không phát hiện package có advisory đã biết |
| Test | Không có test project để thực thi |
| `dotnet format --verify-no-changes` | Thất bại do lỗi whitespace ở 6 tệp |

Sáu tệp bị báo format gồm controller tenant, DI Infrastructure và một số repository. Đây không phải lỗi chức năng nhưng nên chuẩn hóa và đưa format verification vào CI.

### 8.2. Điểm tốt

- Tên lớp và thư mục phản ánh use case/domain rõ.
- Nhiều đoạn có bình luận giải thích “vì sao”, nhất là middleware, query filter và transaction.
- Dùng cancellation token xuyên qua phần lớn luồng bất đồng bộ.
- Lỗi nghiệp vụ có mã riêng và được ánh xạ tập trung sang HTTP.
- Controller nhìn chung không chứa nghiệp vụ phức tạp.

### 8.3. Điểm cần cải thiện

- Một số tệp lớn, đặc biệt `DemoDataSeeder` khoảng 745 dòng và `CreateTenantUseCase` khoảng 269 dòng; nên tách builder/factory hoặc fixture theo miền.
- Repository tự save ở từng phương thức làm transaction boundary khó quan sát.
- Thiếu tài liệu README về cách chạy, tài khoản local, migration, kiến trúc và quy ước môi trường.
- Không có `.github` workflow, Dockerfile hoặc cấu hình triển khai.
- Không có `global.json`; máy khác có SDK không tương thích có thể cho kết quả khác.
- Health check hiện chỉ chứng minh tiến trình API sống, chưa kiểm tra database/dependency readiness.

---

## 9. Độ khớp giữa Domain và API

Domain đã đi trước API khá xa. Điều này có lợi vì luật nghiệp vụ được chuẩn bị sẵn, nhưng cũng tạo hai rủi ro:

1. Các entity/policy chưa được gọi qua use case thực tế nên chưa được kiểm chứng trong luồng end-to-end.
2. Database đã có nhiều bảng dù API chưa dùng, khiến migration và seed phức tạp sớm.

Thứ tự triển khai hợp lý cho phần còn thiếu:

1. Staff và Service — dữ liệu nền cho vận hành.
2. Customer.
3. Appointment và kiểm tra xung đột lịch.
4. SalesInvoice, payment và invoice counter.
5. Dashboard/reporting.

Mỗi miền nên đi trọn lát cắt Domain → Application → Infrastructure → API → integration test, thay vì chỉ bổ sung entity/controller riêng lẻ.

---

## 10. Kế hoạch cải thiện đề xuất

### Giai đoạn A — chặn rủi ro production

1. Loại demo seed khỏi mọi môi trường ngoài Development/demo.
2. Bỏ mật khẩu cố định khỏi mã nguồn và xoay mọi credential liên quan.
3. Đặt cookie `Secure` theo môi trường; cấu hình HTTPS, HSTS và forwarded headers.
4. Tách migration khỏi startup production.
5. Đưa connection string và secret sang environment/secret store.
6. Thêm rate limit cho login và các endpoint nhạy cảm.

### Giai đoạn B — tạo lưới an toàn

1. Tạo `NailManagement.Domain.Tests` cho policy/value object/entity.
2. Tạo integration test dùng database thật hoặc container SQL Server cho repository/query filter.
3. Viết test chống rò dữ liệu tenant, đặc biệt các truy vấn `IgnoreQueryFilters()`.
4. Viết authorization matrix test cho từng role/feature/write mode.
5. Viết test luồng login, khóa tài khoản, chọn tenant, tenant read-only và logout.
6. Thêm CI: restore → build Release → test → format verify → vulnerable package audit.

### Giai đoạn C — hoàn thiện tính đúng

1. Thay `CountAsync() + 1` bằng sequence/counter an toàn đồng thời.
2. Chuẩn hóa validation email, điện thoại và time zone ở server.
3. Chốt quy tắc biên hết hạn và thêm test clock cố định.
4. Chuẩn hóa Unit of Work/SaveChanges để transaction boundary rõ hơn.
5. Hoàn thiện và commit lát cắt Accounts đang dở.

### Giai đoạn D — hoàn thiện nghiệp vụ

1. Mở API/use case cho Staff, Service, Customer.
2. Mở API/use case cho Appointment và kiểm tra lịch.
3. Mở API/use case cho Invoice/Payment.
4. Bổ sung phân trang, sắp xếp và filter nhất quán cho list endpoint.
5. Sinh client từ OpenAPI hoặc kiểm tra contract backend/frontend tự động.

### Giai đoạn E — vận hành

1. README và runbook migration/rollback/bootstrap admin.
2. Health check dạng liveness/readiness, trong đó readiness kiểm tra database.
3. Structured logging, correlation ID, metrics và cảnh báo login bất thường.
4. Backup/restore SQL Server, retention cho session và audit log.
5. Docker/deployment manifest hoặc hướng dẫn IIS/reverse proxy tương ứng môi trường đích.

---

## 11. Tiêu chí tối thiểu trước khi phát hành production

- [ ] Không còn tài khoản hoặc mật khẩu demo biết trước.
- [ ] Cookie phiên có `Secure`, HTTPS/HSTS/proxy được kiểm chứng.
- [ ] Migration production chạy qua deployment job có rollback plan.
- [ ] Secret không nằm trong repository.
- [ ] Login có rate limit và giám sát.
- [ ] Có test tenant isolation và permission matrix.
- [ ] Có integration test cho luồng đăng nhập và các thao tác ghi chính.
- [ ] Build/test/format/audit chạy bắt buộc trong CI.
- [ ] Database readiness health check hoạt động.
- [ ] Working tree sạch, thay đổi Accounts đã được review/commit.
- [ ] Có hướng dẫn triển khai, bootstrap admin, backup và phục hồi.

---

## 12. Kết luận

Dự án có nền móng kiến trúc tốt hơn mức trung bình của một backend đang ở giai đoạn đầu: Domain độc lập, multi-tenant fail-closed, phân quyền nhiều lớp, mật khẩu được băm đúng hướng và transaction tạo tenant được cân nhắc. Đây là nền tảng đáng giữ lại.

Khoảng cách lớn nhất không nằm ở việc “viết lại kiến trúc”, mà ở ba việc thực dụng:

1. Đóng các lỗ hổng cấu hình production và loại dữ liệu demo nguy hiểm.
2. Bổ sung test/CI để chứng minh tenant isolation, permission và quy tắc nghiệp vụ.
3. Hoàn thiện các lát cắt nghiệp vụ còn thiếu từ Application đến API.

Sau khi xử lý nhóm P0/P1 và có bộ integration test cốt lõi, dự án có thể tiến đến môi trường staging an toàn. Ở trạng thái hiện tại, phù hợp cho phát triển/local demo, chưa phù hợp để nhận dữ liệu khách hàng thật.
