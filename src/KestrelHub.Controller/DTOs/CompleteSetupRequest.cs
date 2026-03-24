using System.ComponentModel.DataAnnotations;

namespace KestrelHub.Controller.DTOs;

public record CompleteSetupRequest(
    [Required, EmailAddress] string AdminEmail,
    [Required, MinLength(12)] string AdminPassword,
    [Required] string AdminDisplayName,
    string? InstanceName);
