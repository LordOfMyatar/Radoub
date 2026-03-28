using System;
using Radoub.Formats.Logging;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for exporting flowchart visualizations to PNG format.
    /// </summary>
    public static class FlowchartExportService
    {
        /// <summary>
        /// Exports a control to PNG format.
        /// </summary>
        /// <param name="control">The control to export</param>
        /// <param name="filePath">The destination file path</param>
        /// <param name="dpi">The DPI for the export (default 96)</param>
        /// <returns>True if export succeeded</returns>
        public static async Task<bool> ExportToPngAsync(Control control, string filePath, double dpi = 96)
        {
            try
            {
                if (control == null || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot export: control has no size");
                    return false;
                }

                // Calculate pixel size based on DPI
                var scale = dpi / 96.0;
                var pixelWidth = (int)(control.Bounds.Width * scale);
                var pixelHeight = (int)(control.Bounds.Height * scale);

                // Create render target
                var pixelSize = new PixelSize(pixelWidth, pixelHeight);
                var renderBitmap = new RenderTargetBitmap(pixelSize, new Vector(dpi, dpi));

                // Render the control
                renderBitmap.Render(control);

                // Save to file
                await Task.Run(() =>
                {
                    using (var stream = File.Create(filePath))
                    {
                        renderBitmap.Save(stream);
                    }
                });

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Flowchart exported to PNG: {UnifiedLogger.SanitizePath(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"PNG export failed: {ex.Message}");
                return false;
            }
        }
    }
}
