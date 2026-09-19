using System.Security.Claims;
using System.Text;
using NSubstitute;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using SubiektNexoConnector.Infrastructure.Nexo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubiektNexoConnector.Api.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public sealed class IdempotencyMiddlewareTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "idempotency-http-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Idempotency:DatabasePath"] = Path.Combine(_directory, "test.db") }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdempotencyPersistence(configuration);
        _services = services.BuildServiceProvider();
        await using var db = await _services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>().CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task NewRequest_PassesBodyAndOperationIdToNextDelegate()
    {
        var context = CreateContext();
        var execution = new IdempotencyExecutionContext();
        var called = false;
        await InvokeAsync(context, execution, async http =>
        {
            called = true;
            Assert.NotNull(execution.OperationId);
            Assert.Equal("{\"sku\":\"ABC\"}", await new StreamReader(http.Request.Body).ReadToEndAsync());
        });
        Assert.True(called);
    }

    [Theory]
    [InlineData("{\"sku\":\"ABC\"}", "idempotency_operation_in_progress")]
    [InlineData("{\"sku\":\"XYZ\"}", "idempotency_key_reused")]
    public async Task DuplicateOrChangedPayload_IsBlocked(string body, string code)
    {
        var store = _services.GetRequiredService<IdempotencyOperationStore>();
        await store.CreateAsync("client-a", "key-1", "POST", "/products",
            Encoding.UTF8.GetBytes("{\"sku\":\"ABC\"}"), DateTimeOffset.UtcNow);
        var duplicate = CreateContext(body);
        await InvokeAsync(duplicate, new(), _ => throw new InvalidOperationException("Must not execute again."));
        Assert.Equal(409, duplicate.Response.StatusCode);
        Assert.Contains(code, ResponseBody(duplicate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("ą")]
    public async Task InvalidKey_IsRejectedBeforeCreatingOperation(string key)
    {
        var context = CreateContext();
        context.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        await InvokeAsync(context, new(), _ => throw new InvalidOperationException());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Null(await _services.GetRequiredService<IdempotencyOperationStore>().FindAsync("client-a", key));
    }

    [Fact]
    public async Task MultipleKeys_AreRejected()
    {
        var context = CreateContext();
        context.Request.Headers[IdempotencyMiddleware.HeaderName] = new[] { "key-1", "key-2" };
        await InvokeAsync(context, new(), _ => throw new InvalidOperationException());
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnmarkedEndpoint_BypassesIdempotency()
    {
        var context = CreateContext();
        context.SetEndpoint(null);
        context.Request.Headers.Clear();
        var called = false;
        var execution = new IdempotencyExecutionContext();
        await InvokeAsync(context, execution, _ => { called = true; return Task.CompletedTask; });
        Assert.True(called);
        Assert.Null(execution.OperationId);
    }

    [Fact]
    public async Task AnonymousRequest_IsRejected()
    {
        var context = CreateContext();
        context.User = new ClaimsPrincipal();
        await InvokeAsync(context, new(), _ => throw new InvalidOperationException());
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task OversizedBodyWithoutContentLength_IsRejectedAndRewound()
    {
        var context = CreateContext(new string('a', IdempotencyMiddleware.MaxBodyBytes + 1));
        await InvokeAsync(context, new(), _ => throw new InvalidOperationException());
        Assert.Equal(413, context.Response.StatusCode);
        Assert.Equal(0L, context.Request.Body.Position);
    }

    [Fact]
    public async Task CompletedResponse_IsPersistedAndReplayedWithoutExecutingSferaAgain()
    {
        var first = CreateContext();
        var execution = new IdempotencyExecutionContext();
        await InvokeAsync(first, execution, async context =>
        {
            ExecuteSfera(() =>
            {
                Assert.True(execution.ExecutionStarted);
            });
            context.Response.StatusCode = 201;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Location = "/products/ABC";
            await context.Response.Body.WriteAsync("{\"sku\":\"ABC\"}"u8.ToArray());
        });
        var persisted = (await _services.GetRequiredService<IdempotencyOperationStore>()
            .FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Completed, persisted.State);
        Assert.Equal(201, persisted.ResponseStatusCode);
        var replay = CreateContext();
        await InvokeAsync(replay, new(), _ => throw new InvalidOperationException("Must not execute."));
        Assert.Equal(201, replay.Response.StatusCode);
        Assert.Equal(ResponseBody(first), ResponseBody(replay));
        Assert.Equal("application/json", replay.Response.ContentType);
        Assert.Equal("/products/ABC", replay.Response.Headers.Location.ToString());
        Assert.Equal("true", replay.Response.Headers["Idempotency-Replayed"].ToString());
        Assert.Null(_services.GetRequiredService<IdempotencyExecutionAccessor>().Current);
    }

    [Fact]
    public async Task ClientDisconnectDuringExecution_PersistsFullResponseForRetry()
    {
        var first = CreateContext();
        using var disconnected = new CancellationTokenSource();
        first.RequestAborted = disconnected.Token;
        var execution = new IdempotencyExecutionContext();

        await InvokeAsync(first, execution, async context =>
        {
            ExecuteSfera(() => { });
            disconnected.Cancel();

            Assert.False(context.RequestAborted.IsCancellationRequested);
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Location = "/products/ABC";
            await context.Response.Body.WriteAsync("{\"sku\":\"ABC\"}"u8.ToArray(), context.RequestAborted);
        });

        Assert.True(first.RequestAborted.IsCancellationRequested);
        var persisted = (await _services.GetRequiredService<IdempotencyOperationStore>()
            .FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Completed, persisted.State);
        Assert.Equal("{\"sku\":\"ABC\"}", Encoding.UTF8.GetString(persisted.ResponseBody!));

        var retry = CreateContext();
        await InvokeAsync(retry, new(), _ => throw new InvalidOperationException("Must replay."));

        Assert.Equal(StatusCodes.Status201Created, retry.Response.StatusCode);
        Assert.Equal("{\"sku\":\"ABC\"}", ResponseBody(retry));
        Assert.Equal("true", retry.Response.Headers["Idempotency-Replayed"].ToString());
    }

    [Theory]
    [InlineData("POST", "Check the resource list")]
    [InlineData("PATCH", "Check the current resource data")]
    [InlineData("PUT", "Check the current resource data")]
    [InlineData("DELETE", "Check whether the resource still exists")]
    public async Task FailureAfterExecutionStarted_IsIndeterminateAndDuplicateIsBlocked(string method, string detail)
    {
        var first = CreateContext();
        first.Request.Method = method;
        await Assert.ThrowsAsync<IdempotencyIndeterminateException>(() => InvokeAsync(first, new(), _ =>
        {
            ExecuteSfera(() => throw new Exception("Uncertain Sfera write"));
            return Task.CompletedTask;
        }));
        var persisted = (await _services.GetRequiredService<IdempotencyOperationStore>()
            .FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Indeterminate, persisted.State);
        var duplicate = CreateContext();
        duplicate.Request.Method = method;
        await InvokeAsync(duplicate, new(), _ => throw new Exception("Must not execute."));
        Assert.Equal(409, duplicate.Response.StatusCode);
        Assert.Contains("idempotency_result_indeterminate", ResponseBody(duplicate));
        Assert.Contains(detail, ResponseBody(duplicate));
    }

    [Fact]
    public async Task ConnectionFailure_ReleasesPendingAndAllowsSameKeyRetry()
    {
        var calls = 0;
        await Assert.ThrowsAsync<Exception>(() => InvokeAsync(CreateContext(), new(), _ =>
        {
            ExecuteSfera(() => calls++, new Exception("Login failed"));
            return Task.CompletedTask;
        }));
        Assert.Equal(0, calls);
        var store = _services.GetRequiredService<IdempotencyOperationStore>();
        Assert.Null(await store.FindAsync("client-a", "key-1"));
        await InvokeAsync(CreateContext(), new(), _ =>
        {
            ExecuteSfera(() => calls++);
            return Task.CompletedTask;
        });
        Assert.Equal(1, calls);
        Assert.Equal(IdempotencyRequestState.Completed, (await store.FindAsync("client-a", "key-1"))!.State);
    }

    [Fact]
    public async Task FailedExecutingTransition_DoesNotCallSferaOperation()
    {
        var execution = new IdempotencyExecutionContext();
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(CreateContext(), execution, async _ =>
        {
            await _services.GetRequiredService<IdempotencyOperationStore>()
                .ReleasePendingAsync(execution.OperationId!.Value);
            ExecuteSfera(() => called = true);
        }));
        Assert.False(called);
        Assert.False(execution.ExecutionStarted);
    }

    [Fact]
    public async Task DatabaseFailureAfterSfera_DoesNotSendBufferedSuccess()
    {
        var context = CreateContext();
        await Assert.ThrowsAsync<IdempotencyIndeterminateException>(() => InvokeAsync(context, new(), async http =>
        {
            ExecuteSfera(() => { });
            http.Response.StatusCode = 201;
            await http.Response.Body.WriteAsync("created"u8.ToArray());
            await using var db = await _services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
                .CreateDbContextAsync();
            // Simulate loss of the result store after the business operation succeeded.
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IdempotencyOperations");
        }));
        Assert.Empty(ResponseBody(context));
    }

    private void ExecuteSfera(Action operation, Exception? connectionFailure = null)
    {
        var sessionFactory = Substitute.For<ISessionFactory>();
        if (connectionFailure is not null)
            sessionFactory.Create().Returns(_ => throw connectionFailure);
        using var executor = new SferaExecutor(sessionFactory, new SferaExecutionOptions(),
            _services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SferaExecutor>>(),
            _services.GetRequiredService<IdempotencyOperationStore>(),
            _services.GetRequiredService<IdempotencyExecutionAccessor>());
        executor.Execute(_ => operation());
    }

    private DefaultHttpContext CreateContext(string body = "{\"sku\":\"ABC\"}")
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = "POST";
        context.Request.Path = "/products";
        context.Request.Headers[IdempotencyMiddleware.HeaderName] = "key-1";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "client-a") }, "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()), "test"));
        return context;
    }

    private Task InvokeAsync(HttpContext context, IdempotencyExecutionContext execution, RequestDelegate next) =>
        new IdempotencyMiddleware(next).InvokeAsync(context,
            _services.GetRequiredService<IdempotencyOperationStore>(), execution,
            _services.GetRequiredService<IdempotencyExecutionAccessor>(),
            _services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IdempotencyMiddleware>>());

    private static string ResponseBody(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
