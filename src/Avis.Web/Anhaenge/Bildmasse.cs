using System.Buffers.Binary;

namespace Avis.Web.Anhaenge;

/// <summary>
/// Liest Breite und Höhe eines Bildes aus dem Anfang der Datei, ohne es zu
/// entpacken. Gebraucht wird das, um im Text einer Mail ein Logo von einem
/// Bildschirmfoto zu unterscheiden: Beide können als Datei klein sein, ein
/// Logo ist es auch in seinen Abmessungen.
/// </summary>
public static class Bildmasse
{
    /// <summary>Breite und Höhe in Bildpunkten, oder null, wenn sie sich nicht lesen lassen (anderes Format, beschädigte Datei).</summary>
    public static (int Breite, int Hoehe)? Lesen(ReadOnlySpan<byte> b)
    {
        // PNG: Nach der Kennung folgt immer zuerst der Abschnitt IHDR mit Breite und Höhe.
        if (b.Length >= 24 && b.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) && b[12..16].SequenceEqual("IHDR"u8))
            return Gueltig(BinaryPrimitives.ReadInt32BigEndian(b[16..]), BinaryPrimitives.ReadInt32BigEndian(b[20..]));

        if (b.Length >= 10 && (b.StartsWith("GIF87a"u8) || b.StartsWith("GIF89a"u8)))
            return Gueltig(BinaryPrimitives.ReadUInt16LittleEndian(b[6..]), BinaryPrimitives.ReadUInt16LittleEndian(b[8..]));

        if (b.StartsWith<byte>([0xFF, 0xD8]))
            return Jpeg(b);

        return null;
    }

    /// <summary>
    /// JPEG besteht aus Abschnitten, jeder mit einer Kennung und seiner Länge.
    /// Die Abmessungen stehen im ersten Abschnitt der Art "Start of Frame".
    /// </summary>
    private static (int, int)? Jpeg(ReadOnlySpan<byte> b)
    {
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF)
                return null;
            var art = b[i + 1];
            if (art == 0xFF)
            {
                i++; // Füllbyte
                continue;
            }

            // Start of Frame: C0 bis CF, außer C4, C8 und CC, die etwas anderes bedeuten.
            if (art is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                return Gueltig(BinaryPrimitives.ReadUInt16BigEndian(b[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(b[(i + 5)..]));

            // Abschnitte ohne Länge.
            if (art is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                i += 2;
                continue;
            }

            i += 2 + BinaryPrimitives.ReadUInt16BigEndian(b[(i + 2)..]);
        }
        return null;
    }

    private static (int, int)? Gueltig(int breite, int hoehe) => breite > 0 && hoehe > 0 ? (breite, hoehe) : null;
}
