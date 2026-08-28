using System.Text;

namespace NEXA.Common;

/// <summary>
/// Utility sanitizer converting strings into safe 7-bit printable ASCII characters before passing them to OpenCV Hershey text rendering.
/// <para>
/// <b>What it is:</b> A robust ASCII normalizer protecting OpenCvSharp from P/Invoke marshaling exceptions.
/// </para>
/// <para>
/// <b>What it does:</b> Replaces German umlauts (ä, ö, ü, ß), typographical dashes (–, —), smart quotes, and unmappable Unicode glyphs with clean ASCII equivalents.
/// </para>
/// <para>
/// <b>Why it is used:</b> Prevents "System.ArgumentException: Cannot marshal: Encountered unmappable character" crashes when rendering window titles containing Unicode characters.
/// </para>
/// </summary>
public static class TextSanitizer
{
    /// <summary>
    /// Converts a string into safe 7-bit printable ASCII text safe for <see cref="OpenCvSharp.Cv2.PutText"/>.
    /// </summary>
    /// <param name="input">The raw text string.</param>
    /// <returns>A sanitized ASCII string.</returns>
    public static string ToSafeAscii(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        StringBuilder sb = new(input.Length);
        foreach (char c in input)
        {
            if (c == 'ä' || c == 'Ä')
            {
                sb.Append("ae");
            }
            else if (c == 'ö' || c == 'Ö')
            {
                sb.Append("oe");
            }
            else if (c == 'ü' || c == 'Ü')
            {
                sb.Append("ue");
            }
            else if (c == 'ß')
            {
                sb.Append("ss");
            }
            else if (c == '–' || c == '—')
            {
                sb.Append('-');
            }
            else if (c == '“' || c == '”' || c == '„')
            {
                sb.Append('"');
            }
            else if (c == '‘' || c == '’')
            {
                sb.Append('\'');
            }
            else if (c == '°')
            {
                sb.Append(" deg");
            }
            else if (c >= 32 && c <= 126)
            {
                sb.Append(c);
            }
            else if (c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append('?');
            }
        }

        return sb.ToString();
    }
}
