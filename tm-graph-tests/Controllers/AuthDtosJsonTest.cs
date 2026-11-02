using System.Text.Json;
using tm_graph_src.Controllers;

namespace tm_graph_tests;

public class AuthDtosJsonTest
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LoginDto_ShouldReadRememberMeFromSnakeCase()
    {
        var dto = JsonSerializer.Deserialize<LoginDto>(
            """{"identifier":"user","password":"password123","remember_me":true}""", Options);

        Assert.NotNull(dto);
        Assert.Equal("user", dto.Identifier);
        Assert.True(dto.RememberMe);
    }

    [Fact]
    public void ConfirmDto_ShouldReadRememberMeFromSnakeCase()
    {
        var dto = JsonSerializer.Deserialize<ConfirmDto>(
            """{"code":"123456789012","remember_me":true}""", Options);

        Assert.NotNull(dto);
        Assert.Equal("123456789012", dto.Code);
        Assert.True(dto.RememberMe);
    }
}
