using System.Net;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using NailManagement.Tests.Infrastructure;

namespace NailManagement.Tests.Concurrency;

/// <summary>
/// BR-BRANCH-005 — hạn mức chi nhánh của gói giữ đúng <b>kể cả khi nhiều lệnh tạo đến cùng lúc</b>.
/// <para>
/// Phép kiểm hạn mức là "đếm rồi mới ghi". Không khóa thì mọi lệnh cùng đếm thấy còn chỗ, và tiệm
/// gói Premium (tối đa 3) chạy được năm, sáu chi nhánh — đúng thứ hạn mức sinh ra để chặn, và
/// cũng là thứ tiệm đang trả tiền gói cao hơn để có.
/// </para>
/// <para>
/// Dựng một tiệm mới cho mỗi lần chạy, để phép thử biết chắc tiệm có đúng một chi nhánh chính và
/// còn đúng hai chỗ trống — không phụ thuộc số chi nhánh mà dữ liệu mẫu hay phép thử khác để lại.
/// </para>
/// </summary>
[Collection(SalonSysCollection.Name)]
public sealed class QuotaRaceTests(SalonSysFactory factory)
{
    private const int PremiumMaxSalons = 3;
    private const int Attempts = 6;
    private const string OwnerPassword = "Quota@2026-Test";

    [Fact]
    public async Task Nhieu_lenh_tao_chi_nhanh_cung_luc_khong_vuot_han_muc_goi()
    {
        var (tenantId, ownerEmail) = await CreatePremiumTenantAsync();

        var owners = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(async _ =>
        {
            var client = new SalonSysClient(factory.CreateDefaultClient(new CookieContainerHandler()));
            await client.LoginAsync(ownerEmail, OwnerPassword);
            await client.SelectTenantAsync(tenantId);
            return client;
        }));

        try
        {
            var responses = await Task.WhenAll(owners.Select((client, index) => client.PostAsync(
                "/api/branches", new { name = $"Chi nhánh đồng thời {index + 1}" })));

            // Chi nhánh chính đã chiếm một chỗ.
            Assert.Equal(PremiumMaxSalons - 1, responses.Count(response => response.Status == HttpStatusCode.Created));
            Assert.All(
                responses.Where(response => response.Status != HttpStatusCode.Created),
                response => Assert.Equal("LIMIT_EXCEEDED", response.ErrorCode));

            var branches = await owners[0].GetAsync("/api/branches");
            Assert.Equal(PremiumMaxSalons, branches.CountOf("branches"));
        }
        finally
        {
            foreach (var client in owners) client.Dispose();
        }
    }

    private async Task<(string TenantId, string OwnerEmail)> CreatePremiumTenantAsync()
    {
        using var superadmin = await SalonSysClient.SuperAdminAsync(factory);

        var suffix = Random.Shared.Next(100_000, 999_999);
        var ownerEmail = $"quota{suffix}@salonsys.test";

        var created = await superadmin.PostAsync("/api/tenants", new
        {
            code = $"QUOTA-{suffix}",
            name = $"Tiệm kiểm hạn mức {suffix}",
            packageId = "PKG-PREMIUM",
            expiresAt = DateTimeOffset.UtcNow.AddDays(30),
            isTrial = false,
            primaryBranchName = "Chi nhánh chính",
            owner = new { mode = "new", email = ownerEmail, displayName = "Chủ tiệm kiểm thử", password = OwnerPassword }
        });

        Assert.True(created.Status == HttpStatusCode.Created, created.Body.ToString());

        return (created.Body.GetProperty("tenant").GetProperty("id").GetString()!, ownerEmail);
    }
}
