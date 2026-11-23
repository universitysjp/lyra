using Xunit;

// Redis containers are started per fixture; serialize classes so concurrent
// fixtures do not exhaust local Docker resources.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
