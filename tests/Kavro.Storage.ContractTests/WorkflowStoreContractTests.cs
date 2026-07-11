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
    
    [Fact]
    public async Task B1_Expired_lease_makes_message_available_with_incremented_attempt()
    {
        await Store.EnqueueAsync(TestData.Msg());

        var first = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Equal(1, Assert.Single(first).Attempt);

        // worker-1 "died": does not commit or renew. The lease expires.
        Time.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var reclaimed = await Store.LockNextAsync("worker-2", TimeSpan.FromMinutes(5), maxItems: 10);

        var item = Assert.Single(reclaimed);
        Assert.Equal(2, item.Attempt);                      // counter has increased
        Assert.Equal(Now + TimeSpan.FromMinutes(5), item.LeaseExpiresAt); // new lease from current now
    }
    
    [Fact]
    public async Task B2_RenewLease_extends_expiry()
    {
        await Store.EnqueueAsync(TestData.Msg());
        var item = Assert.Single(await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(5), maxItems: 10));

        Time.Advance(TimeSpan.FromMinutes(4)); // almost expired

        var renewed = await Store.RenewLeaseAsync(item.MessageId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.True(renewed);

        // without renewal, the lease would have expired here
        Time.Advance(TimeSpan.FromMinutes(2));

        var stolen = await Store.LockNextAsync("worker-2", TimeSpan.FromMinutes(5), maxItems: 10);
        Assert.Empty(stolen); // renewal worked - the message is still for worker-1
    }
    
    [Fact]
    public async Task B3_RenewLease_fails_for_foreign_or_expired_lease()
    {
        await Store.EnqueueAsync(TestData.Msg());
        var item = Assert.Single(await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(5), maxItems: 10));

        // someone else's rent
        Assert.False(await Store.RenewLeaseAsync(item.MessageId, "worker-2", TimeSpan.FromMinutes(5)));

        // expired lease - even the owner can't
        Time.Advance(TimeSpan.FromMinutes(6));
        Assert.False(await Store.RenewLeaseAsync(item.MessageId, "worker-1", TimeSpan.FromMinutes(5)));

        // non-existent message
        Assert.False(await Store.RenewLeaseAsync(messageId: 999_999, "worker-1", TimeSpan.FromMinutes(5)));
    }
    
    [Fact]
    public async Task C1_CreateInstance_persists_instance_and_start_message_atomically()
    {
        var instance = TestData.Instance(instanceId: "wf-1", version: 1);
        var start = TestData.Msg(instanceId: "wf-1");

        await Store.CreateInstanceAsync(instance, start);

        var stored = await Store.GetInstanceAsync("wf-1");
        Assert.Equal(instance, stored);

        var items = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);
        Assert.Equal("wf-1", Assert.Single(items).InstanceId);

        var history = await Store.GetHistoryAsync("wf-1");
        Assert.Empty(history);

        Assert.Null(await Store.GetInstanceAsync("no-such"));
    }

    [Fact]
    public async Task C2_CreateInstance_with_duplicate_id_throws_and_changes_nothing()
    {
        await Store.CreateInstanceAsync(TestData.Instance("wf-1", version: 1), TestData.Msg("wf-1"));

        var dup = TestData.Instance("wf-1", version: 99) with { Name = "Other" };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Store.CreateInstanceAsync(dup, TestData.Msg("wf-1")));

        var stored = await Store.GetInstanceAsync("wf-1");
        Assert.Equal(1, stored!.Version);

        var items = await Store.LockNextAsync("worker-1", TimeSpan.FromMinutes(1), maxItems: 10);
        Assert.Single(items);
    }
}