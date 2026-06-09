// MDL ASCII Reader - Light, Emitter, Reference property parsing
// Partial class for non-mesh node type properties

using System.Numerics;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
    private void ParseLightProperty(MdlLightNode light, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "color":
                if (tokens.Length >= 4)
                    light.Color = new System.Numerics.Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "radius":
                if (tokens.Length >= 2)
                    light.Radius = ParseFloat(tokens[1]);
                break;

            case "multiplier":
                if (tokens.Length >= 2)
                    light.Multiplier = ParseFloat(tokens[1]);
                break;

            case "isdynamic":
                if (tokens.Length >= 2)
                    light.IsDynamic = ParseInt(tokens[1]) != 0;
                break;

            case "affectdynamic":
                if (tokens.Length >= 2)
                    light.AffectDynamic = ParseInt(tokens[1]) != 0;
                break;

            case "shadow":
                if (tokens.Length >= 2)
                    light.Shadow = ParseInt(tokens[1]) != 0;
                break;

            case "flareradius":
                if (tokens.Length >= 2)
                    light.FlareRadius = ParseFloat(tokens[1]);
                break;

            case "priority":
                if (tokens.Length >= 2)
                    light.Priority = ParseInt(tokens[1]);
                break;

            case "ambientonly":
                if (tokens.Length >= 2)
                    light.AmbientOnly = ParseInt(tokens[1]) != 0;
                break;

            case "fading":
                if (tokens.Length >= 2)
                    light.Fading = ParseInt(tokens[1]) != 0;
                break;
        }
    }

    private void ParseEmitterProperty(MdlEmitterNode emitter, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "update":
                if (tokens.Length >= 2)
                    emitter.Update = tokens[1];
                break;

            case "render":
                if (tokens.Length >= 2)
                    emitter.RenderMethod = tokens[1];
                break;

            case "blend":
                if (tokens.Length >= 2)
                    emitter.Blend = tokens[1];
                break;

            case "texture":
                if (tokens.Length >= 2)
                    emitter.Texture = tokens[1];
                break;

            case "spawntype":
                if (tokens.Length >= 2)
                    emitter.SpawnType = tokens[1];
                break;

            case "xgrid":
                if (tokens.Length >= 2)
                    emitter.XGrid = ParseInt(tokens[1]);
                break;

            case "ygrid":
                if (tokens.Length >= 2)
                    emitter.YGrid = ParseInt(tokens[1]);
                break;

            case "renderorder":
            case "render_order":
                if (tokens.Length >= 2)
                    emitter.RenderOrder = ParseInt(tokens[1]);
                break;

            case "inherit":
                if (tokens.Length >= 2)
                    emitter.Inherit = ParseInt(tokens[1]) != 0;
                break;

            case "inheritlocal":
                if (tokens.Length >= 2)
                    emitter.InheritLocal = ParseInt(tokens[1]) != 0;
                break;

            case "inheritpart":
                if (tokens.Length >= 2)
                    emitter.InheritPart = ParseInt(tokens[1]) != 0;
                break;

            case "affectedbywind":
                if (tokens.Length >= 2)
                    emitter.AffectedByWind = ParseInt(tokens[1]) != 0;
                break;

            case "m_issplat":
            case "issplat":
                if (tokens.Length >= 2)
                    emitter.IsSplat = ParseInt(tokens[1]) != 0;
                break;

            case "bounce":
                if (tokens.Length >= 2)
                    emitter.Bounce = ParseInt(tokens[1]) != 0;
                break;

            case "random":
                if (tokens.Length >= 2)
                    emitter.Random = ParseInt(tokens[1]) != 0;
                break;

            case "loop":
                if (tokens.Length >= 2)
                    emitter.Loop = ParseInt(tokens[1]) != 0;
                break;

            case "p2p":
                if (tokens.Length >= 2)
                    emitter.P2P = ParseInt(tokens[1]) != 0;
                break;

            case "p2p_bezier":
                if (tokens.Length >= 2)
                    emitter.P2PBezier = ParseInt(tokens[1]) != 0;
                break;

            // ---- Numeric controller props (#2395) ----
            case "birthrate":
                if (tokens.Length >= 2) emitter.BirthRate = ParseFloat(tokens[1]);
                break;

            case "lifeexp":
                if (tokens.Length >= 2) emitter.LifeExp = ParseFloat(tokens[1]);
                break;

            case "velocity":
                if (tokens.Length >= 2) emitter.Velocity = ParseFloat(tokens[1]);
                break;

            case "randvel":
                if (tokens.Length >= 2) emitter.RandVel = ParseFloat(tokens[1]);
                break;

            case "spread":
                if (tokens.Length >= 2) emitter.Spread = ParseFloat(tokens[1]);
                break;

            case "mass":
                if (tokens.Length >= 2) emitter.Mass = ParseFloat(tokens[1]);
                break;

            case "grav":
                if (tokens.Length >= 2) emitter.Grav = ParseFloat(tokens[1]);
                break;

            case "drag":
                if (tokens.Length >= 2) emitter.Drag = ParseFloat(tokens[1]);
                break;

            case "particlerot":
                if (tokens.Length >= 2) emitter.ParticleRot = ParseFloat(tokens[1]);
                break;

            case "fps":
                if (tokens.Length >= 2) emitter.Fps = ParseFloat(tokens[1]);
                break;

            case "framestart":
                if (tokens.Length >= 2)
                    emitter.FrameStart = (int)System.MathF.Round(ParseFloat(tokens[1]));
                break;

            case "frameend":
                if (tokens.Length >= 2)
                    emitter.FrameEnd = (int)System.MathF.Round(ParseFloat(tokens[1]));
                break;

            case "sizestart":
                if (tokens.Length >= 2) emitter.SizeStart = ParseFloat(tokens[1]);
                break;

            case "sizeend":
                if (tokens.Length >= 2) emitter.SizeEnd = ParseFloat(tokens[1]);
                break;

            case "sizestart_y":
                if (tokens.Length >= 2) emitter.SizeStartY = ParseFloat(tokens[1]);
                break;

            case "sizeend_y":
                if (tokens.Length >= 2) emitter.SizeEndY = ParseFloat(tokens[1]);
                break;

            case "sizemid":
                if (tokens.Length >= 2) { emitter.SizeMid = ParseFloat(tokens[1]); emitter.HasSizeMid = true; }
                break;

            case "sizemid_y":
                if (tokens.Length >= 2) emitter.SizeMidY = ParseFloat(tokens[1]);
                break;

            case "colorstart":
                if (tokens.Length >= 4)
                    emitter.ColorStart = new Vector3(
                        ParseFloat(tokens[1]), ParseFloat(tokens[2]), ParseFloat(tokens[3]));
                break;

            case "colorend":
                if (tokens.Length >= 4)
                    emitter.ColorEnd = new Vector3(
                        ParseFloat(tokens[1]), ParseFloat(tokens[2]), ParseFloat(tokens[3]));
                break;

            case "colormid":
                if (tokens.Length >= 4)
                {
                    emitter.ColorMid = new Vector3(
                        ParseFloat(tokens[1]), ParseFloat(tokens[2]), ParseFloat(tokens[3]));
                    emitter.HasColorMid = true;
                }
                break;

            case "alphastart":
                if (tokens.Length >= 2) emitter.AlphaStart = ParseFloat(tokens[1]);
                break;

            case "alphaend":
                if (tokens.Length >= 2) emitter.AlphaEnd = ParseFloat(tokens[1]);
                break;

            case "alphamid":
                if (tokens.Length >= 2) { emitter.AlphaMid = ParseFloat(tokens[1]); emitter.HasAlphaMid = true; }
                break;

            case "percentstart":
                if (tokens.Length >= 2) emitter.PercentStart = ParseFloat(tokens[1]);
                break;

            case "percentmid":
                if (tokens.Length >= 2) emitter.PercentMid = ParseFloat(tokens[1]);
                break;

            case "percentend":
                if (tokens.Length >= 2) emitter.PercentEnd = ParseFloat(tokens[1]);
                break;
        }
    }

    private void ParseReferenceProperty(MdlReferenceNode reference, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "refmodel":
                if (tokens.Length >= 2)
                    reference.RefModel = tokens[1];
                break;

            case "reattachable":
                if (tokens.Length >= 2)
                    reference.Reattachable = ParseInt(tokens[1]) != 0;
                break;
        }
    }
}
