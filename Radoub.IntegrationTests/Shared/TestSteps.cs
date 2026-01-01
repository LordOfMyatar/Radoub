using System.Text;

namespace Radoub.IntegrationTests.Shared;

/// <summary>
/// Tracks test steps with diagnostic output.
/// Continues after first failure, stops on second failure.
/// Rationale: One failure = specific issue to fix. Two failures = fundamental problem.
/// </summary>
public class TestSteps
{
    private readonly List<StepResult> _results = new();
    private int _failureCount = 0;
    private bool _stopped = false;

    /// <summary>
    /// Runs a step and records the result.
    /// Continues after first failure, stops on second.
    /// </summary>
    /// <param name="name">Descriptive name for the step</param>
    /// <param name="action">Action that returns true for success, false for failure</param>
    /// <returns>True if step passed, false if failed or skipped</returns>
    public bool Run(string name, Func<bool> action)
    {
        if (_stopped)
        {
            _results.Add(new StepResult(name, StepStatus.Skipped, "Stopped after second failure"));
            return false;
        }

        try
        {
            var passed = action();
            if (passed)
            {
                _results.Add(new StepResult(name, StepStatus.Passed));
                return true;
            }
            else
            {
                return RecordFailure(name, "Returned false");
            }
        }
        catch (Exception ex)
        {
            return RecordFailure(name, ex.Message);
        }
    }

    /// <summary>
    /// Runs a step that doesn't return a value (void action).
    /// Success = no exception thrown.
    /// </summary>
    public bool Run(string name, Action action)
    {
        return Run(name, () =>
        {
            action();
            return true;
        });
    }

    /// <summary>
    /// Runs a step that returns an object.
    /// Success = non-null result.
    /// </summary>
    public T? Run<T>(string name, Func<T?> action) where T : class
    {
        if (_stopped)
        {
            _results.Add(new StepResult(name, StepStatus.Skipped, "Stopped after second failure"));
            return null;
        }

        try
        {
            var result = action();
            if (result != null)
            {
                _results.Add(new StepResult(name, StepStatus.Passed));
                return result;
            }
            else
            {
                RecordFailure(name, "Returned null");
                return null;
            }
        }
        catch (Exception ex)
        {
            RecordFailure(name, ex.Message);
            return null;
        }
    }

    private bool RecordFailure(string name, string reason)
    {
        _failureCount++;
        _results.Add(new StepResult(name, StepStatus.Failed, reason));

        if (_failureCount >= 2)
        {
            _stopped = true;
        }

        return false;
    }

    /// <summary>
    /// Gets the number of failures encountered.
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// Gets whether the test was stopped early (2+ failures).
    /// </summary>
    public bool WasStopped => _stopped;

    /// <summary>
    /// Gets all step results.
    /// </summary>
    public IReadOnlyList<StepResult> Results => _results;

    /// <summary>
    /// Asserts all steps passed. Throws with detailed diagnostic output if any failed.
    /// </summary>
    public void AssertAllPassed()
    {
        if (_failureCount == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"Test failed with {_failureCount} failure(s){(_stopped ? " (stopped early)" : "")}");
        sb.AppendLine();
        sb.AppendLine("Step Results:");
        sb.AppendLine("─────────────");

        for (int i = 0; i < _results.Count; i++)
        {
            var step = _results[i];
            var marker = step.Status switch
            {
                StepStatus.Passed => "✓",
                StepStatus.Failed => "✗",
                StepStatus.Skipped => "○",
                _ => "?"
            };

            var line = $"  {marker} Step {i + 1}: {step.Name}";
            if (step.Status == StepStatus.Failed)
            {
                line += $" <-- FAILED: {step.Reason}";
            }
            else if (step.Status == StepStatus.Skipped)
            {
                line += " (skipped)";
            }

            sb.AppendLine(line);
        }

        sb.AppendLine();

        // Add investigation hints based on failure patterns
        var firstFailure = _results.FirstOrDefault(r => r.Status == StepStatus.Failed);
        if (firstFailure != null)
        {
            var failIndex = _results.IndexOf(firstFailure);
            sb.AppendLine("Investigation:");
            sb.AppendLine($"  First failure at step {failIndex + 1}: \"{firstFailure.Name}\"");

            if (failIndex == 0)
            {
                sb.AppendLine("  → App launch or initialization issue");
            }
            else if (failIndex == 1)
            {
                sb.AppendLine("  → Navigation or panel visibility issue");
            }
            else
            {
                sb.AppendLine($"  → Previous {failIndex} steps passed - issue is specific to this element");
            }
        }

        throw new Xunit.Sdk.XunitException(sb.ToString());
    }

    /// <summary>
    /// Gets a summary string for logging.
    /// </summary>
    public string GetSummary()
    {
        var passed = _results.Count(r => r.Status == StepStatus.Passed);
        var failed = _results.Count(r => r.Status == StepStatus.Failed);
        var skipped = _results.Count(r => r.Status == StepStatus.Skipped);

        return $"Steps: {passed} passed, {failed} failed, {skipped} skipped";
    }
}

/// <summary>
/// Result of a single test step.
/// </summary>
public record StepResult(string Name, StepStatus Status, string? Reason = null);

/// <summary>
/// Status of a test step.
/// </summary>
public enum StepStatus
{
    Passed,
    Failed,
    Skipped
}
