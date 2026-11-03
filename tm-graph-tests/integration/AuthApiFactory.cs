using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using tm_graph.Services;
using tm_graph_src.Controllers;

namespace tm_graph_tests;

internal sealed class AuthApiFactory : WebApplicationFactory<AuthController>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"tm-graph-auth-tests-{Guid.NewGuid():N}.db");

    public FakeEmailService Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ToString();

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Db:ConnectionString"] = connectionString,
                ["Email:SmtpServer"] = "localhost",
                ["Email:Port"] = "25",
                ["Email:Address"] = "no-reply@example.com",
                ["Email:Password"] = "test-password",
                ["Email:FromName"] = "App",
                ["Email:SecureSocketOption"] = "None",
                ["EmailVerificationCode:TtlSeconds"] = "300",
                ["EmailVerificationCode:EmailSubject"] = "Verification code",
                ["EmailVerificationCode:EmailBodyTemplate"] = "Your code is {code}",
                ["Session:ShortLifetime"] = "02:00:00",
                ["Session:LongLifetime"] = "30.00:00:00"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
