using Microsoft.EntityFrameworkCore;

namespace TodoWebApp;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>().ToTable(nameof(TodoItem));
    }
}

public record TodoItem
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
}