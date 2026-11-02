using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using tm_graph_src;
using tm_graph_src.Options;
using tm_graph_src.Services;
using tm_graph.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<EmailVerificationCodeOptions>()
    .Bind(builder.Configuration.GetSection(EmailVerificationCodeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SessionLifetimeOptions>()
    .Bind(builder.Configuration.GetSection(SessionLifetimeOptions.SectionName))
    .Validate(o => o.ShortLifetime > TimeSpan.Zero, "Session:ShortLifetime must be positive.")
    .Validate(o => o.LongLifetime >= o.ShortLifetime,
        "Session:LongLifetime must be greater than or equal to Session:ShortLifetime.")
    .ValidateOnStart();

builder.Services.AddOptions<DbOptions>()
    .Bind(builder.Configuration.GetSection(DbOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<PasswordHashService>();
builder.Services.AddTransient<IMemoryCache, MemoryCache>();
builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton(typeof(ITypedCache<,>), typeof(TypedCacheService<,>));
builder.Services.AddSingleton(typeof(ITypedSet<>), typeof(TypedSetService<>));
builder.Services.AddSingleton<EmailVerificationService>();
builder.Services.AddScoped<IUserStore, UserStore>();
builder.Services.AddScoped<ISessionStore, SessionStore>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<UniquenessVerifierService>();

builder.Services.AddDbContext<AppDbContext>((services, options) =>
    {
        var dbConfig = services.GetService<IOptions<DbOptions>>();
        options.UseSqlite(dbConfig!.Value.ConnectionString);
    }
    );

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddLogging();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = SessionAuthenticationOptions.SchemeName;
}).AddScheme<SessionAuthenticationOptions, SessionAuthenticationHandler>(SessionAuthenticationOptions.SchemeName,
    o => { });

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

var app = builder.Build();
    
if (!EF.IsDesignTime)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseRouting();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();