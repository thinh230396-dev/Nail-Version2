using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Branches;
using NailManagement.Domain.Common;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.Features.Branches.UseCases;

/// <summary>
/// Sửa hồ sơ một chi nhánh.
/// <para>
/// Chi nhánh của tiệm khác không tìm thấy được ở đây, và câu trả lời là <c>NOT_FOUND</c> chứ
/// không phải <c>FORBIDDEN</c> — BR-TENANT-013 bước 4. Trả 403 là vô tình xác nhận bản ghi
/// đó có thật, và ghép nhiều câu trả lời như vậy lại là dò được dữ liệu của tiệm khác.
/// </para>
/// </summary>
public sealed class UpdateBranchUseCase(
    IBranchRepository branches,
    IClock clock)
{
    public async Task<BranchDto> ExecuteAsync(
        UpdateBranchCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var branch = await branches.FindByIdAsync(command.BranchId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh.");

        var name = Guard.Length(command.Name, "name", "Tên chi nhánh", 3, 80);

        // Bỏ qua chính nó khi so trùng tên, nếu không thì lưu lại mà không đổi tên cũng hỏng.
        if (await branches.NameExistsAsync(name, branch.Id, cancellationToken))
            throw DomainException.ForField("name", $"Tiệm đã có chi nhánh tên {name}.");

        branch.UpdateProfile(name, command.Code, command.Address, command.Phone, now);
        await branches.UpdateAsync(branch, cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
