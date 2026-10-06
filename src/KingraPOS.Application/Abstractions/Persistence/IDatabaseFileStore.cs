namespace KingraPOS.Application.Abstractions.Persistence;

public interface IDatabaseFileStore
{
    string DatabasePath { get; }

    string GetDefaultBackupFolder();

    Task CreateSnapshotAsync(string destinationPath, CancellationToken cancellationToken = default);

    Task<int> GetSchemaVersionAsync(string databasePath, CancellationToken cancellationToken = default);

    bool IsSqliteDatabase(string databasePath);

    Task ReplaceDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default);

    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default);
}
