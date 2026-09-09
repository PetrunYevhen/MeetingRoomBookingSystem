using Testcontainers.MsSql;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// Starts one real SQL Server container for the whole integration test run (ADR 0002:
/// concurrency/constraint behavior must be proven against the real engine, never
/// InMemory/SQLite). Shared via <see cref="DatabaseCollection"/> so every test class
/// reuses the same container instead of paying container startup cost per class.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    // Same image tag as docker-compose.yml, pinned explicitly per the package's guidance.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "Database";
}
