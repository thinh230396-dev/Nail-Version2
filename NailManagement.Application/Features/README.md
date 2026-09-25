# Tổ chức Application theo feature

Mỗi feature gom use case, DTO, mapper và helper của cùng một nghiệp vụ. `Services` là mẫu nhỏ để tham khảo; `Appointments` và `SalesInvoices` có thêm helper điều phối.

```text
Features/<Feature>/
├── UseCases/<Action>UseCase.cs
├── <Entity>Dtos.cs
├── <Entity>Mapper.cs
└── DependencyInjection.cs
```

## Thêm hoặc sửa một feature

1. Xác định đầu vào, kết quả, quyền, phạm vi tenant/chi nhánh và trạng thái hợp lệ của luồng.
2. Đặt quy tắc nghiệp vụ trong Domain; đặt điều phối và kiểm tra phạm vi trong use case. Application chỉ đọc/ghi qua interface trong `Abstractions`.
3. Giữ DTO và mapper trong feature. Chỉ tách thư mục con khi có nhiều file; không tạo trước folder rỗng cho module chưa triển khai.
4. Đăng ký use case/helper bằng `Add<Feature>Feature()` trong `DependencyInjection.cs` của feature; gọi một lần từ `AddApplication()`.
5. Controller ánh xạ HTTP request sang input của use case. Không đưa HTTP request type hoặc `DbContext` vào Application.
6. Nếu thêm interface hoặc migration, triển khai ở Infrastructure. Luồng ghi nhiều entity cần xác định ranh giới giao dịch và cách xử lý audit lỗi.
7. Kiểm tra hành vi thay đổi bằng test có ý nghĩa; ghi thay đổi contract và cập nhật `README-SCALE-ROADMAP.md`.

## Phụ thuộc cần giữ

- Application được phụ thuộc Domain và abstraction; không phụ thuộc API, EF Core hoặc frontend.
- `Common` chỉ chứa cơ chế thực sự dùng chung như context, scope, lỗi và phân trang. Helper chuyên nghiệp vụ nằm trong feature của nó.
- DTO hoặc mapper giữa các feature có thể được tham chiếu khi kết quả thực sự cần dữ liệu đó, ví dụ `Sessions` dùng `AccountDto`. Không gọi use case của feature khác chỉ để tái sử dụng vài dòng code; tách policy hoặc abstraction có trách nhiệm rõ nếu cần.
- Với luồng nhiều feature như thu tiền và hoàn tất lịch hẹn, ghi rõ use case điều phối, entity tác động và giao dịch. Chia folder không tự ngăn phụ thuộc vòng.
- Di chuyển namespace không phải lý do đổi route hoặc JSON contract. Khi đổi contract vì nghiệp vụ mới, cập nhật frontend và kiểm thử tương ứng.

## Kiểm tra trước khi bàn giao

```powershell
dotnet build NailManagement.slnx --no-restore
dotnet test NailManagement.slnx --no-restore --verbosity minimal
```

Chạy từ thư mục backend. Bộ test HTTP/SQL dùng database kiểm thử riêng do test factory tạo; không thay cấu hình này sang database khách hàng.
