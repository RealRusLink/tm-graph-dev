using System.ComponentModel.DataAnnotations;
using System.Reflection;
using tm_graph_src.Controllers;

namespace tm_graph_tests;

/// <summary>
/// CreateUserDto задаёт правила валидации на позиционных параметрах record-конструктора.
/// Именно эти атрибуты использует MVC при валидации тела запроса, поэтому здесь они
/// применяются к значениям свойств того же record по именам параметров.
/// </summary>
public class CreateUserDtoValidationTest
{
    private static readonly ConstructorInfo DtoConstructor = typeof(CreateUserDto).GetConstructors().Single();

    private static List<ValidationResult> Validate(CreateUserDto dto)
    {
        var results = new List<ValidationResult>();
        foreach (var parameter in DtoConstructor.GetParameters())
        {
            var value = typeof(CreateUserDto).GetProperty(parameter.Name!)!.GetValue(dto);
            var attributes = parameter.GetCustomAttributes<ValidationAttribute>().ToArray();

            Validator.TryValidateValue(
                value,
                new ValidationContext(dto) { MemberName = parameter.Name },
                results,
                attributes);
        }

        return results;
    }

    private static CreateUserDto ValidDto() => new(
        Username: "realruslink",
        Email: "user@example.com",
        Password: "password123");

    private static void AssertFails(CreateUserDto dto, string memberName)
    {
        var results = Validate(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(memberName));
    }

    [Fact]
    public void ValidDto_ShouldPass()
    {
        Assert.Empty(Validate(ValidDto()));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    public void Username_ShouldFail_WhenTooShort(string username)
    {
        AssertFails(ValidDto() with { Username = username }, nameof(CreateUserDto.Username));
    }

    [Fact]
    public void Username_ShouldFail_WhenTooLong()
    {
        AssertFails(ValidDto() with { Username = new string('a', 25) }, nameof(CreateUserDto.Username));
    }

    [Theory]
    [InlineData("user name")]
    [InlineData("user-name")]
    [InlineData("user@name")]
    [InlineData("юзер")]
    public void Username_ShouldFail_WhenContainsForbiddenCharacters(string username)
    {
        AssertFails(ValidDto() with { Username = username }, nameof(CreateUserDto.Username));
    }

    [Fact]
    public void Username_ShouldAccept_BoundaryLength()
    {
        Assert.Empty(Validate(ValidDto() with { Username = "abc" }));
        Assert.Empty(Validate(ValidDto() with { Username = new string('a', 24) }));
        Assert.Empty(Validate(ValidDto() with { Username = "user_123" }));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    public void Email_ShouldFail_WhenInvalid(string email)
    {
        AssertFails(ValidDto() with { Email = email }, nameof(CreateUserDto.Email));
    }

    [Fact]
    public void Email_ShouldFail_WhenTooLong()
    {
        AssertFails(ValidDto() with { Email = new string('a', 60) + "@example.com" }, nameof(CreateUserDto.Email));
    }

    [Fact]
    public void Email_ShouldFail_WhenTooShort()
    {
        AssertFails(ValidDto() with { Email = "a@b.c" }, nameof(CreateUserDto.Email));
    }

    [Fact]
    public void Password_ShouldFail_WhenTooShort()
    {
        AssertFails(ValidDto() with { Password = "1234567" }, nameof(CreateUserDto.Password));
    }

    [Fact]
    public void Password_ShouldFail_WhenTooLong()
    {
        AssertFails(ValidDto() with { Password = new string('a', 65) }, nameof(CreateUserDto.Password));
    }

    [Fact]
    public void Password_ShouldAccept_BoundaryLength()
    {
        Assert.Empty(Validate(ValidDto() with { Password = new string('a', 8) }));
        Assert.Empty(Validate(ValidDto() with { Password = new string('a', 64) }));
    }
}
