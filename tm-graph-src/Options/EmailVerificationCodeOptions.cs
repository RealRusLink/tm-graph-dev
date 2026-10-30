namespace tm_graph_src.Options;

public class EmailVerificationCodeOptions
{
    public const string SectionName = "EmailVerificationCode";
    public int TtlSeconds { get; set; }
}