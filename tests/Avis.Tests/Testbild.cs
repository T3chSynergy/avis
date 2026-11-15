using System.Buffers.Binary;

namespace Avis.Tests;

/// <summary>Nachgebaute Bilddateien für die Tests: der Anfang stimmt, der Rest ist aufgefüllt.</summary>
public static class Testbild
{
    /// <summary>Ein PNG mit diesen Abmessungen und dieser Dateigröße in Bytes.</summary>
    public static byte[] Png(int breite, int hoehe, int groesse)
    {
        var bild = new byte[Math.Max(groesse, 33)];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }.CopyTo(bild, 0);
        "IHDR"u8.CopyTo(bild.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bild.AsSpan(16), breite);
        BinaryPrimitives.WriteInt32BigEndian(bild.AsSpan(20), hoehe);
        return bild;
    }

    /// <summary>Ein GIF mit diesen Abmessungen.</summary>
    public static byte[] Gif(int breite, int hoehe, int groesse)
    {
        var bild = new byte[Math.Max(groesse, 13)];
        "GIF89a"u8.CopyTo(bild);
        BinaryPrimitives.WriteUInt16LittleEndian(bild.AsSpan(6), (ushort)breite);
        BinaryPrimitives.WriteUInt16LittleEndian(bild.AsSpan(8), (ushort)hoehe);
        return bild;
    }

    /// <summary>Ein JPEG mit diesen Abmessungen: erst ein Abschnitt mit Angaben der Kamera, dann der mit den Abmessungen.</summary>
    public static byte[] Jpeg(int breite, int hoehe, int groesse)
    {
        var bild = new byte[Math.Max(groesse, 64)];
        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }.CopyTo(bild, 0);     // Anfang, dann ein Abschnitt von 16 Bytes
        new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 }.CopyTo(bild, 20);          // Start of Frame
        BinaryPrimitives.WriteUInt16BigEndian(bild.AsSpan(25), (ushort)hoehe);
        BinaryPrimitives.WriteUInt16BigEndian(bild.AsSpan(27), (ushort)breite);
        return bild;
    }
}
