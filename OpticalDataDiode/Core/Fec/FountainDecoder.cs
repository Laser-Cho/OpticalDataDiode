using System;
using System.Collections;
using System.Collections.Generic;

namespace OpticalDataDiode.Core.Fec
{
    public class FountainDecoder
    {
        private ushort _sessionId;
        private ushort _k;
        private ushort _blockSize;
        private int _totalLength;

        // 점진적 가우스 소거법을 위한 행렬 상태 (GF(2))
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

        /// <summary>
        /// 새로운 패킷을 수신하여 디코더 상태에 반영합니다.
        /// </summary>
        public bool ProcessPacket(FecPacket packet)
        {
            if (packet == null) return false;

            // 새로운 세션 시작 시 초기화
            if (!_isInitialized || packet.SessionId != _sessionId)
            {
                Initialize(packet.SessionId, packet.TotalSourceBlocks, packet.BlockSize, packet.TotalDataLength);
            }

            if (IsComplete) return true;

            // 해당 패킷의 인코딩 계수 벡터 생성 (1 x K)
            var eq = new BitArray(_k);
            var neighbors = FountainEncoder.GetNeighbors(packet.SequenceSeed, _k);
            foreach (int idx in neighbors)
            {
                if (idx < _k) eq.Set(idx, true);
            }

            byte[] val = (byte[])packet.Payload.Clone();

            // 점진적 가우스 소거 (Incremental Gaussian Elimination)
            for (int i = 0; i < _k; i++)
            {
                if (!eq.Get(i)) continue;

                if (_hasPivot[i])
                {
                    // 기존 피벗 행과 소거
                    eq.Xor(_pivotEquations[i]);
                    FountainEncoder.XorInPlace(val, _pivotPayloads[i]);
                }
                else
                {
                    // 새로운 피벗 등록
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

        /// <summary>
        /// Rank가 K에 도달했을 때 역대입(Back-substitution)을 통해 원본 바이트 배열 복원
        /// </summary>
        public byte[] ReconstructData()
        {
            if (!IsComplete)
                throw new InvalidOperationException("충분한 심볼이 수신되지 않았습니다.");

            // 후진 소거 (Back-substitution)
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

            // 원본 데이터 병합
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
