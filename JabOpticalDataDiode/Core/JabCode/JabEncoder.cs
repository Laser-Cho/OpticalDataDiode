using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JabOpticalDataDiode.Core.JabCode
{
    public class JabEncoder
    {
        private readonly int _outputPixelSize;
        private readonly int _modulePixelSize;

        public JabEncoder(int outputPixelSize = 650)
        {
            _outputPixelSize = outputPixelSize;
            _modulePixelSize = outputPixelSize / JabMatrix.GRID_SIZE;
        }

        /// <summary>
        /// 바이트 데이터를 8색 JAB Code 매트릭스 비트맵으로 인코딩 및 렌더링합니다.
        /// </summary>
        public BitmapSource EncodeToBitmap(byte[] data)
        {
            var matrix = new JabMatrix();

            // 바이너리 데이터를 3비트 정수(0~7) 스트림으로 변환하여 매트릭스에 매핑
            int bitPos = 0;
            int totalBits = data.Length * 8;

            for (int y = 0; y < JabMatrix.GRID_SIZE; y++)
            {
                for (int x = 0; x < JabMatrix.GRID_SIZE; x++)
                {
                    if (!JabMatrix.IsDataCell(x, y))
                        continue;

                    if (bitPos < totalBits)
                    {
                        byte val = 0;
                        for (int b = 0; b < 3; b++)
                        {
                            if (bitPos < totalBits)
                            {
                                int byteIdx = bitPos / 8;
                                int bitOffset = 7 - (bitPos % 8);
                                int bit = (data[byteIdx] >> bitOffset) & 1;
                                val = (byte)((val << 1) | bit);
                                bitPos++;
                            }
                            else
                            {
                                val = (byte)(val << 1);
                            }
                        }
                        matrix.Grid[x, y] = val;
                    }
                    else
                    {
                        // 패딩 (의사 난수 패턴으로 노이즈 분산)
                        matrix.Grid[x, y] = (byte)((x * 7 + y * 13) % 8);
                    }
                }
            }

            // 고해상도 비트맵 렌더링
            int size = JabMatrix.GRID_SIZE * _modulePixelSize;
            byte[] pixelBuffer = new byte[size * size * 4];

            for (int gy = 0; gy < JabMatrix.GRID_SIZE; gy++)
            {
                for (int gx = 0; gx < JabMatrix.GRID_SIZE; gx++)
                {
                    byte colorIdx = matrix.Grid[gx, gy];
                    var c = JabColor.DefaultPalette[colorIdx];

                    int startX = gx * _modulePixelSize;
                    int startY = gy * _modulePixelSize;

                    for (int py = 0; py < _modulePixelSize; py++)
                    {
                        int rowOffset = ((startY + py) * size + startX) * 4;
                        for (int px = 0; px < _modulePixelSize; px++)
                        {
                            int idx = rowOffset + (px * 4);
                            pixelBuffer[idx + 0] = c.B; // Blue
                            pixelBuffer[idx + 1] = c.G; // Green
                            pixelBuffer[idx + 2] = c.R; // Red
                            pixelBuffer[idx + 3] = 255; // Alpha
                        }
                    }
                }
            }

            var bitmap = BitmapSource.Create(
                size,
                size,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelBuffer,
                size * 4);

            bitmap.Freeze();
            return bitmap;
        }
    }
}
