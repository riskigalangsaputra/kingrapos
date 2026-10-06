using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace KingraPOS.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    public const int CurrentSchemaVersion = 1;

    private const string SchemaScriptResource = "KingraPOS.Infrastructure.Persistence.Scripts.schema.sql";
    private const string TriggersScriptResource = "KingraPOS.Infrastructure.Persistence.Scripts.triggers.sql";

    private static readonly Regex PragmaStatementPattern =
        new(@"(?im)^[ \t]*PRAGMA\b[^;]*;", RegexOptions.Compiled);

    private readonly string _databasePath;

    public DatabaseInitializer(string databasePath)
    {
        _databasePath = databasePath;
    }

    public static DatabaseInitializationResult Initialize(string databasePath) =>
        new DatabaseInitializer(databasePath).Ensure();

    public DatabaseInitializationResult Ensure()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var connection = new SqliteConnection(KingraPosDatabase.BuildConnectionString(_databasePath));
        connection.Open();

        Execute(connection, "PRAGMA journal_mode = WAL;");
        Execute(connection, "PRAGMA foreign_keys = ON;");
        Execute(connection, "PRAGMA busy_timeout = 5000;");

        var created = false;

        if (GetUserVersion(connection) < CurrentSchemaVersion)
        {
            CreateSchema(connection);
            created = true;
        }

        return new DatabaseInitializationResult(
            _databasePath,
            CurrentSchemaVersion,
            CountTables(connection),
            created);
    }

    private static void CreateSchema(DbConnection connection)
    {
        var script = string.Join(
            Environment.NewLine,
            StripPragmaStatements(ReadScript(SchemaScriptResource)),
            StripPragmaStatements(ReadScript(TriggersScriptResource)));

        using (var transaction = connection.BeginTransaction())
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = script;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        Execute(connection, $"PRAGMA user_version = {CurrentSchemaVersion};");
    }

    private static string ReadScript(string resourceName)
    {
        using var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Script '{resourceName}' tidak ditemukan pada assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string StripPragmaStatements(string script) =>
        PragmaStatementPattern.Replace(script, string.Empty);

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int GetUserVersion(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
    }

    private static int CountTables(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";

        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
    }
}
