namespace DialogEditor.Services
{
    /// <summary>
    /// Clamps the side-by-side flowchart panel width to a usable, on-screen range.
    /// Self-heals corrupted settings that would otherwise leave the panel off-screen (#2049).
    /// </summary>
    public static class PanelWidthClamper
    {
        private const double HardMinWidth = 200;
        private const double MinLeftPanelReserve = 400;
        private const double ResetLowerThreshold = 100;
        private const double ResetUpperFraction = 0.95;
        private const double ResetDefaultFraction = 0.50;

        /// <summary>
        /// Returns a safe panel width for the given window width.
        /// Reset path: if storedWidth is outside [ResetLowerThreshold, 0.95 * windowWidth],
        /// replace with 0.50 * windowWidth. Then clamp to [HardMinWidth, windowWidth - MinLeftPanelReserve].
        /// </summary>
        public static double Clamp(double storedWidth, double windowWidth)
        {
            var width = storedWidth;

            if (width < ResetLowerThreshold || width > windowWidth * ResetUpperFraction)
            {
                width = windowWidth * ResetDefaultFraction;
            }

            var hardMax = windowWidth - MinLeftPanelReserve;
            if (width > hardMax) width = hardMax;
            if (width < HardMinWidth) width = HardMinWidth;

            return width;
        }
    }
}
