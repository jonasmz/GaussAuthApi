using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Administration.Users.ListUsers;
using GaussAuth.Application.Users.ActivateUser;
using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Application.Users.DeactivateUser;
using GaussAuth.Application.Users.GetUser;
using GaussAuth.Application.Users.Profiles;
using GaussAuth.Api.Administration;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users", CreateUserAsync).RequireGlobalAdministrator().RequireRateLimiting("user-creation");
        app.MapGet("/users", ListUsersAsync).RequireGlobalAdministrator();
        app.MapGet("/users/{userId:guid}", GetUserAsync).RequireGlobalAdministrator();
        app.MapPut("/users/{userId:guid}/profile", UpdateProfileAsync).RequireGlobalAdministrator();
        app.MapPost("/users/{userId:guid}/activate", ActivateUserAsync).RequireGlobalAdministrator();
        app.MapPost("/users/{userId:guid}/deactivate", DeactivateUserAsync).RequireGlobalAdministrator();

        return app;
    }

    private static async Task<IResult> ListUsersAsync(
        bool? isActive,
        string? email,
        string? cursor,
        int? limit,
        ListUsersHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListUsersQuery(isActive, email, cursor, limit), cancellationToken);
        return result.IsInvalid
            ? TypedResults.BadRequest()
            : TypedResults.Ok(new AdminUserListResponse(result.Items.Select(AdminUserSummaryResponse.FromDomain).ToArray(), result.NextCursor));
    }

    private static async Task<IResult> ActivateUserAsync(
        Guid userId,
        ActivateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(userId, cancellationToken);
        return user is null ? UserNotFound() : TypedResults.Ok(UserResponse.FromDomain(user));
    }

    private static async Task<IResult> DeactivateUserAsync(
        Guid userId,
        DeactivateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(userId, cancellationToken);
        return user is null ? UserNotFound() : TypedResults.Ok(UserResponse.FromDomain(user));
    }

    private static IResult UserNotFound() => TypedResults.NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "User not found."
    });

    private static async Task<IResult> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        UpdateProfileHandler handler,
        CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var validationErrors))
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        if (request.ExtensionData is { Count: > 0 })
        {
            var unexpectedFields = request.ExtensionData.Keys.ToDictionary(
                key => key, _ => new[] { "This field is not part of the profile update contract." });
            return TypedResults.ValidationProblem(unexpectedFields);
        }

        var command = new UpdateProfileCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.PhoneNumber);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.User is not null)
        {
            return TypedResults.Ok(UserResponse.FromDomain(result.User));
        }

        if (result.IsNotFound)
        {
            return UserNotFound();
        }

        return TypedResults.ValidationProblem(
            result.ValidationErrors ?? new Dictionary<string, string[]>());
    }

    private static async Task<IResult> GetUserAsync(
        Guid userId,
        GetUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserQuery(userId), cancellationToken);

        return user is null ? UserNotFound() : TypedResults.Ok(UserResponse.FromDomain(user));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        if (HasValidationErrors(request, out var validationErrors))
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var command = new CreateUserCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.PhoneNumber);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.User is not null)
        {
            var response = UserResponse.FromDomain(result.User);
            return TypedResults.Created($"/admin/users/{response.Id}", response);
        }

        if (result.IsDuplicateEmail)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "A user with this email already exists."
            });
        }

        return TypedResults.ValidationProblem(
            result.ValidationErrors ?? new Dictionary<string, string[]>());
    }

    private static bool HasValidationErrors(object request, out Dictionary<string, string[]> errors)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty])
                .Select(member => (member, message: result.ErrorMessage ?? string.Empty)))
            .GroupBy(pair => pair.member)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.message).ToArray());

        return !isValid;
    }
}
