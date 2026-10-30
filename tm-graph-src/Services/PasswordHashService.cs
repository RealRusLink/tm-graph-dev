namespace tm_graph_src.Services;

public class PasswordHashService
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
}