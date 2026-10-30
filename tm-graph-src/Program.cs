using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tm_graph_src;
using tm_graph_src.Options;
using tm_graph_src.Services;
using tm_graph.Services;
Console.WriteLine(System.Environment.CurrentDirectory);
Console.WriteLine(System.Environment.CurrentDirectory);
Console.WriteLine(System.Environment.CurrentDirectory);
Console.WriteLine(System.Environment.CurrentDirectory);
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

builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<PasswordHashService>();
builder.Services.AddTransient<IMemoryCache, MemoryCache>();
builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton(typeof(ITypedCache<,>), typeof(TypedCacheService<,>));
builder.Services.AddSingleton(typeof(ITypedSet<>), typeof(TypedSetService<>));
builder.Services.AddSingleton<EmailVerificationService>();
builder.Services.AddScoped<UniquenessVerifierService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db");
    }
    );

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddLogging();




var app = builder.Build();

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