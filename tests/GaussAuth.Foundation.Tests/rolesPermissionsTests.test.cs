using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class RolesPermissionsTests
{
    // T013: role creation, normalization, validation, uniqueness, isolation.
    [TestMethod]
    public async Task Role_creation_normalizes_name_and_enforces_application_scoped_uniqueness()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var otherApplicationId = await CreateApplicationAsync(client);

        using var created = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "  Operator  ", description = "Runs operations" });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("Operator", body.GetProperty("name").GetString());

        using var duplicateDifferentCase = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "operator" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicateDifferentCase.StatusCode);

        using var crossApplication = await client.PostAsJsonAsync($"/applications/{otherApplicationId}/roles", new { name = "Operator" });
        Assert.AreEqual(HttpStatusCode.Created, crossApplication.StatusCode);

        using var emptyName = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "   " });
        Assert.AreEqual(HttpStatusCode.BadRequest, emptyName.StatusCode);

        using var tooLongName = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = new string('a', 201) });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLongName.StatusCode);

        using var tooLongDescription = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "Another", description = new string('a', 501) });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLongDescription.StatusCode);

        using var missingApplication = await client.PostAsJsonAsync($"/applications/{Guid.NewGuid()}/roles", new { name = "Role" });
        Assert.AreEqual(HttpStatusCode.NotFound, missingApplication.StatusCode);

        using var deactivateApplication = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApplication.StatusCode);
        using var inactiveApplication = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "AfterDeactivation" });
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveApplication.StatusCode);
    }

    [TestMethod]
    public async Task Role_description_update_is_bounded_and_preserves_name()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var roleId = await CreateRoleAsync(client, applicationId, "Operator");

        using var updated = await client.PutAsJsonAsync($"/applications/{applicationId}/roles/{roleId}/description", new { description = "Updated" });
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var body = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("Operator", body.GetProperty("name").GetString());
        Assert.AreEqual("Updated", body.GetProperty("description").GetString());

        using var tooLong = await client.PutAsJsonAsync($"/applications/{applicationId}/roles/{roleId}/description", new { description = new string('a', 501) });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using var missing = await client.PutAsJsonAsync($"/applications/{applicationId}/roles/{Guid.NewGuid()}/description", new { description = "x" });
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task Concurrent_role_creation_with_same_normalized_name_yields_single_winner()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var attempts = Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name = "Racer" }));
        var responses = await Task.WhenAll(attempts);
        Assert.AreEqual(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
    }

    // T014: permission creation, code validation, normalization, uniqueness, isolation.
    [TestMethod]
    public async Task Permission_creation_normalizes_code_and_enforces_application_scoped_uniqueness()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var otherApplicationId = await CreateApplicationAsync(client);

        using var created = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "  Reservations.Read  ", description = "Read reservations" });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("reservations.read", body.GetProperty("code").GetString());

        using var duplicate = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "reservations.read" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var crossApplication = await client.PostAsJsonAsync($"/applications/{otherApplicationId}/permissions", new { code = "reservations.read" });
        Assert.AreEqual(HttpStatusCode.Created, crossApplication.StatusCode);

        using var tooShort = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "ab" });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooShort.StatusCode);

        using var tooLong = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = new string('a', 129) });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using var invalidPattern = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "Invalid_Code!" });
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidPattern.StatusCode);

        using var tooLongDescription = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "another.code", description = new string('a', 501) });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLongDescription.StatusCode);

        using var missingApplication = await client.PostAsJsonAsync($"/applications/{Guid.NewGuid()}/permissions", new { code = "some.code" });
        Assert.AreEqual(HttpStatusCode.NotFound, missingApplication.StatusCode);

        using var deactivateApplication = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApplication.StatusCode);
        using var inactiveApplication = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "after.deactivation" });
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveApplication.StatusCode);
    }

    [TestMethod]
    public async Task Permission_description_update_is_bounded_and_preserves_code()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var permissionId = await CreatePermissionAsync(client, applicationId, "reservations.read");

        using var updated = await client.PutAsJsonAsync($"/applications/{applicationId}/permissions/{permissionId}/description", new { description = "Updated" });
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var body = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("reservations.read", body.GetProperty("code").GetString());
        Assert.AreEqual("Updated", body.GetProperty("description").GetString());

        using var missing = await client.PutAsJsonAsync($"/applications/{applicationId}/permissions/{Guid.NewGuid()}/description", new { description = "x" });
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task Concurrent_permission_creation_with_same_code_yields_single_winner()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var attempts = Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code = "race.code" }));
        var responses = await Task.WhenAll(attempts);
        Assert.AreEqual(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.AreEqual(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
    }

    // T015: retrieval, pagination, activation lifecycle idempotency.
    [TestMethod]
    public async Task Role_and_permission_retrieval_and_pagination_follow_contract_rules()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var roleId = await CreateRoleAsync(client, applicationId, "Role-A");
        var permissionId = await CreatePermissionAsync(client, applicationId, "permission.a");
        for (var i = 0; i < 3; i++) { await CreateRoleAsync(client, applicationId, $"Extra-Role-{i}"); await CreatePermissionAsync(client, applicationId, $"extra.permission.{i}"); }

        using var getRole = await client.GetAsync($"/applications/{applicationId}/roles/{roleId}");
        Assert.AreEqual(HttpStatusCode.OK, getRole.StatusCode);
        using var getPermission = await client.GetAsync($"/applications/{applicationId}/permissions/{permissionId}");
        Assert.AreEqual(HttpStatusCode.OK, getPermission.StatusCode);

        using var mismatchedRole = await client.GetAsync($"/applications/{Guid.NewGuid()}/roles/{roleId}");
        Assert.AreEqual(HttpStatusCode.NotFound, mismatchedRole.StatusCode);
        using var missingRole = await client.GetAsync($"/applications/{applicationId}/roles/{Guid.NewGuid()}");
        Assert.AreEqual(HttpStatusCode.NotFound, missingRole.StatusCode);

        using var roleList = await client.GetAsync($"/applications/{applicationId}/roles?limit=2");
        Assert.AreEqual(HttpStatusCode.OK, roleList.StatusCode);
        var roleListBody = JsonDocument.Parse(await roleList.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(2, roleListBody.GetProperty("items").GetArrayLength());
        Assert.IsNotNull(roleListBody.GetProperty("nextCursor").GetString());

        using var defaultLimit = await client.GetAsync($"/applications/{applicationId}/roles");
        Assert.AreEqual(HttpStatusCode.OK, defaultLimit.StatusCode);

        using var invalidLimit = await client.GetAsync($"/applications/{applicationId}/roles?limit=0");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        using var tooLargeLimit = await client.GetAsync($"/applications/{applicationId}/roles?limit=101");
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLargeLimit.StatusCode);
        using var invalidCursor = await client.GetAsync($"/applications/{applicationId}/roles?cursor=not-a-guid");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        using var missingApplicationList = await client.GetAsync($"/applications/{Guid.NewGuid()}/roles");
        Assert.AreEqual(HttpStatusCode.NotFound, missingApplicationList.StatusCode);

        using var deactivate = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivate.StatusCode);
        using var repeatDeactivate = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, repeatDeactivate.StatusCode);
        using var activate = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/activate", null);
        Assert.AreEqual(HttpStatusCode.OK, activate.StatusCode);
        using var repeatActivate = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/activate", null);
        Assert.AreEqual(HttpStatusCode.OK, repeatActivate.StatusCode);

        using var deactivateApplication = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApplication.StatusCode);
        using var activateOnInactiveApplication = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/activate", null);
        Assert.AreEqual(HttpStatusCode.Conflict, activateOnInactiveApplication.StatusCode);
    }

    // T023: RolePermission creation, duplicate, reactivation, listing, missing/inactive parents.
    [TestMethod]
    public async Task RolePermission_assignment_rejects_duplicates_and_reactivates_historical_row()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var applicationId = await CreateApplicationAsync(client);
        var roleId = await CreateRoleAsync(client, applicationId, "Operator");
        var permissionId = await CreatePermissionAsync(client, applicationId, "reservations.read");

        using var assigned = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.Created, assigned.StatusCode);
        var firstId = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        using var duplicate = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var removed = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}/remove", null);
        Assert.AreEqual(HttpStatusCode.OK, removed.StatusCode);
        Assert.IsFalse(JsonDocument.Parse(await removed.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());

        using var reassigned = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.OK, reassigned.StatusCode);
        var reassignedBody = JsonDocument.Parse(await reassigned.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(firstId, reassignedBody.GetProperty("id").GetGuid());
        Assert.IsTrue(reassignedBody.GetProperty("isActive").GetBoolean());

        using var list = await client.GetAsync($"/applications/{applicationId}/roles/{roleId}/permissions?limit=1");
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        using var invalidLimitList = await client.GetAsync($"/applications/{applicationId}/roles/{roleId}/permissions?limit=0");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidLimitList.StatusCode);
        using var missingRoleList = await client.GetAsync($"/applications/{applicationId}/roles/{Guid.NewGuid()}/permissions");
        Assert.AreEqual(HttpStatusCode.NotFound, missingRoleList.StatusCode);

        using var missingRoleAssign = await client.PostAsync($"/applications/{applicationId}/roles/{Guid.NewGuid()}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, missingRoleAssign.StatusCode);
        using var missingPermissionAssign = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{Guid.NewGuid()}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, missingPermissionAssign.StatusCode);

        using var deactivateRole = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateRole.StatusCode);
        var inactiveRolePermissionId = await CreatePermissionAsync(client, applicationId, "another.permission");
        using var assignInactiveRole = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{inactiveRolePermissionId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, assignInactiveRole.StatusCode);

        var secondRoleId = await CreateRoleAsync(client, applicationId, "Second-Role");
        using var deactivatePermission = await client.PostAsync($"/applications/{applicationId}/permissions/{permissionId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivatePermission.StatusCode);
        using var assignInactivePermission = await client.PostAsync($"/applications/{applicationId}/roles/{secondRoleId}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, assignInactivePermission.StatusCode);

        using var removeMissingRelationship = await client.PostAsync($"/applications/{applicationId}/roles/{secondRoleId}/permissions/{permissionId}/remove", null);
        Assert.AreEqual(HttpStatusCode.NotFound, removeMissingRelationship.StatusCode);
    }

    // T024: RolePermission cross-application rejection and isolation.
    [TestMethod]
    public async Task RolePermission_rejects_cross_application_pairs_and_stays_isolated()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var firstApplicationId = await CreateApplicationAsync(client);
        var secondApplicationId = await CreateApplicationAsync(client);
        var roleId = await CreateRoleAsync(client, firstApplicationId, "Operator");
        var permissionInOtherApplicationId = await CreatePermissionAsync(client, secondApplicationId, "reservations.read");

        using var crossAssign = await client.PostAsync($"/applications/{firstApplicationId}/roles/{roleId}/permissions/{permissionInOtherApplicationId}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, crossAssign.StatusCode);

        var sameCodePermissionId = await CreatePermissionAsync(client, firstApplicationId, "reservations.read");
        using var assignedSameApplication = await client.PostAsync($"/applications/{firstApplicationId}/roles/{roleId}/permissions/{sameCodePermissionId}", null);
        Assert.AreEqual(HttpStatusCode.Created, assignedSameApplication.StatusCode);

        using var otherApplicationList = await client.GetAsync($"/applications/{secondApplicationId}/roles/{roleId}/permissions");
        Assert.AreEqual(HttpStatusCode.NotFound, otherApplicationList.StatusCode);
    }

    // T025: UserRole creation, duplicate, reactivation, listing, rejection for missing/inactive user/application/membership/role.
    [TestMethod]
    public async Task UserRole_assignment_rejects_duplicates_and_reactivates_historical_row()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);
        var roleId = await CreateRoleAsync(client, applicationId, "Operator");

        using var assigned = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Created, assigned.StatusCode);
        var firstId = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        using var duplicate = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var removed = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}/remove", null);
        Assert.AreEqual(HttpStatusCode.OK, removed.StatusCode);
        Assert.IsFalse(JsonDocument.Parse(await removed.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());

        using var reassigned = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.OK, reassigned.StatusCode);
        var reassignedBody = JsonDocument.Parse(await reassigned.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(firstId, reassignedBody.GetProperty("id").GetGuid());
        Assert.IsTrue(reassignedBody.GetProperty("isActive").GetBoolean());

        using var list = await client.GetAsync($"/applications/{applicationId}/users/{userId}/roles?limit=1");
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        using var missingUserList = await client.GetAsync($"/applications/{applicationId}/users/{Guid.NewGuid()}/roles");
        Assert.AreEqual(HttpStatusCode.NotFound, missingUserList.StatusCode);
        using var missingApplicationList = await client.GetAsync($"/applications/{Guid.NewGuid()}/users/{userId}/roles");
        Assert.AreEqual(HttpStatusCode.NotFound, missingApplicationList.StatusCode);

        using var missingUserAssign = await client.PostAsync($"/applications/{applicationId}/users/{Guid.NewGuid()}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, missingUserAssign.StatusCode);
        using var missingRoleAssign = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{Guid.NewGuid()}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, missingRoleAssign.StatusCode);

        var secondUserId = await CreateUserAsync(client);
        using var noMembershipAssign = await client.PostAsync($"/applications/{applicationId}/users/{secondUserId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, noMembershipAssign.StatusCode);

        using var deactivateUser = await client.PostAsync($"/users/{userId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateUser.StatusCode);
        using var removeBeforeInactiveReassign = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}/remove", null);
        Assert.AreEqual(HttpStatusCode.OK, removeBeforeInactiveReassign.StatusCode);
        using var inactiveUserAssign = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveUserAssign.StatusCode);
    }

    // T026: UserRole rejects role/application mismatch and inactive/absent membership; application isolation.
    [TestMethod]
    public async Task UserRole_rejects_mismatched_application_and_inactive_membership_and_stays_isolated()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var firstApplicationId = await CreateApplicationAsync(client);
        var secondApplicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, firstApplicationId);
        await CreateMembershipAsync(client, userId, secondApplicationId);
        var roleInSecondApplicationId = await CreateRoleAsync(client, secondApplicationId, "Operator");

        using var mismatchedRoleAssign = await client.PostAsync($"/applications/{firstApplicationId}/users/{userId}/roles/{roleInSecondApplicationId}", null);
        Assert.AreEqual(HttpStatusCode.NotFound, mismatchedRoleAssign.StatusCode);

        var roleInFirstApplicationId = await CreateRoleAsync(client, firstApplicationId, "Operator");
        using var assignedFirst = await client.PostAsync($"/applications/{firstApplicationId}/users/{userId}/roles/{roleInFirstApplicationId}", null);
        Assert.AreEqual(HttpStatusCode.Created, assignedFirst.StatusCode);

        using var deactivateMembership = await client.PostAsync($"/applications/{secondApplicationId}/memberships/{userId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateMembership.StatusCode);
        using var inactiveMembershipAssign = await client.PostAsync($"/applications/{secondApplicationId}/users/{userId}/roles/{roleInSecondApplicationId}", null);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveMembershipAssign.StatusCode);

        using var firstApplicationRoles = await client.GetAsync($"/applications/{firstApplicationId}/users/{userId}/roles");
        Assert.AreEqual(HttpStatusCode.OK, firstApplicationRoles.StatusCode);
        var items = JsonDocument.Parse(await firstApplicationRoles.Content.ReadAsStringAsync()).RootElement.GetProperty("items");
        Assert.AreEqual(1, items.GetArrayLength());
        Assert.AreEqual(roleInFirstApplicationId, items[0].GetProperty("roleId").GetGuid());
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email = $"rp-{Guid.NewGuid():N}@example.test", password = "Quickstart!2026", firstName = "A", lastName = "B", displayName = "AB" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/applications", new { code = $"app-{Guid.NewGuid():N}", name = "Application" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid userId, Guid applicationId)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, Guid applicationId, string name)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePermissionAsync(HttpClient client, Guid applicationId, string code)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
