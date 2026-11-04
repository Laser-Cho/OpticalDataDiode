using System;
using System.IO;

namespace JabOpticalDataDiode.Core.Fec
{
    public class FecPacket
    {
        public const byte MAGIC_BYTE = 0xEA;

        public ushort SessionId { get; set; }
        public ushort TotalSourceBlocks { get; set; } // K
        public ushort BlockSize { get; set; }         // Symbol Size
        public int TotalDataLength { get; set; }
        public uint SequenceSeed { get; set; }        // ESI
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(MAGIC_BYTE);
            writer.Write(SessionId);
            writer.Write(TotalSourceBlocks);
            writer.Write(BlockSize);
            writer.Write(TotalDataLength);
            writer.Write(SequenceSeed);
            writer.Write(Payload.Length);
            writer.Write(Payload);

            byte[] raw = ms.ToArray();
            uint crc = ComputeCrc32(raw, 0, raw.Length);

            byte[] packetWithCrc = new byte[raw.Length + 4];
            Buffer.BlockCopy(raw, 0, packetWithCrc, 0, raw.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(crc), 0, packetWithCrc, raw.Length, 4);

            return packetWithCrc;
        }

        public static bool TryDeserialize(byte[] data, out FecPacket? packet)
        {
            packet = null;
            if (data == null || data.Length < 21)
                return false;

            int payloadAndHeaderLen = data.Length - 4;
            uint expectedCrc = BitConverter.ToUInt32(data, payloadAndHeaderLen);
            uint actualCrc = ComputeCrc32(data, 0, payloadAndHeaderLen);

            if (expectedCrc != actualCrc)
                return false;

            using var ms = new MemoryStream(data, 0, payloadAndHeaderLen);
            using var reader = new BinaryReader(ms);

            if (reader.ReadByte() != MAGIC_BYTE)
                return false;

            packet = new FecPacket
            {
                SessionId = reader.ReadUInt16(),
                TotalSourceBlocks = reader.ReadUInt16(),
                BlockSize = reader.ReadUInt16(),
                TotalDataLength = reader.ReadInt32(),
                SequenceSeed = reader.ReadUInt32()
            };

            int payloadLen = reader.ReadInt32();
            if (payloadLen > 0 && ms.Position + payloadLen <= ms.Length)
            {
                packet.Payload = reader.ReadBytes(payloadLen);
                return true;
            }

            return false;
        }

        public static uint ComputeCrc32(byte[] buffer, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                byte b = buffer[i];
                crc ^= b;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320;
                    else
                        crc >>= 1;
                }
            }
            return ~crc;
        }
    }
}
