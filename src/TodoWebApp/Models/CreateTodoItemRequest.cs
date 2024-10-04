using System.ComponentModel.DataAnnotations;

namespace TodoWebApp.Models;

public record CreateTodoItemRequest
{
    [Required]
    public required string Title { get; init; }
    public string? Description { get; init; }
}