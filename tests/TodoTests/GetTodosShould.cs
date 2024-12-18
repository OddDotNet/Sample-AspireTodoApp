using System.Diagnostics;
using System.Net.Http.Json;
using Aspire.Hosting;
using Google.Protobuf;
using Grpc.Net.Client;
using OddDotCSharp;
using OddDotNet.Proto.Common.V1;
using OddDotNet.Proto.Trace.V1;
using TodoWebApp.Models;

namespace TodoTests;

public class GetTodosShould : IAsyncLifetime
{
#pragma warning disable CS8618
    private DistributedApplication _app;
    private HttpClient _todoClient;
    private SpanQueryService.SpanQueryServiceClient _spanQueryServiceClient;
#pragma warning restore CS8618
    
    [Fact]
    public async Task UseCacheOnSecondRequest()
    {
        // ARRANGE
        var todo = new CreateTodoItemRequest
        {
            Title = "test",
            Description = "123"
        };

        var response = await _todoClient.PostAsJsonAsync("/todos", todo);
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
        
        // Special OpenTelemetry header for passing in your own trace ID. The format is:
        // "00-byte[16]-byte[8]-01" a.k.a. ""00-traceId-spanId-01"
        const string traceParent = "traceparent";

        // ACT
        _todoClient.DefaultRequestHeaders.Add(traceParent, $"00-{firstTraceId.ToString()}-{firstSpanId.ToString()}-01");
        var firstRequest = await _todoClient.GetAsync($"/todos/{model.Id}");
        firstRequest.EnsureSuccessStatusCode();
        
        _todoClient.DefaultRequestHeaders.Remove(traceParent); // Remove and re-add with new trace ID
        
        _todoClient.DefaultRequestHeaders.Add(traceParent, $"00-{secondTraceId.ToString()}-{secondSpanId.ToString()}-01");
        var secondRequest = await _todoClient.GetAsync($"/todos/{model.Id}");
        secondRequest.EnsureSuccessStatusCode();

        // ASSERT
        var spanQueryRequest = new SpanQueryRequestBuilder()
            .TakeAll() // Take every span available
            .Wait(TimeSpan.FromSeconds(1)) // Wait for 1 second to allow spans to come in
            .Where(filters => // Add filters
            {
                // Filters added at this level will be AND-ed (&&)
                filters.AddOrFilter(orFilters =>
                {
                    // Filters added in here will be OR-ed (||)
                    orFilters.AddTraceIdFilter(firstTraceIdAsBytes,
                        ByteStringCompareAsType.Equals);
                    orFilters.AddTraceIdFilter(secondTraceIdAsBytes,
                        ByteStringCompareAsType.Equals);
                });
            })
            .Build();
        
        // Start the query
        var spanQueryResponse = await _spanQueryServiceClient.QueryAsync(spanQueryRequest);

        var firstRequestSpans = spanQueryResponse.Spans.Where(span => span.Span.TraceId == ByteString.CopyFrom(firstTraceIdAsBytes)).ToList();
        var secondRequestSpans = spanQueryResponse.Spans.Where(span => span.Span.TraceId == ByteString.CopyFrom(secondTraceIdAsBytes)).ToList();
        
        const string instrumentationScopeName = "OpenTelemetry.Instrumentation.EntityFrameworkCore";
        
        // The first call should have hit the database, so verify that call
        Assert.Contains(firstRequestSpans,
            span => string.Equals(span.InstrumentationScope.Name, instrumentationScopeName));
        
        // The second call should have used cache instead of the database, so verify this span is NOT present
        Assert.DoesNotContain(secondRequestSpans, 
            span => string.Equals(span.InstrumentationScope.Name, instrumentationScopeName));
    }

    public async Task InitializeAsync()
    {
        var appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TodoTestAppHost>();
        _app = await appHostBuilder.BuildAsync();
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await _app.StartAsync();
        _todoClient = _app.CreateHttpClient("todo");
        await resourceNotificationService.WaitForResourceAsync("todo", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(30));
        await resourceNotificationService.WaitForResourceAsync("odddotnet", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(30));
        
        var channel = GrpcChannel.ForAddress(_app.GetEndpoint("odddotnet", "grpc"));
        _spanQueryServiceClient = new SpanQueryService.SpanQueryServiceClient(channel);
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}