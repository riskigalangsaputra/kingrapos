using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ICurrentUserSession
{
    bool IsAuthenticated { get; }

    AuthenticatedUserDto? User { get; }

    IReadOnlySet<string> Permissions { get; }

    void SignIn(AuthenticatedUserDto user, IReadOnlyCollection<string> permissions);

    void SignOut();
}
