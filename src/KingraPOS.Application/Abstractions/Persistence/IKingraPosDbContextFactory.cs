namespace KingraPOS.Application.Abstractions.Persistence;

public interface IKingraPosDbContextFactory
{
    IKingraPosDbContext Create();
}
