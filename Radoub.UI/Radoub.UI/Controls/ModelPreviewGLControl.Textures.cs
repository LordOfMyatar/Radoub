using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using Radoub.UI.Services;
using Radoub.Formats.Logging;
using Silk.NET.OpenGL;

namespace Radoub.UI.Controls;

/// <summary>
/// ModelPreviewGLControl partial: texture and PLT-color management — the public color/service
/// setters, cache invalidation, and the GPU texture load/upload pipeline (UpdateTextures /
/// UploadTexture). Split from the monolithic control (#2127); no behavior change. Shared texture
/// cache state lives in the main partial.
/// </summary>
public partial class ModelPreviewGLControl
{
    /// <summary>
    /// Set the texture service for loading textures.
    /// </summary>
    public void SetTextureService(TextureService textureService)
    {
        _textureService = textureService;
        _needsTextureUpdate = true;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Set character colors for PLT texture rendering.
    /// </summary>
    public void SetCharacterColors(int skinColor, int hairColor, int tattoo1Color, int tattoo2Color)
    {
        SetColorIndices(new PltColorIndices
        {
            Skin = skinColor,
            Hair = hairColor,
            Tattoo1 = tattoo1Color,
            Tattoo2 = tattoo2Color,
            Metal1 = _colorIndices.Metal1,
            Metal2 = _colorIndices.Metal2,
            Cloth1 = _colorIndices.Cloth1,
            Cloth2 = _colorIndices.Cloth2,
            Leather1 = _colorIndices.Leather1,
            Leather2 = _colorIndices.Leather2
        });
    }

    /// <summary>
    /// Set all PLT color indices (body + armor colors).
    /// </summary>
    public void SetColorIndices(PltColorIndices colorIndices)
    {
        if (!ColorsEqual(_colorIndices, colorIndices))
        {
            _colorIndices = colorIndices;
            // Only PLT textures depend on color indices. Flat TGA/DDS textures
            // are color-independent and can survive the change (#2058).
            InvalidatePltTextures();
            _needsTextureUpdate = true;
            RequestNextFrameRendering();
        }
    }

    private void InvalidatePltTextures()
    {
        if (_pltTextureNames.Count == 0) return;

        foreach (var name in _pltTextureNames)
        {
            if (_textureCache.TryGetValue(name, out var texId))
            {
                if (_gl != null && texId != 0)
                    _gl.DeleteTexture(texId);
                _textureCache.Remove(name);
                _textureAlphaProfile.Remove(name);
            }
        }
        _pltTextureNames.Clear();
    }

    /// <summary>
    /// Set armor colors for PLT texture rendering.
    /// </summary>
    public void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)
    {
        SetColorIndices(new PltColorIndices
        {
            Skin = _colorIndices.Skin,
            Hair = _colorIndices.Hair,
            Tattoo1 = _colorIndices.Tattoo1,
            Tattoo2 = _colorIndices.Tattoo2,
            Metal1 = metal1,
            Metal2 = metal2,
            Cloth1 = cloth1,
            Cloth2 = cloth2,
            Leather1 = leather1,
            Leather2 = leather2
        });
    }

    /// <summary>
    /// Clears the GPU texture cache so textures reload with current color indices.
    /// Also called on module switch to clear stale HAK textures (#1867).
    /// </summary>
    public void ClearTextureCache()
    {
        if (_gl != null)
        {
            foreach (var texId in _textureCache.Values)
                _gl.DeleteTexture(texId);
        }
        _textureCache.Clear();
        _textureAlphaProfile.Clear();
        _pltTextureNames.Clear();
    }

    private static bool ColorsEqual(PltColorIndices a, PltColorIndices b)
    {
        return a.Skin == b.Skin && a.Hair == b.Hair &&
               a.Tattoo1 == b.Tattoo1 && a.Tattoo2 == b.Tattoo2 &&
               a.Metal1 == b.Metal1 && a.Metal2 == b.Metal2 &&
               a.Cloth1 == b.Cloth1 && a.Cloth2 == b.Cloth2 &&
               a.Leather1 == b.Leather1 && a.Leather2 == b.Leather2;
    }

