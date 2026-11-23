using Xunit;

// Each fixture starts its own PostgreSQL container; serialize classes to keep
// the integration suite within local Docker memory limits.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
