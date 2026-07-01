// MDL ASCII Reader - Animation parsing
// Partial class for animation data parsing

using System.Collections.Generic;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
    private MdlAnimation? ParseAnimation(string animName)
    {
        var anim = new MdlAnimation { Name = animName };

        // Animation nodes form a flat list with "parent" properties (same shape as geometry).
        // Each carries keyframe controllers (positionkey/orientationkey/scalekey). Previously the
        // ASCII reader skipped these (SkipToEndNode), so every animation parsed with a null
        // GeometryRoot and the preview could never build a pose — canine_a / blinkdog never
        // animated (#2552). Reuse the geometry node machinery so the controllers land on the tree.
        var nodesByName = new Dictionary<string, MdlNode>(System.StringComparer.OrdinalIgnoreCase);
        var parentNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        while (_currentLine < _lines.Length)
        {
            var line = CurrentLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                _currentLine++;
                continue;
            }

            var tokens = Tokenize(line);
            if (tokens.Length == 0)
            {
                _currentLine++;
                continue;
            }

            switch (tokens[0].ToLowerInvariant())
            {
                case "doneanim":
                    _currentLine++;
                    anim.GeometryRoot = LinkNodeHierarchy(nodesByName, parentNames);
                    return anim;

                case "length":
                    if (tokens.Length >= 2)
                        anim.Length = ParseFloat(tokens[1]);
                    _currentLine++;
                    break;

                case "transtime":
                    if (tokens.Length >= 2)
                        anim.TransitionTime = ParseFloat(tokens[1]);
                    _currentLine++;
                    break;

                case "animroot":
                    if (tokens.Length >= 2)
                        anim.AnimRoot = tokens[1];
                    _currentLine++;
                    break;

                case "event":
                    if (tokens.Length >= 3)
                    {
                        anim.Events.Add(new MdlAnimationEvent
                        {
                            Time = ParseFloat(tokens[1]),
                            EventName = tokens[2]
                        });
                    }
                    _currentLine++;
                    break;

                case "node":
                    if (tokens.Length >= 3)
                    {
                        var (node, parentName) = ParseNodeWithParent(tokens[1], tokens[2]);
                        nodesByName[node.Name] = node;
                        if (!string.IsNullOrEmpty(parentName) &&
                            !parentName.Equals("NULL", System.StringComparison.OrdinalIgnoreCase))
                        {
                            parentNames[node.Name] = parentName;
                        }
                    }
                    else
                    {
                        _currentLine++;
                    }
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        anim.GeometryRoot = LinkNodeHierarchy(nodesByName, parentNames);
        return anim;
    }
}
