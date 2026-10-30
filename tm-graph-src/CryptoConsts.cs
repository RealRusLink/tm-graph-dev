namespace tm_graph_src;

public static class CryptoConsts
{
    public const string Digits = "0123456789";

    public const string HexLower = "0123456789abcdef";

    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";

    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public const string Letters = Lowercase + Uppercase;

    public const string Alphanumeric = Letters + Digits;

    public const int TokenLength = 32;
    
    public const int CodeLength = 12;

}