using System.Text.RegularExpressions;
using AuthService.Domain.Exceptions;

namespace AuthService.Domain.Entities;

public sealed class User
{
    private const int MaxFailedAttempts = 3;
    private static readonly Regex EmailRegex = new(@"^[\w.-]+@[\w.-]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Z])(?=.*[^A-Za-z0-9]).{8,}$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private constructor for EF Core
    private User() { }

    // Factory Method — validates domain invariants
    public static User Create(string firstName, string lastName, string email, string passwordHash, UserRole role = UserRole.Buyer)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("El nombre es obligatorio.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("El apellido es obligatorio.", nameof(lastName));
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            throw new ArgumentException("El correo electrónico no tiene un formato válido.", nameof(email));

        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Validates raw password format (before hashing) — enforces RN2
    public static void ValidatePasswordFormat(string password)
    {
        if (string.IsNullOrEmpty(password) || !PasswordRegex.IsMatch(password))
            throw new InvalidPasswordException();
    }

    public UserState State => IsLocked()
        ? new LockedState(this)
        : new ActiveState(this);

    public bool IsLocked() => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    public void RecordFailedAttempt()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedAttempts)
            LockUntil(DateTime.UtcNow.AddMinutes(15));
    }

    public void LockUntil(DateTime until)
    {
        LockedUntil = until;
    }

    public void ResetFailedAttempts()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }
}
