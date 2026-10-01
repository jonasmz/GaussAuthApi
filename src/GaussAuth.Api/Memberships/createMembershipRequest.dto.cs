using System.ComponentModel.DataAnnotations;

namespace GaussAuth.Api.Memberships;

public sealed class CreateMembershipRequest { [Required] public Guid UserId { get; set; } }
