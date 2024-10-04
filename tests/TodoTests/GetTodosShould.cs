using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Google.Protobuf;
using Grpc.Net.Client;
using OddDotCSharp;
using OddDotNet.Proto.Spans.V1;
using TodoWebApp.Models;

namespace TodoTests;

public class GetTodosShould
{
    [Fact]
    public async Task UseCacheOnSecondRequest()
    {
        var appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TodoTestAppHost>();

        await using var app = await appHostBuilder.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync();

        var httpClient = app.CreateHttpClient("todo");
        await resourceNotificationService.WaitForResourceAsync("todo", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(30));
        await resourceNotificationService.WaitForResourceAsync("odddotnet", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(30));

        var todo = new CreateTodoItemRequest
        {
            Title = "test",
            Description = "123"
        };

        var response = await httpClient.PostAsJsonAsync("/todos", todo);

        response.EnsureSuccessStatusCode();

        TodoItemModel? model = await response.Content.ReadFromJsonAsync<TodoItemModel>();
        
        Assert.NotNull(model);
        
        // Create two traceIds and spanIds for the traceparent header
        var firstTraceId = ActivityTraceId.CreateRandom();
        var firstTraceIdAsBytes = Convert.FromHexString(firstTraceId.ToHexString());
        var firstSpanId = ActivitySpanId.CreateRandom();
        var secondTraceId = ActivityTraceId.CreateRandom();
        var secondTraceIdAsBytes = Convert.FromHexString(secondTraceId.ToHexString());
        var secondSpanId = ActivitySpanId.CreateRandom();
        
        const string traceParent = "traceparent";

        httpClient.DefaultRequestHeaders.Add(traceParent, $"00-{firstTraceId.ToString()}-{firstSpanId.ToString()}-01");
        var firstRequest = await httpClient.GetAsync($"/todos/{model.Id}");
        firstRequest.EnsureSuccessStatusCode();
        
        httpClient.DefaultRequestHeaders.Remove(traceParent);
        httpClient.DefaultRequestHeaders.Add(traceParent, $"00-{secondTraceId.ToString()}-{secondSpanId.ToString()}-01");
        var secondRequest = await httpClient.GetAsync($"/todos/{model.Id}");
        secondRequest.EnsureSuccessStatusCode();

        var spanQueryRequest = new SpanQueryRequestBuilder()
            .TakeAll()
            .Wait(TimeSpan.FromSeconds(1))
            .Where(filters =>
            {
                filters.AddOrFilter(orFilters =>
                {
                    orFilters.AddTraceIdFilter(firstTraceIdAsBytes,
                        ByteStringCompareAsType.Equals);
                    orFilters.AddTraceIdFilter(firstTraceIdAsBytes,
                        ByteStringCompareAsType.Equals);
                });
            })
            .Build();
        
        var channel = GrpcChannel.ForAddress("http://localhost:4317");
        var spanQueryServiceClient = new SpanQueryService.SpanQueryServiceClient(channel);
        
        var spanQueryResponse = await spanQueryServiceClient.QueryAsync(spanQueryRequest);

        var firstRequestSpans =
            spanQueryResponse.Spans.Where(span => span.TraceId == ByteString.CopyFrom(firstTraceIdAsBytes));
        var secondRequestSpans = spanQueryResponse.Spans.Where(span => span.TraceId == ByteString.CopyFrom(secondTraceIdAsBytes));
        
        // Assert
        const string instrumentationScopeName = "OpenTelemetry.Instrumentation.EntityFrameworkCore";
        // The first call should have hit the database, so verify that call
        Assert.Contains(firstRequestSpans,
            span => span.InstrumentationScope.Name == instrumentationScopeName);
        
        // The second call should have used cache instead of the database, so verify this span is NOT present
        Assert.DoesNotContain(secondRequestSpans, 
            span => span.InstrumentationScope.Name == instrumentationScopeName);
    }
}