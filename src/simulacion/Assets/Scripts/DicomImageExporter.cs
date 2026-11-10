using System;
using System.IO;
using System.Numerics;
using System.Text;
using UnityEngine;

// Exportador educativo: DICOM Part 10, Secondary Capture, RGB sin compresion.
public static class DicomImageExporter
{
    private const string SopClass = "1.2.840.10008.5.1.4.1.1.7";
    private const string TransferSyntax = "1.2.840.10008.1.2.1";
    private const string ImplementationClass = "2.25.33090707098715294251218999106250481619";

    public static void Save(Texture2D texture, string path)
    {
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        int width = texture.width, height = texture.height;
        if (width > ushort.MaxValue || height > ushort.MaxValue)
            throw new ArgumentOutOfRangeException("Las dimensiones exceden el limite de este exportador.");

        // DICOM ordena las filas de arriba hacia abajo. Unity GetPixels32 empieza abajo.
        Color32[] pixels = texture.GetPixels32();
        byte[] rgb = new byte[width * height * 3];
        int index = 0;
        for (int y = height - 1; y >= 0; y--)
            for (int x = 0; x < width; x++)
            {
                Color32 p = pixels[y * width + x];
                rgb[index++] = p.r;
                rgb[index++] = p.g;
                rgb[index++] = p.b;
            }

        string sopUid = NewUid(), studyUid = NewUid(), seriesUid = NewUid();
        DateTime now = DateTime.Now;
        string date = now.ToString("yyyyMMdd"), time = now.ToString("HHmmss.ffffff");
        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII))
        {
            writer.Write(new byte[128]);
            writer.Write(Encoding.ASCII.GetBytes("DICM"));

            // File Meta Information (grupo 0002), siempre Explicit VR Little Endian.
            byte[] meta;
            using (var ms = new MemoryStream())
            using (var mw = new BinaryWriter(ms, Encoding.ASCII, true))
            {
                WriteElement(mw, 0x0002, 0x0001, "OB", new byte[] { 0, 1 });
                WriteText(mw, 0x0002, 0x0002, "UI", SopClass);
                WriteText(mw, 0x0002, 0x0003, "UI", sopUid);
                WriteText(mw, 0x0002, 0x0010, "UI", TransferSyntax);
                WriteText(mw, 0x0002, 0x0012, "UI", ImplementationClass);
                WriteText(mw, 0x0002, 0x0013, "SH", "SIMSENHANCE_1");
                mw.Flush();
                meta = ms.ToArray();
            }
            WriteUL(writer, 0x0002, 0x0000, (uint)meta.Length);
            writer.Write(meta);

            // Dataset (etiquetas en orden creciente).
            WriteText(writer, 0x0008, 0x0016, "UI", SopClass);
            WriteText(writer, 0x0008, 0x0018, "UI", sopUid);
            WriteText(writer, 0x0008, 0x0020, "DA", date);
            WriteText(writer, 0x0008, 0x0021, "DA", date);
            WriteText(writer, 0x0008, 0x0022, "DA", date);
            WriteText(writer, 0x0008, 0x0023, "DA", date);
            WriteText(writer, 0x0008, 0x0030, "TM", time);
            WriteText(writer, 0x0008, 0x0031, "TM", time);
            WriteText(writer, 0x0008, 0x0032, "TM", time);
            WriteText(writer, 0x0008, 0x0033, "TM", time);
            WriteText(writer, 0x0008, 0x0060, "CS", "OT");
            WriteText(writer, 0x0008, 0x0064, "CS", "WSD");
            WriteText(writer, 0x0008, 0x0070, "LO", "SimulatorSenhance");
            WriteText(writer, 0x0008, 0x1030, "LO", "Simulacion quirurgica laparoscopica");
            WriteText(writer, 0x0008, 0x103E, "LO", "Captura de camara laparoscopica");
            // Datos del paciente ficticio
            WriteText(writer, 0x0010, 0x0010, "PN", "SIMULADO^PACIENTE");
            WriteText(writer, 0x0010, 0x0020, "LO", "SIM-001");
            WriteText(writer, 0x0010, 0x0040, "CS", "M");
            WriteText(writer, 0x0010, 0x1010, "AS", "035Y");

            WriteText(writer, 0x0020, 0x000D, "UI", studyUid);
            WriteText(writer, 0x0020, 0x000E, "UI", seriesUid);
            WriteText(writer, 0x0020, 0x0011, "IS", "1");
            WriteText(writer, 0x0020, 0x0013, "IS", "1");
            WriteUS(writer, 0x0028, 0x0002, 3);
            WriteText(writer, 0x0028, 0x0004, "CS", "RGB");
            WriteUS(writer, 0x0028, 0x0006, 0);
            WriteUS(writer, 0x0028, 0x0010, (ushort)height);
            WriteUS(writer, 0x0028, 0x0011, (ushort)width);
            WriteUS(writer, 0x0028, 0x0100, 8);
            WriteUS(writer, 0x0028, 0x0101, 8);
            WriteUS(writer, 0x0028, 0x0102, 7);
            WriteUS(writer, 0x0028, 0x0103, 0);
            WriteElement(writer, 0x7FE0, 0x0010, "OB", rgb);
        }
    }

    private static string NewUid()
    {
        byte[] bytes = new byte[17];
        Array.Copy(Guid.NewGuid().ToByteArray(), bytes, 16);
        return "2.25." + new BigInteger(bytes).ToString();
    }

    private static void WriteText(BinaryWriter w, ushort group, ushort element, string vr, string value)
    {
        byte[] data = Encoding.ASCII.GetBytes(value);
        if (data.Length % 2 != 0)
        {
            Array.Resize(ref data, data.Length + 1);
            data[data.Length - 1] = vr == "UI" ? (byte)0 : (byte)' ';
        }
        WriteElement(w, group, element, vr, data);
    }

    private static void WriteUS(BinaryWriter w, ushort group, ushort element, ushort value)
    {
        WriteElement(w, group, element, "US", BitConverter.GetBytes(value));
    }

    private static void WriteUL(BinaryWriter w, ushort group, ushort element, uint value)
    {
        WriteElement(w, group, element, "UL", BitConverter.GetBytes(value));
    }

    private static void WriteElement(BinaryWriter w, ushort group, ushort element, string vr, byte[] value)
    {
        w.Write(group);
        w.Write(element);
        w.Write(Encoding.ASCII.GetBytes(vr));
        bool longVr = vr == "OB" || vr == "OW" || vr == "SQ" || vr == "UN" || vr == "UT";
        uint paddedLength = (uint)(value.Length + (value.Length & 1));
        if (longVr)
        {
            w.Write((ushort)0);
            w.Write(paddedLength);
        }
        else
        {
            if (paddedLength > ushort.MaxValue) throw new InvalidOperationException("Elemento DICOM demasiado largo: " + vr);
            w.Write((ushort)paddedLength);
        }
        w.Write(value);
        if ((value.Length & 1) != 0) w.Write((byte)0);
    }
}
