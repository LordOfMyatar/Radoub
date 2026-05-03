using Radoub.Formats.Mdl;

namespace ItemEditor.Services;

/// <summary>
/// Seam between <see cref="ItemPreviewController"/> and the live OpenGL preview control.
/// The controller owns the load/recolor/debounce decisions; the renderer just executes.
/// A fake implementation is used for unit tests; the real production adapter wraps
/// <c>Radoub.UI.Controls.ModelPreviewGLControl</c>.
/// </summary>
public interface IItemPreviewRenderer
{
    /// <summary>Hand the renderer a composed model to display.</summary>
    void SetModel(MdlModel model);

    /// <summary>Hide the model and show the "No 3D model" placeholder.</summary>
    void Clear();

    /// <summary>
    /// Apply PLT colors. The controller passes 0 for any color category that does not
    /// apply to the current ModelType (e.g. layered items only use Cloth1/2).
    /// </summary>
    void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2);
}
