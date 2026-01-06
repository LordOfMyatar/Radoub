// MDL ASCII Reader - Light, Emitter, Reference property parsing
// Partial class for non-mesh node type properties

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
