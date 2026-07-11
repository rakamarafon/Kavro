using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Kavro.Storage.ContractTests;

public abstract class WorkflowStoreContractTests
{
    protected FakeTimeProvider Time { get; }
    protected IWorkflowStore Store { get; }

    protected WorkflowStoreContractTests()
    {
        Time = new FakeTimeProvider(startDateTime: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Store = CreateStore(Time);
    }

    protected abstract IWorkflowStore CreateStore(TimeProvider time);

    protected DateTimeOffset Now => Time.GetUtcNow();

    [Fact]
    public async Task A1_Enqueue_then_LockNext_returns_the_message()
    {
        var msg = TestData.Msg(instanceId: "wf-1", payload: """{"n":42}""");
        await Store.EnqueueAsync(msg);

        var items = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);

        var item = Assert.Single(items);
        Assert.Equal("wf-1", item.InstanceId);
        Assert.Equal(WorkKind.Orchestration, item.Kind);
        Assert.Equal(Payload.FromUtf8("""{"n":42}"""), item.Payload);
        Assert.Equal(1, item.Attempt);
        Assert.Equal(Now + TimeSpan.FromMinutes(1), item.LeaseExpiresAt);
    }
    
    [Fact]
    public async Task A2_LockNext_does_not_return_future_messages()
    {
        await Store.EnqueueAsync(TestData.Msg(visibleAt: Now + TimeSpan.FromMinutes(5)));

        var before = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);
        Assert.Empty(before);

        Time.Advance(TimeSpan.FromMinutes(5));

        var after = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);
        Assert.Single(after);
    }
    
    [Fact]
    public async Task A3_LockNext_respects_maxItems_and_orders_by_VisibleAt()
    {
        await Store.EnqueueAsync(TestData.Msg(instanceId: "wf-late",  visibleAt: Now + TimeSpan.FromMinutes(2)));
        await Store.EnqueueAsync(TestData.Msg(instanceId: "wf-early", visibleAt: Now + TimeSpan.FromMinutes(1)));
        await Store.EnqueueAsync(TestData.Msg(instanceId: "wf-mid",   visibleAt: Now + TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(30))));

        Time.Advance(TimeSpan.FromMinutes(3));

        var first = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 2);

        Assert.Equal(2, first.Count);
        Assert.Equal("wf-early", first[0].InstanceId);
        Assert.Equal("wf-mid",   first[1].InstanceId);

        var rest = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);
        Assert.Equal("wf-late", Assert.Single(rest).InstanceId);
    }
    
    [Fact]
    public async Task A4_Locked_message_is_not_returned_while_lease_is_active()
    {
        await Store.EnqueueAsync(TestData.Msg());

        var first = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Single(first);

        // same worker — can't get same msg again
        var again = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Empty(again);

        // another worker — also
        var other = await Store.LockNextAsync("worker-2", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Empty(other);

        // time goes by, but the lease is still alive - still to no one
        Time.Advance(TimeSpan.FromMinutes(4));
        var stillLocked = await Store.LockNextAsync("worker-2", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Empty(stillLocked);
    }
}