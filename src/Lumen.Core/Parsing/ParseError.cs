using System.Globalization;

namespace Lumen.Core.Parsing;

public readonly record struct ParseError(string Message, int Line, int Column)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"[line {Line}:{Column}] {Message}");
}
