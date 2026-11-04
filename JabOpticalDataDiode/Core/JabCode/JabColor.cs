using System;

namespace JabOpticalDataDiode.Core.JabCode
{
    public struct RgbColor
    {
        public byte R;
        public byte G;
        public byte B;

        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public double DistanceSquared(byte r, byte g, byte b)
        {
            int dr = R - r;
            int dg = G - g;
            int db = B - b;
            return (dr * dr * 2.0) + (dg * dg * 4.0) + (db * db * 3.0); // 가중치 부여 RGB 유클리드 거리
        }
    }

    public static class JabColor
    {
        // 8색 컬러 팔레트 (3비트/모듈)
        // 0: Black, 1: Blue, 2: Green, 3: Cyan, 4: Red, 5: Magenta, 6: Yellow, 7: White
        public static readonly RgbColor[] DefaultPalette = new RgbColor[8]
        {
            new RgbColor(0, 0, 0),       // 0: Black   (000)
            new RgbColor(0, 0, 255),     // 1: Blue    (001)
            new RgbColor(0, 255, 0),     // 2: Green   (010)
            new RgbColor(0, 255, 255),   // 3: Cyan    (011)
            new RgbColor(255, 0, 0),     // 4: Red     (100)
            new RgbColor(255, 0, 255),   // 5: Magenta (101)
            new RgbColor(255, 255, 0),   // 6: Yellow  (110)
            new RgbColor(255, 255, 255)  // 7: White   (111)
        };

        /// <summary>
        /// 동적 캘리브레이션된 팔레트에서 가장 가까운 컬러 인덱스(0~7)를 판별합니다.
        /// </summary>
        public static byte ClassifyColor(byte r, byte g, byte b, RgbColor[]? calibratedPalette = null)
        {
            var palette = calibratedPalette ?? DefaultPalette;
            byte bestIndex = 0;
            double minDistance = double.MaxValue;

            for (byte i = 0; i < 8; i++)
            {
                double dist = palette[i].DistanceSquared(r, g, b);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
