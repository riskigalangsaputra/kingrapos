namespace KingraPOS.Infrastructure.Persistence;

public sealed record DatabaseInitializationResult(
    string DatabasePath,
    int SchemaVersion,
    int TableCount,
    bool Created);
