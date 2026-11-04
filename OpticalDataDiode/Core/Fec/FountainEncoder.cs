using System;
using System.Collections.Generic;

namespace OpticalDataDiode.Core.Fec
{
    public class FountainEncoder
    {
        private readonly byte[] _originalData;
        private readonly ushort _k;          // Source block count
        private readonly ushort _blockSize;  // Symbol size in bytes
        private readonly ushort _sessionId;
        private readonly byte[][] _sourceBlocks;
        private uint _nextEsi = 0;

        public ushort SourceBlockCount => _k;
        public ushort BlockSize => _blockSize;
        public ushort SessionId => _sessionId;
        public int OriginalLength => _originalData.Length;

        public FountainEncoder(byte[] data, ushort blockSize = 512)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("데이터가 비어 있습니다.", nameof(data));

            _originalData = data;
            _blockSize = blockSize;
            _sessionId = (ushort)new Random().Next(1, 65535);

            _k = (ushort)Math.Ceiling((double)data.Length / _blockSize);
            _sourceBlocks = new byte[_k][];

            for (int i = 0; i < _k; i++)
            {
                _sourceBlocks[i] = new byte[_blockSize];
                int offset = i * _blockSize;
                int len = Math.Min(_blockSize, data.Length - offset);
                Buffer.BlockCopy(data, offset, _sourceBlocks[i], 0, len);
            }
        }

        /// <summary>
        /// 다음 인코딩 패킷을 생성합니다.
        /// 처음 K개는 Systematic(원형 그대로), 이후는 Robust Fountain 심볼 무한 생성.
        /// </summary>
        public FecPacket NextPacket()
        {
            uint esi = _nextEsi++;
            byte[] payload = new byte[_blockSize];

            if (esi < _k)
            {
                // Systematic Symbol (Degree = 1)
                Buffer.BlockCopy(_sourceBlocks[esi], 0, payload, 0, _blockSize);
            }
            else
            {
                // Fountain Coded Symbol
                var neighbors = GetNeighbors(esi, _k);
                foreach (int srcIdx in neighbors)
                {
                    XorInPlace(payload, _sourceBlocks[srcIdx]);
                }
            }

            return new FecPacket
            {
                SessionId = _sessionId,
                TotalSourceBlocks = _k,
                BlockSize = _blockSize,
                TotalDataLength = _originalData.Length,
                SequenceSeed = esi,
                Payload = payload
            };
        }

        public static List<int> GetNeighbors(uint esi, int k)
        {
            var neighbors = new List<int>();
            if (k <= 1)
            {
                neighbors.Add(0);
                return neighbors;
            }

            if (esi < (uint)k)
            {
                neighbors.Add((int)esi);
                return neighbors;
            }

            // 결정론적 의사 난수 생성기 (Linear Congruential Generator)
            ulong state = ((ulong)esi * 6364136223846793005UL) + 1442695040888963407UL;

            // Robust Soliton 분포를 근사하는 Degree 선택
            int degree = SampleDegree(ref state, k);
            var chosen = new HashSet<int>();

            while (chosen.Count < degree)
            {
                state = (state * 6364136223846793005UL) + 1442695040888963407UL;
                int idx = (int)((state >> 33) % (ulong)k);
                chosen.Add(idx);
            }

            neighbors.AddRange(chosen);
            return neighbors;
        }

        private static int SampleDegree(ref ulong state, int k)
        {
            state = (state * 6364136223846793005UL) + 1442695040888963407UL;
            double p = (double)(state >> 32) / (double)uint.MaxValue;

            if (p < 0.1) return 1;
            if (p < 0.6) return Math.Min(2, k);
            if (p < 0.8) return Math.Min(3, k);
            if (p < 0.9) return Math.Min(4, k);
            if (p < 0.96) return Math.Min(Math.Max(5, k / 4), k);
            return Math.Min(Math.Max(7, k / 2), k);
        }

        public static void XorInPlace(byte[] target, byte[] source)
        {
            int len = Math.Min(target.Length, source.Length);
            int i = 0;

            // 64비트 단위 고속 XOR
            int longCount = len / 8;
            for (; i < longCount * 8; i += 8)
            {
                ulong t = BitConverter.ToUInt64(target, i);
                ulong s = BitConverter.ToUInt64(source, i);
                Buffer.BlockCopy(BitConverter.GetBytes(t ^ s), 0, target, i, 8);
            }

            for (; i < len; i++)
            {
                target[i] ^= source[i];
            }
        }
    }
}
