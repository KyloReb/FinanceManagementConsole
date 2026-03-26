using System.ComponentModel.DataAnnotations;

namespace FMC.Shared.DTOs.User;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string DisplayName => $"{FirstName} {LastName}".Trim();
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
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

    public List<string> Roles { get; set; } = new();
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

    public List<string> Roles { get; set; } = new();
}
