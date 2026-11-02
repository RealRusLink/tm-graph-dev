namespace tm_graph_src.Services;

public class PasswordHashService
{
    private static readonly string DummyHashValue = BCrypt.Net.BCrypt.HashPassword("dummy-password");

    public string DummyHash => DummyHashValue;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
}
