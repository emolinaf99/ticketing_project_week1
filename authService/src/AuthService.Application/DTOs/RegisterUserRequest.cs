using System.ComponentModel.DataAnnotations;
using AuthService.Domain.Entities;

namespace AuthService.Application.DTOs;

public sealed class RegisterUserRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string FirstName { get; init; } = default!;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100)]
    public string LastName { get; init; } = default!;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [MaxLength(255)]
    public string Email { get; init; } = default!;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; init; } = default!;

    [Required(ErrorMessage = "La confirmación de contraseña es obligatoria.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; init; } = default!;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public UserRole Role { get; init; } = UserRole.Buyer;
}

public sealed record RegisterUserResponse(string Message, string Redirect);
