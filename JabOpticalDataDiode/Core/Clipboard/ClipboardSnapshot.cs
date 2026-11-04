using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JabOpticalDataDiode.Core.Clipboard
{
    public class ClipboardFormatData
    {
        public uint FormatId { get; set; }
        public string FormatName { get; set; } = string.Empty;
        public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    }

    public class ClipboardSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<ClipboardFormatData> Formats { get; set; } = new();

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.Write((byte)'J');
            writer.Write((byte)'A');
            writer.Write((byte)'B');
            writer.Write((byte)'C');
            writer.Write((byte)1); // Version 1

            writer.Write(Timestamp.ToBinary());
            writer.Write(Formats.Count);

            foreach (var item in Formats)
            {
                writer.Write(item.FormatId);
                writer.Write(item.FormatName ?? string.Empty);
                writer.Write(item.RawBytes.Length);
                writer.Write(item.RawBytes);
            }

            return ms.ToArray();
        }

        public static ClipboardSnapshot Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            if (reader.ReadByte() != 'J' ||
                reader.ReadByte() != 'A' ||
                reader.ReadByte() != 'B' ||
                reader.ReadByte() != 'C')
            {
                throw new InvalidDataException("잘못된 JAB 클립보드 바이너리 헤더입니다.");
            }

            byte version = reader.ReadByte();
            var snapshot = new ClipboardSnapshot
            {
                Timestamp = DateTime.FromBinary(reader.ReadInt64())
            };

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var formatId = reader.ReadUInt32();
                var formatName = reader.ReadString();
                var length = reader.ReadInt32();
                var rawBytes = reader.ReadBytes(length);

                snapshot.Formats.Add(new ClipboardFormatData
                {
                    FormatId = formatId,
                    FormatName = formatName,
                    RawBytes = rawBytes
                });
            }

            return snapshot;
        }
    }
}
