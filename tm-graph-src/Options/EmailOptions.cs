using System.ComponentModel.DataAnnotations;

namespace tm_graph_src.Options;

public class EmailOptions
{
    public const string SectionName = "Email";
    [Required]
    public string SmtpServer { get; set; } = string.Empty;
    [Required]
    public int Port { get; set; }
    [EmailAddress]
    [Required]
    
    public string Address { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
}