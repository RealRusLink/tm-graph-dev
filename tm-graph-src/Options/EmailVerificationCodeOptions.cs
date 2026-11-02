using System.ComponentModel.DataAnnotations;

namespace tm_graph_src.Options;

public class EmailVerificationCodeOptions
{
    public const string SectionName = "EmailVerificationCode";

    [Range(1, 86400)]
    public int TtlSeconds { get; set; }

    [Required]
    public string EmailSubject { get; set; } = "Verification code";

    [Required]
    public string EmailBodyTemplate { get; set; } = "Your code is {code}";
}
