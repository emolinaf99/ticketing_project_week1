using AuthService.Application.DTOs;
using AuthService.Application.Ports.Input;
using AuthService.Domain.Entities;
using AuthService.Domain.Exceptions;
using AuthService.Domain.Ports.Output;

namespace AuthService.Application.UseCases;

public sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHashingService _passwordHashingService;

    public RegisterUserUseCase(
        IUserRepository userRepository,
        IPasswordHashingService passwordHashingService)
    {
        _userRepository = userRepository;
        _passwordHashingService = passwordHashingService;
    }

    public async Task<RegisterUserResponse> ExecuteAsync(RegisterUserRequest request)
    {
        // RN2: validate password format before hashing
        User.ValidatePasswordFormat(request.Password);

        // RN1: ensure email is unique
        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail))
            throw new EmailAlreadyExistsException(normalizedEmail);

        // RN3: hash before persistence — never store plain text
        string hash = _passwordHashingService.Hash(request.Password);

        var user = User.Create(request.FirstName, request.LastName, normalizedEmail, hash, request.Role);

        await _userRepository.SaveAsync(user);

        return new RegisterUserResponse(
            Message: "Registro exitoso. Serás redirigido al login.",
            Redirect: "/login");
    }
}