    private void UpdateTextures()
    {
        if (_gl == null || _textureService == null || _model == null) return;

        _textureRemapping.Clear();

        // Collect unique texture names from meshes, remembering the mesh's MTR material
        // name (#2497) so the loader can resolve diffuse from the .mtr texture0 when the
        // bare bitmap misses (the white-model case).
        var textureNames = new HashSet<string>();
        var materialByTexture = new Dictionary<string, string>();
        foreach (var mesh in _model.GetMeshNodes())
        {
            // Only consider meshes that are actually drawn. Render=false bone/internal
            // meshes carry bitmaps (often the model name, e.g. boar's hidden legs use
            // 'c_boar') that resolve to a base-game stub no visible surface uses —
            // loading them wasted work and falsely tripped the base-texture warning. (#2029)
            if (!mesh.Render || mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
                continue;
            if (string.IsNullOrEmpty(mesh.Bitmap))
                continue;
            var bitmap = mesh.Bitmap.ToLowerInvariant();
            textureNames.Add(bitmap);
            if (!string.IsNullOrEmpty(mesh.MaterialName) && !materialByTexture.ContainsKey(bitmap))
                materialByTexture[bitmap] = mesh.MaterialName.ToLowerInvariant();
        }

        // #2395: include emitter particle textures so they load into _textureCache
        // (the particle render path looks them up by Material.Texture).
        foreach (var (_, emitter, _) in _particleSystems)
        {
            var t = emitter.Material.Texture;
            if (!string.IsNullOrEmpty(t) && !t.Equals("null", StringComparison.OrdinalIgnoreCase))
                textureNames.Add(t.ToLowerInvariant());
        }

        // Delete old textures that are no longer needed
        var toRemove = new List<string>();
        foreach (var kvp in _textureCache)
        {
            if (!textureNames.Contains(kvp.Key))
            {
                _gl.DeleteTexture(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _textureCache.Remove(key);
            _textureAlphaProfile.Remove(key);
            _pltTextureNames.Remove(key);
        }

        // Load new textures
        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"UpdateTextures: {textureNames.Count} textures, colors: skin={_colorIndices.Skin}, hair={_colorIndices.Hair}, " +
            $"metal1={_colorIndices.Metal1}, metal2={_colorIndices.Metal2}, cloth1={_colorIndices.Cloth1}, cloth2={_colorIndices.Cloth2}");

        var failedTextures = new List<string>();
        foreach (var texName in textureNames)
        {
            if (_textureCache.ContainsKey(texName))
                continue; // Already loaded

            try
            {
                materialByTexture.TryGetValue(texName, out var materialName);
                var textureData = _preferBifTextures
                    ? _textureService.LoadTexturePreferBIFWithKind(texName, _colorIndices)
                    : _textureService.LoadTextureWithKind(texName, materialName, _colorIndices);
                if (textureData == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Texture '{texName}' returned null");
                    failedTextures.Add(texName);
                    continue;
                }

                var (width, height, pixels, isPlt) = textureData.Value;
                var texId = UploadTexture(width, height, pixels);
                if (texId != 0)
                {
                    _textureCache[texName] = texId;
                    // PLT skin (player/part-based heads, bodies) is opaque by definition (#2540): a
                    // PLT's "alpha" byte is a palette-LAYER index (skin/hair/tattoo selector), NOT
                    // transparency. The engine never treats it as transparent — and scanning the
                    // rendered RGBA would mis-read those palette-derived alphas as a cutout/blend,
                    // wrongly carving solid humanoid heads (the gnome-head regression). Force Opaque.
                    _textureAlphaProfile[texName] = isPlt
                        ? AlphaProfile.Opaque
                        : MeshTransparency.AnalyzeAlphaProfile(pixels, width, height);
                    if (isPlt) _pltTextureNames.Add(texName);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Loaded texture '{texName}' ({width}x{height}) -> texId={texId}, isPlt={isPlt}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load texture '{texName}': {ex.Message}");
                failedTextures.Add(texName);
            }
        }

        // For textures that failed to load, try model name as fallback.
        // NWN creature models often have meshes with bitmap set to node name (e.g. "torso_g")
        // instead of an actual texture. The real texture is typically the model name (e.g. "c_curst2").
        if (failedTextures.Count > 0)
        {
            var modelTexture = _model.Name?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(modelTexture) && !_textureCache.ContainsKey(modelTexture))
            {
                var fallbackData = _preferBifTextures
                    ? _textureService.LoadTexturePreferBIFWithKind(modelTexture, _colorIndices)
                    : _textureService.LoadTextureWithKind(modelTexture, _colorIndices);
                if (fallbackData != null)
                {
                    var (w, h, px, isPlt) = fallbackData.Value;
                    var fallbackId = UploadTexture(w, h, px);
                    if (fallbackId != 0)
                    {
                        _textureCache[modelTexture] = fallbackId;
                        // PLT skin alpha is a palette-layer index, never transparency (#2540) — force
                        // Opaque so a PLT body/head is never carved or blended (see primary site above).
                        _textureAlphaProfile[modelTexture] = isPlt
                            ? AlphaProfile.Opaque
                            : MeshTransparency.AnalyzeAlphaProfile(px, w, h);
                        if (isPlt) _pltTextureNames.Add(modelTexture);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"  Loaded model fallback texture '{modelTexture}' ({w}x{h}) -> texId={fallbackId}, isPlt={isPlt}");
                    }
                }
            }

            // Map failed textures to model texture if it loaded successfully
            if (!string.IsNullOrEmpty(modelTexture) && _textureCache.ContainsKey(modelTexture))
            {
                foreach (var failed in failedTextures)
                {
                    _textureRemapping[failed] = modelTexture;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"  Remapped texture '{failed}' -> '{modelTexture}' (model fallback)");
                }
            }
        }

        // #1758: warn only when a non-base model's actually-rendered texture resolves from
        // base-game BIF instead of its own pack. Computed here over the final rendered
        // texture set — NOT inside the load loop — so the result is deterministic regardless
        // of which textures were already cached from a previous model. The old in-loop check
        // skipped cached textures, making the warning flicker on/off across reloads. (#2029)
        bool usedBaseFallback = !_preferBifTextures
            && textureNames.Any(t => _textureService.ResolvesFromBase(t));

        if (Dispatcher.UIThread.CheckAccess())
            TextureSourceChanged?.Invoke(this, usedBaseFallback);
        else
            Dispatcher.UIThread.Post(() => TextureSourceChanged?.Invoke(this, usedBaseFallback));
    }

    private uint UploadTexture(int width, int height, byte[] rgba)
    {
        if (_gl == null) return 0;

        var texId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texId);

        // Upload RGBA data
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
            new ReadOnlySpan<byte>(rgba));

        // Set texture parameters
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        // Generate mipmaps for better quality at distance
        _gl.GenerateMipmap(TextureTarget.Texture2D);

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return texId;
    }
}
