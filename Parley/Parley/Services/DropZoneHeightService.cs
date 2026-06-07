namespace DialogEditor.Services
{
    /// <summary>
    /// Resolves the height the TreeView drop-zone calculation should run against (#2382).
    ///
    /// An expanded TreeViewItem's Bounds.Height includes its entire child subtree, so the
    /// header row ends up inside the top-20% "Before" zone of a tall item — which rejected
    /// valid same-type Before/Into drops onto expanded threads. The zone math must use the
    /// header-row height instead. This helper picks the header height when it is a sane
    /// measurement and falls back to the full item height otherwise.
    /// </summary>
    public static class DropZoneHeightService
    {
        /// <summary>
        /// Returns the height to use for drop-zone calculation: the header height when it is
        /// a valid, non-larger measurement than the full item; otherwise the full item height.
        /// </summary>
        public static double ResolveZoneHeight(double fullItemHeight, double? headerHeight)
        {
            if (headerHeight is double h && h > 0 && h <= fullItemHeight)
                return h;

            return fullItemHeight;
        }
    }
}
