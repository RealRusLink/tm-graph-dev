using Microsoft.EntityFrameworkCore;

namespace tm_graph_src;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Record> Records => Set<Record>();
    public DbSet<Connection> Connections => Set<Connection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Connection>().HasIndex(e => new { e.FromId, e.ToId }).IsUnique();
        modelBuilder.Entity<Connection>().HasOne(c => c.To).WithMany().HasForeignKey(c => c.ToId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Connection>().HasOne(c => c.From).WithMany().HasForeignKey(c => c.FromId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Connection>().HasOne(c => c.Parent).WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<User>().HasMany(u => u.Records).WithOne(r => r.User).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<Record>().HasMany(r => r.Children).WithOne(r => r.Parent).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Session>().HasOne(s => s.User).WithMany().OnDelete(DeleteBehavior.Cascade);
    }
};

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; }
    public string Email { get; set; }
    public string Hash { get; set; }
    public ICollection<Record> Records { get; set; }
}

public class Record
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; }
    public User User { get; set; }
    
    public string? ParentId { get; set; }
    public Record? Parent { get; set; }
    
    public ICollection<Record> Children { get; set; }
    
    
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class Connection
{   
    
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string ParentId { get; set; }
    public Record Parent { get; set; }
    
    public string FromId { get; set; }
    public Record From { get; set; }
    
    public string ToId { get; set; }
    public Record To { get; set; }
    
}

public class Session
{   
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserAgent { get; set; }
    public string TokenHash { get; set; }
    public string UserId { get; set; }
    public User User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
