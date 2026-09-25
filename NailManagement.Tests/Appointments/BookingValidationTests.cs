using System.Net;
using NailManagement.Tests.Infrastructure;

using static NailManagement.Tests.Scenarios.SalonScenario;

namespace NailManagement.Tests.Appointments;

[Collection(SalonSysCollection.Name)]
public sealed class BookingValidationTests(SalonSysFactory factory)
{
    [Fact]
    public async Task Receptionist_cannot_be_assigned_as_appointment_technician()
    {
        using var owner = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var receptionistId = await FirstIdAsync(owner, "/api/staff", "staff",
            staff => Text(staff, "role") == "RECEPTIONIST" && Text(staff, "status") != "INACTIVE");

        var response = await TryBookAsync(owner, await ActiveCustomerIdAsync(owner), receptionistId,
            NextSlot(), [Text(await ActiveServiceAsync(owner), "id")!]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.Contains("staffId", FieldNames(response));
    }

    [Fact]
    public async Task Inactive_branch_rejects_new_booking_but_keeps_existing_appointment_readable()
    {
        using var owner = await SalonSysClient.TenantAdminAsync(factory, Lumiere);
        var technicianId = await TechnicianIdAsync(owner, BranchQ1);
        var customerId = await ActiveCustomerIdAsync(owner);
        var services = new[] { Text(await ActiveServiceAsync(owner), "id")! };
        var old = await TryBookAsync(owner, customerId, technicianId, NextSlot(), services);
        Assert.Equal(HttpStatusCode.Created, old.Status);
        var appointmentId = Text(old.Body.GetProperty("appointment"), "id");

        try
        {
            var disabled = await owner.PatchAsync($"/api/branches/{BranchQ1}/status", new { status = "INACTIVE" });
            Assert.Equal(HttpStatusCode.OK, disabled.Status);

            var read = await owner.GetAsync($"/api/appointments/{appointmentId}");
            Assert.Equal(HttpStatusCode.OK, read.Status);

            var response = await TryBookAsync(owner, customerId, technicianId, NextSlot(), services);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
            Assert.Contains("staffId", FieldNames(response));
        }
        finally
        {
            var restored = await owner.PatchAsync($"/api/branches/{BranchQ1}/status", new { status = "ACTIVE" });
            Assert.Equal(HttpStatusCode.OK, restored.Status);
        }
    }
}
