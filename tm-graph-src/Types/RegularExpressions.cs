using System.Text.RegularExpressions;

namespace tm_graph.Types;

public static class RegularExpressions
{
    public const string EmailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const string UsernameRegex = @"^[a-zA-Z0-9_]+$";
}