using System;
using System.IO;
using System.IO.Compression;

namespace JabOpticalDataDiode.Core.Compression
{
    public static class CompressionHelper
    {
        public static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var outputStream = new MemoryStream();
            using (var gzip = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return Array.Empty<byte>();

            using var inputStream = new MemoryStream(compressedData);
            using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            gzip.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}
