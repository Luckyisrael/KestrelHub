using System.ComponentModel.DataAnnotations;

namespace KestrelHub.Controller.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);
