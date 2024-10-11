using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Swashbuckle.AspNetCore.Annotations;
using TodoWebApp.Models;

namespace TodoWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TodosController(TodoDbContext dbContext, IMemoryCache cache) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a new TODO Item"
    )]
    [SwaggerResponse(statusCode: 200, "The newly created TODO Item", typeof(TodoItemModel), ["application/json"])]
    [SwaggerResponse(statusCode: 400, "The CreateTodoItemRequest is invalid", typeof(ProblemDetails), ["application/json"])]
    public async Task<Ok<TodoItemModel>> CreateTodoItem([FromBody] CreateTodoItemRequest request, CancellationToken cancellationToken = default)
    {
        var todoItem = new TodoItem
        {
            Title = request.Title,
            Description = request.Description
        };
        var result = dbContext.TodoItems.Add(todoItem);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TodoItemModel
        {
            Id = result.Entity.Id,
            Title = result.Entity.Title,
            Description = result.Entity.Description,
        };

        return TypedResults.Ok(response);
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Get an existing TODO Item",
        Description = "Repeat requests are cached for 30 seconds"
    )]
    [SwaggerResponse(statusCode: 200, "The TODO Item", typeof(TodoItemModel), ["application/json"])]
    [SwaggerResponse(statusCode: 400, "The Id is not an integer", typeof(ProblemDetails), ["application/json"])]
    [SwaggerResponse(statusCode: 404, "The TODO Item could not be found", typeof(ProblemDetails), ["application/json"])]
    public async Task<Results<Ok<TodoItemModel>, NotFound>> GetTodoItem([FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        // Check to see if item is in cache before hitting database
        if (!cache.TryGetValue<TodoItem>(id, out var todoItem))
        {
            todoItem = await dbContext.TodoItems.FindAsync([id], cancellationToken);
            
            // Cache the result for 30 seconds in memory for quicker response times
            cache.Set(id, todoItem, TimeSpan.FromSeconds(30));
        }
        
        return 
            todoItem is not null
                ? TypedResults.Ok(new TodoItemModel {Id = todoItem.Id, Title = todoItem.Title, Description = todoItem.Description })
                : TypedResults.NotFound();
    }
}