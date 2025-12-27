using Xunit;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Sequential test collection for Quartermaster.
/// UI tests must run sequentially - parallel execution causes FlaUI conflicts.
/// </summary>
[CollectionDefinition("QuartermasterSequential", DisableParallelization = true)]
public class QuartermasterSequentialCollection { }
