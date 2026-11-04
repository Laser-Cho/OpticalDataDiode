using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpticalDataDiode.Core.Clipboard
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

        /// <summary>
        /// 전체 스냅샷을 압축 가능한 단일 바이트 배열로 직렬화합니다.
        /// </summary>
        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // 매직 헤더 및 버전
            writer.Write((byte)'O');
            writer.Write((byte)'D');
            writer.Write((byte)'C');
            writer.Write((byte)'B');
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

        /// <summary>
        /// 바이트 배열로부터 스냅샷을 역직렬화합니다.
        /// </summary>
        public static ClipboardSnapshot Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            if (reader.ReadByte() != 'O' ||
                reader.ReadByte() != 'D' ||
                reader.ReadByte() != 'C' ||
                reader.ReadByte() != 'B')
            {
                throw new InvalidDataException("잘못된 클립보드 스냅샷 바이너리 헤더입니다.");
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
