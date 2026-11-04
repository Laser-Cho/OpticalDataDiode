using System;
using System.Collections;

namespace JabOpticalDataDiode.Core.Fec
{
    public class FountainDecoder
    {
        private ushort _sessionId;
        private ushort _k;
        private ushort _blockSize;
        private int _totalLength;

        private BitArray[] _pivotEquations = Array.Empty<BitArray>();
        private byte[][] _pivotPayloads = Array.Empty<byte[]>();
        private bool[] _hasPivot = Array.Empty<bool>();
        private int _rank = 0;
        private bool _isInitialized = false;

        public bool IsComplete => _isInitialized && _rank == _k;
        public int RequiredBlocks => _k;
        public int ReceivedRank => _rank;
        public double ProgressRatio => _k == 0 ? 0 : Math.Min(1.0, (double)_rank / _k);
        public ushort CurrentSessionId => _sessionId;

        public bool ProcessPacket(FecPacket packet)
        {
            if (packet == null) return false;

            if (!_isInitialized || packet.SessionId != _sessionId)
            {
                Initialize(packet.SessionId, packet.TotalSourceBlocks, packet.BlockSize, packet.TotalDataLength);
            }

            if (IsComplete) return true;

            var eq = new BitArray(_k);
            var neighbors = FountainEncoder.GetNeighbors(packet.SequenceSeed, _k);
            foreach (int idx in neighbors)
            {
                if (idx < _k) eq.Set(idx, true);
            }

            byte[] val = (byte[])packet.Payload.Clone();

            for (int i = 0; i < _k; i++)
            {
                if (!eq.Get(i)) continue;

                if (_hasPivot[i])
                {
                    eq.Xor(_pivotEquations[i]);
                    FountainEncoder.XorInPlace(val, _pivotPayloads[i]);
                }
                else
                {
                    _pivotEquations[i] = eq;
                    _pivotPayloads[i] = val;
                    _hasPivot[i] = true;
                    _rank++;
                    break;
                }
            }

            return IsComplete;
        }

        private void Initialize(ushort sessionId, ushort k, ushort blockSize, int totalLength)
        {
            _sessionId = sessionId;
            _k = k;
            _blockSize = blockSize;
            _totalLength = totalLength;

            _pivotEquations = new BitArray[_k];
            _pivotPayloads = new byte[_k][];
            _hasPivot = new bool[_k];
            _rank = 0;
            _isInitialized = true;
        }

        public byte[] ReconstructData()
        {
            if (!IsComplete)
                throw new InvalidOperationException("충분한 심볼이 수신되지 않았습니다.");

            for (int i = _k - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_pivotEquations[j].Get(i))
                    {
                        _pivotEquations[j].Set(i, false);
                        FountainEncoder.XorInPlace(_pivotPayloads[j], _pivotPayloads[i]);
                    }
                }
            }

            byte[] reconstructed = new byte[_totalLength];
            for (int i = 0; i < _k; i++)
            {
                int offset = i * _blockSize;
                int len = Math.Min(_blockSize, _totalLength - offset);
                Buffer.BlockCopy(_pivotPayloads[i], 0, reconstructed, offset, len);
            }

            return reconstructed;
        }
    }
}
