namespace TodoWebApp.Models;

public record TodoItemModel
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
}