using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Security;

public sealed class CurrentUserSession : ICurrentUserSession
{
    private HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public bool IsAuthenticated => User is not null;

    public AuthenticatedUserDto? User { get; private set; }

    public IReadOnlySet<string> Permissions => _permissions;

    public void SignIn(AuthenticatedUserDto user, IReadOnlyCollection<string> permissions)
    {
        User = user;
        _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }

    public void SignOut()
    {
        User = null;
        _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
