// MDL ASCII Reader - Animation parsing
// Partial class for animation data parsing

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
    private MdlAnimation? ParseAnimation(string animName)
    {
        var anim = new MdlAnimation { Name = animName };

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
                    // Animation nodes - simplified handling
                    _currentLine++;
                    SkipToEndNode();
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        return anim;
    }

    private void SkipToEndNode()
    {
        int depth = 1;
        while (_currentLine < _lines.Length && depth > 0)
        {
            var line = CurrentLine().Trim();
            if (line.StartsWith("node ", StringComparison.OrdinalIgnoreCase))
                depth++;
            else if (line.Equals("endnode", StringComparison.OrdinalIgnoreCase))
                depth--;
            _currentLine++;
        }
    }
}
