using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FG.HighThroughput.Engine.Infrastructure;

/// <summary>
/// Runs once at application startup (F5) to guarantee the LocalDB database and target
/// table exist. Uses raw ADO.NET / T-SQL only - no migrations framework, on purpose,
/// to keep the POC frictionless (open the solution, press F5, database is ready).
/// </summary>
public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;
    private readonly IHostEnvironment _environment;

    private const string DatabaseName = "HighThroughputDB";

    public DatabaseInitializerHostedService(IConfiguration configuration, ILogger<DatabaseInitializerHostedService> logger, IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var masterConnectionString = _configuration.GetConnectionString("HighThroughputDbMaster")
            ?? throw new InvalidOperationException("Connection string 'HighThroughputDbMaster' not found.");

        var databaseConnectionString = _configuration.GetConnectionString("HighThroughputDb")
            ?? throw new InvalidOperationException("Connection string 'HighThroughputDb' not found.");

        // Data/log files are kept in a dedicated folder under App_Data (outside the user's
        // profile / OS drive) so it can be excluded from real-time antivirus scanning without
        // exposing an entire user or system folder - just this project's own database files.
        var databaseFolder = Path.Combine(_environment.ContentRootPath, "App_Data", "Database");
        Directory.CreateDirectory(databaseFolder);

        var dataFilePath = Path.Combine(databaseFolder, $"{DatabaseName}.mdf");
        var logFilePath = Path.Combine(databaseFolder, $"{DatabaseName}_log.ldf");

        _logger.LogInformation("Ensuring LocalDB database '{Database}' exists at {Folder}...", DatabaseName, databaseFolder);

        try
        {
            await using (var connection = new SqlConnection(masterConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // If the database is registered in the LocalDB instance but its .mdf/.ldf
                // files were deleted/moved manually (e.g. the App_Data folder was removed),
                // DB_ID(...) still returns a value even though the files no longer exist,
                // and the plain CREATE DATABASE below would never run - leaving an orphaned
                // entry that fails on every subsequent connection attempt. Detect that case
                // here and drop the stale registration first so it can be recreated cleanly.
                if (!File.Exists(dataFilePath))
                {
                    var dropOrphanedDatabaseScript = $"""
                        IF DB_ID(N'{DatabaseName}') IS NOT NULL
                        BEGIN
                            ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{DatabaseName}];
                        END
                        """;

                    await ExecuteNonQueryAsync(connection, dropOrphanedDatabaseScript, cancellationToken);
                }

                // TODO: extract to embedded .sql resource once the real schema is defined.
                // Explicit file paths keep the .mdf/.ldf inside App_Data instead of LocalDB's
                // default location under the user's profile.
                var createDatabaseScript = $"""
                    IF DB_ID(N'{DatabaseName}') IS NULL
                    BEGIN
                        CREATE DATABASE [{DatabaseName}]
                        ON PRIMARY (NAME = N'{DatabaseName}', FILENAME = N'{dataFilePath}')
                        LOG ON (NAME = N'{DatabaseName}_log', FILENAME = N'{logFilePath}');
                    END
                    """;

                await ExecuteNonQueryAsync(connection, createDatabaseScript, cancellationToken);
            }

            await using (var connection = new SqlConnection(databaseConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                const string createTableScript = """
                    IF OBJECT_ID(N'dbo.IngestionData', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.IngestionData
                        (
                            Id BIGINT NOT NULL PRIMARY KEY CLUSTERED,
                            RawValue NVARCHAR(4000) NULL,
                            IngestedAtUtc DATETIME2 NOT NULL
                        );
                    END
                    """;

                await ExecuteNonQueryAsync(connection, createTableScript, cancellationToken);

                // Every app start (F5) begins from a clean slate: TRUNCATE is a metadata/page
                // deallocation operation (not a row-by-row DELETE), so it stays near-instant
                // even after a run of 20M+ rows - it does not scan or log individual rows.
                // Done BEFORE adding the primary key below so leftover duplicate Ids from a
                // previous run (table created without a key) cannot block the ALTER TABLE.
                _logger.LogInformation("Truncating dbo.IngestionData for a clean start...");
                await ExecuteNonQueryAsync(connection, "TRUNCATE TABLE dbo.IngestionData;", cancellationToken);

                // The table may already exist from a previous run of this POC without a
                // clustered primary key (older schema). Add it if missing: without a
                // clustered index on Id, OFFSET/FETCH paging and COUNT_BIG degrade badly
                // once the table holds millions of rows (full heap scan every time).
                const string ensurePrimaryKeyScript = """
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE object_id = OBJECT_ID(N'dbo.IngestionData') AND is_primary_key = 1
                    )
                    BEGIN
                        ALTER TABLE dbo.IngestionData ADD CONSTRAINT PK_IngestionData PRIMARY KEY CLUSTERED (Id);
                    END
                    """;

                await ExecuteNonQueryAsync(connection, ensurePrimaryKeyScript, cancellationToken);
            }
        }
        catch (SqlException ex)
        {
            // Fail fast and loud: without the database/table ready, the whole POC cannot run.
            // Common causes here: LocalDB instance not installed/started, or insufficient permissions.
            _logger.LogCritical(ex, "Failed to initialize LocalDB database '{Database}' (SQL error {ErrorNumber}). " +
                "Verify that '(localdb)\\mssqllocaldb' is installed and running.", DatabaseName, ex.Number);
            throw;
        }

        _logger.LogInformation("Database initialization complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
