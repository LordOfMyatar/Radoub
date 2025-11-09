using System;
using System.Threading.Tasks;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for debounce functionality (Issue #76)
    /// Validates that rapid operations are correctly throttled to prevent focus misplacement
    /// </summary>
    public class DebounceTests
    {
        /// <summary>
        /// Simulates the debounce pattern used in MainWindow.OnAddSmartNodeClick
        /// Tests that rapid calls within debounce window are rejected
        /// </summary>
        [Fact]
        public void Debounce_RejectsRapidCalls_WithinDebounceWindow()
        {
            // Arrange: Simulate debounce pattern (same as MainWindow)
            const int DEBOUNCE_MS = 150;
            DateTime lastOperationTime = DateTime.MinValue;
            int successfulOperations = 0;

            // Act: Simulate 5 rapid calls (no delay between them)
            for (int i = 0; i < 5; i++)
            {
                var timeSinceLastOp = (DateTime.Now - lastOperationTime).TotalMilliseconds;

                if (timeSinceLastOp >= DEBOUNCE_MS)
                {
                    // Operation allowed
                    lastOperationTime = DateTime.Now;
                    successfulOperations++;
                }
                // else: Operation debounced (rejected)
            }

            // Assert: Only 1 operation should succeed (first one)
            // Subsequent rapid calls should be debounced
            Assert.Equal(1, successfulOperations);
        }

        [Fact]
        public async Task Debounce_AllowsCalls_AfterDebounceWindow()
        {
            // Arrange
            const int DEBOUNCE_MS = 150;
            DateTime lastOperationTime = DateTime.MinValue;
            int successfulOperations = 0;

            // Act: Simulate 3 calls with proper delay between them
            for (int i = 0; i < 3; i++)
            {
                var timeSinceLastOp = (DateTime.Now - lastOperationTime).TotalMilliseconds;

                if (timeSinceLastOp >= DEBOUNCE_MS)
                {
                    lastOperationTime = DateTime.Now;
                    successfulOperations++;
                }

                // Wait longer than debounce window before next call
                await Task.Delay(DEBOUNCE_MS + 10);
            }

            // Assert: All 3 operations should succeed
            Assert.Equal(3, successfulOperations);
        }

        [Fact]
        public async Task Debounce_ThrottlesRapidBurst_AllowsSubsequentCalls()
        {
            // Arrange: Test realistic scenario - rapid burst followed by normal usage
            const int DEBOUNCE_MS = 150;
            DateTime lastOperationTime = DateTime.MinValue;
            int successfulOperations = 0;

            // Act: Rapid burst (5 rapid calls)
            for (int i = 0; i < 5; i++)
            {
                var timeSinceLastOp = (DateTime.Now - lastOperationTime).TotalMilliseconds;
                if (timeSinceLastOp >= DEBOUNCE_MS)
                {
                    lastOperationTime = DateTime.Now;
                    successfulOperations++;
                }
            }

            // Wait for debounce window to expire
            await Task.Delay(DEBOUNCE_MS + 10);

            // Normal call after burst
            var timeSinceLastOp2 = (DateTime.Now - lastOperationTime).TotalMilliseconds;
            if (timeSinceLastOp2 >= DEBOUNCE_MS)
            {
                lastOperationTime = DateTime.Now;
                successfulOperations++;
            }

            // Assert: 1 from rapid burst + 1 normal call = 2 total
            Assert.Equal(2, successfulOperations);
        }

        [Theory]
        [InlineData(50)]   // Very rapid (50ms apart)
        [InlineData(100)]  // Rapid (100ms apart)
        [InlineData(120)]  // Well below threshold (accounting for Task.Delay variance)
        public async Task Debounce_RejectsCallsBelowThreshold(int delayMs)
        {
            // Arrange
            const int DEBOUNCE_MS = 150;
            DateTime lastOperationTime = DateTime.MinValue;
            int successfulOperations = 0;

            // Act: First call (always succeeds)
            lastOperationTime = DateTime.Now;
            successfulOperations++;

            // Wait less than debounce window
            await Task.Delay(delayMs);

            // Second call (should be rejected)
            var timeSinceLastOp = (DateTime.Now - lastOperationTime).TotalMilliseconds;
            if (timeSinceLastOp >= DEBOUNCE_MS)
            {
                lastOperationTime = DateTime.Now;
                successfulOperations++;
            }

            // Assert: Only first call succeeded
            Assert.Equal(1, successfulOperations);
        }

        [Theory]
        [InlineData(150)]  // Exactly at threshold
        [InlineData(160)]  // Just above threshold
        [InlineData(200)]  // Well above threshold
        public async Task Debounce_AllowsCallsAtOrAboveThreshold(int delayMs)
        {
            // Arrange
            const int DEBOUNCE_MS = 150;
            DateTime lastOperationTime = DateTime.MinValue;
            int successfulOperations = 0;

            // Act: First call
            lastOperationTime = DateTime.Now;
            successfulOperations++;

            // Wait at or above debounce window
            await Task.Delay(delayMs);

            // Second call (should succeed)
            var timeSinceLastOp = (DateTime.Now - lastOperationTime).TotalMilliseconds;
            if (timeSinceLastOp >= DEBOUNCE_MS)
            {
                lastOperationTime = DateTime.Now;
                successfulOperations++;
            }

            // Assert: Both calls succeeded
            Assert.Equal(2, successfulOperations);
        }
    }
}
