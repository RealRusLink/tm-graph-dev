using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using tm_graph_src;

namespace tm_graph_tests;

/// <summary>
/// SQLite in-memory database kept alive by an open connection.
/// Each test class instance gets its own isolated database.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
