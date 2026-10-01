using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Users.ActivateUser;
using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Application.Users.DeactivateUser;
using GaussAuth.Application.Users.GetUser;
using GaussAuth.Application.Users.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users", CreateUserAsync).RequireRateLimiting("user-creation");
        app.MapGet("/users/{id:guid}", GetUserAsync);
        app.MapPut("/users/{id:guid}/profile", UpdateProfileAsync);
        app.MapPost("/users/{id:guid}/activate", ActivateUserAsync);
        app.MapPost("/users/{id:guid}/deactivate", DeactivateUserAsync);

        return app;
    }

    private static async Task<IResult> ActivateUserAsync(
        Guid id,
        ActivateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(id, cancellationToken);
        return user is null ? UserNotFound() : TypedResults.Ok(UserResponse.FromDomain(user));
    }

    private static async Task<IResult> DeactivateUserAsync(
        Guid id,
        DeactivateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(id, cancellationToken);
        return user is null ? UserNotFound() : TypedResults.Ok(UserResponse.FromDomain(user));
    }

    private static IResult UserNotFound() => TypedResults.NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "User not found."
    });

    private static async Task<IResult> UpdateProfileAsync(
        Guid id,
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
            id,
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.PhoneNumber,
            request.AvatarReference);

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
        Guid id,
        GetUserHandler handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserQuery(id), cancellationToken);

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
            request.PhoneNumber,
            request.AvatarReference);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.User is not null)
        {
            var response = UserResponse.FromDomain(result.User);
            return TypedResults.Created($"/users/{response.Id}", response);
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
