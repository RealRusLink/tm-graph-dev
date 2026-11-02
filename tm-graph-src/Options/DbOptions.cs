using System.ComponentModel.DataAnnotations;

namespace tm_graph_src.Options;

public class DbOptions
{

    public const string SectionName = "Db";

    [Required] public string ConnectionString { get; set; } = String.Empty;
}