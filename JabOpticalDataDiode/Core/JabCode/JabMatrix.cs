using System;

namespace JabOpticalDataDiode.Core.JabCode
{
    public class JabMatrix
    {
        public const int GRID_SIZE = 65;       // 65x65 모듈 (총 4225개 셀)
        public const int FINDER_SIZE = 7;      // 7x7 코너 파인더

        public byte[,] Grid { get; } = new byte[GRID_SIZE, GRID_SIZE]; // 각 셀 0~7 (3비트)

        public JabMatrix()
        {
            InitializeStructure();
        }

        /// <summary>
        /// 파인더 패턴 및 캘리브레이션 컬러 트랙 배치
        /// </summary>
        public void InitializeStructure()
        {
            // 1. 코너 파인더 패턴 배치 (4 코너)
            // Top-Left (Core: Red), Top-Right (Core: Green), Bottom-Left (Core: Blue), Bottom-Right (Core: Yellow)
            DrawFinderPattern(0, 0, 4);                          // TL: Red
            DrawFinderPattern(GRID_SIZE - FINDER_SIZE, 0, 2);    // TR: Green
            DrawFinderPattern(0, GRID_SIZE - FINDER_SIZE, 1);    // BL: Blue
            DrawFinderPattern(GRID_SIZE - FINDER_SIZE, GRID_SIZE - FINDER_SIZE, 6); // BR: Yellow

            // 2. 테두리 8색 캘리브레이션 트랙 배치
            for (int x = FINDER_SIZE; x < GRID_SIZE - FINDER_SIZE; x++)
            {
                byte colorIndex = (byte)((x - FINDER_SIZE) % 8);
                Grid[x, 0] = colorIndex;                     // 상단 테두리
                Grid[x, GRID_SIZE - 1] = (byte)(7 - colorIndex); // 하단 테두리 (역순)
            }

            for (int y = FINDER_SIZE; y < GRID_SIZE - FINDER_SIZE; y++)
            {
                byte colorIndex = (byte)((y - FINDER_SIZE) % 8);
                Grid[0, y] = colorIndex;                     // 좌측 테두리
                Grid[GRID_SIZE - 1, y] = (byte)(7 - colorIndex); // 우측 테두리
            }
        }

        private void DrawFinderPattern(int startX, int startY, byte coreColor)
        {
            for (int y = 0; y < FINDER_SIZE; y++)
            {
                for (int x = 0; x < FINDER_SIZE; x++)
                {
                    int gx = startX + x;
                    int gy = startY + y;

                    // 7x7 외곽 검정(0), 5x5 흰색(7), 3x3 코어 컬러
                    if (x == 0 || x == 6 || y == 0 || y == 6)
                        Grid[gx, gy] = 0; // Black
                    else if (x == 1 || x == 5 || y == 1 || y == 5)
                        Grid[gx, gy] = 7; // White
                    else
                        Grid[gx, gy] = coreColor;
                }
            }
        }

        /// <summary>
        /// 주어진 좌표가 데이터 기록 가능한 영역인지 확인
        /// </summary>
        public static bool IsDataCell(int x, int y)
        {
            // 코너 파인더 7x7 제외
            bool inTL = x < FINDER_SIZE && y < FINDER_SIZE;
            bool inTR = x >= GRID_SIZE - FINDER_SIZE && y < FINDER_SIZE;
            bool inBL = x < FINDER_SIZE && y >= GRID_SIZE - FINDER_SIZE;
            bool inBR = x >= GRID_SIZE - FINDER_SIZE && y >= GRID_SIZE - FINDER_SIZE;

            if (inTL || inTR || inBL || inBR)
                return false;

            // 테두리 캘리브레이션 트랙 제외
            if (x == 0 || x == GRID_SIZE - 1 || y == 0 || y == GRID_SIZE - 1)
                return false;

            return true;
        }

        /// <summary>
        /// 가용 데이터 셀 수 계산 (65x65에서 약 3780개 셀 => 셀당 3비트 = 약 1417 바이트 수용 가능!)
        /// </summary>
        public static int GetAvailableDataCells()
        {
            int count = 0;
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    if (IsDataCell(x, y)) count++;
                }
            }
            return count;
        }
    }
}
