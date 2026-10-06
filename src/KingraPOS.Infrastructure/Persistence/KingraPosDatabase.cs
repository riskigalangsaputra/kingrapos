using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Infrastructure.Persistence;

public static class KingraPosDatabase
{
    public const string ApplicationFolderName = "KingraPOS";

    public const string DatabaseFileName = "kingrapos.db";

    public static string GetDatabasePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName,
        DatabaseFileName);

    public static string BuildConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = true
        }.ToString();

    public static DbContextOptions<KingraPosDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<KingraPosDbContext>()
            .UseSqlite(BuildConnectionString(databasePath))
            .Options;

    public static KingraPosDbContext CreateContext(string databasePath) =>
        new(CreateOptions(databasePath));
}
