using System.ComponentModel.DataAnnotations;

namespace FMC.Shared.DTOs.User;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public Guid IdGuid => Guid.TryParse(Id, out var g) ? g : Guid.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string DisplayName => $"{FirstName} {LastName}".Trim();
    public bool IsActive { get; set; }
    public string? Organization { get; set; }
    public Guid? OrganizationId { get; set; }
    public string OrganizationAccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Role { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Password { get; set; } // Optional: only if updating password

    public string? Organization { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateUserDto
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Organization { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
