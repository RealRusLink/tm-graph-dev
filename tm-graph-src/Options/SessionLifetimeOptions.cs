namespace tm_graph_src.Options;

public class SessionLifetimeOptions
{
    public const string SectionName = "Session";

    public TimeSpan ShortLifetime { get; set; } = TimeSpan.FromHours(2);

    public TimeSpan LongLifetime { get; set; } = TimeSpan.FromDays(30);
}
