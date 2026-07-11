using Kavro.Storage.ContractTests;

namespace Kavro.Storage.InMemory.Tests;

public sealed class InMemoryStoreTests : WorkflowStoreContractTests
{
    protected override IWorkflowStore CreateStore(TimeProvider time)
        => new InMemoryWorkflowStore(time);
}