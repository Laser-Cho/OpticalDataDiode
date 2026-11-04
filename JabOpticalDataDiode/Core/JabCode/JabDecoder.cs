using System;
using System.Collections.Generic;
using JabOpticalDataDiode.Core.Fec;
using OpenCvSharp;

namespace JabOpticalDataDiode.Core.JabCode
{
    public class JabDecoder
    {
        private const int WARP_SIZE = 650;
        private const int MODULE_SIZE = WARP_SIZE / JabMatrix.GRID_SIZE; // 10 px

        /// <summary>
        /// 웹캠 프레임에서 JAB Code를 검출하고 기하/색상 보정 후 FecPacket을 복원합니다.
        /// </summary>
        public FecPacket? DecodeFromMat(Mat frame)
        {
            if (frame == null || frame.Empty())
                return null;

            // 1. 그레이스케일 및 이진화로 바코드 사각형 후보 영역 탐색
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(gray, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 21, 5);

            Cv2.FindContours(thresh, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            Point2f[]? bestQuad = null;
            double maxArea = frame.Width * frame.Height * 0.03; // 화면의 3% 이상 차지하는 사각형 탐색

            foreach (var cnt in contours)
            {
                double perimeter = Cv2.ArcLength(cnt, true);
                var approx = Cv2.ApproxPolyDP(cnt, 0.04 * perimeter, true);

                if (approx.Length == 4 && Cv2.IsContourConvex(approx))
                {
                    double area = Math.Abs(Cv2.ContourArea(approx));
                    if (area > maxArea)
                    {
                        maxArea = area;
                        bestQuad = new Point2f[4];
                        for (int i = 0; i < 4; i++)
                            bestQuad[i] = new Point2f(approx[i].X, approx[i].Y);
                    }
                }
            }

            // 사각형이 검출되지 않았으면 프레임 중앙 60% 영역을 ROI로 대체 시도
            if (bestQuad == null)
            {
                float marginX = frame.Width * 0.15f;
                float marginY = frame.Height * 0.15f;
                bestQuad = new Point2f[4]
                {
                    new Point2f(marginX, marginY),
                    new Point2f(frame.Width - marginX, marginY),
                    new Point2f(frame.Width - marginX, frame.Height - marginY),
                    new Point2f(marginX, frame.Height - marginY)
                };
            }

            // 2. 사각형의 4개 코너를 시계 방향으로 정렬 (TL, TR, BR, BL)
            Point2f[] orderedCorners = OrderCorners(bestQuad);

            // 3. 원근 투영 변환 (Perspective Warp to 650x650)
            Point2f[] destCorners = new Point2f[4]
            {
                new Point2f(0, 0),
                new Point2f(WARP_SIZE, 0),
                new Point2f(WARP_SIZE, WARP_SIZE),
                new Point2f(0, WARP_SIZE)
            };

            using var transformMatrix = Cv2.GetPerspectiveTransform(orderedCorners, destCorners);
            using var warped = new Mat();
            Cv2.WarpPerspective(frame, warped, transformMatrix, new Size(WARP_SIZE, WARP_SIZE));

            // 4. 4가지 회전(0, 90, 180, 270도)에 대해 디코딩 시도
            for (int rot = 0; rot < 4; rot++)
            {
                var packet = TryDecodeMatrix(warped);
                if (packet != null)
                    return packet;

                Cv2.Rotate(warped, warped, RotateFlags.Rotate90Clockwise);
            }

            return null;
        }

        private static FecPacket? TryDecodeMatrix(Mat warped)
        {
            // 1. 테두리 캘리브레이션 트랙으로부터 현재 조명 환경의 8색 팔레트 학습
            var calibratedPalette = CalibratePalette(warped);

            // 2. 데이터 셀을 순회하며 3비트 심볼 추출
            int totalDataCells = JabMatrix.GetAvailableDataCells();
            int totalBits = totalDataCells * 3;
            byte[] byteBuffer = new byte[totalBits / 8];
            int bitPos = 0;

            for (int gy = 0; gy < JabMatrix.GRID_SIZE; gy++)
            {
                for (int gx = 0; gx < JabMatrix.GRID_SIZE; gx++)
                {
                    if (!JabMatrix.IsDataCell(gx, gy))
                        continue;

                    // 셀 중심 영역의 평균 BGR 색상 측정 (3x3 서브샘플링)
                    int centerX = (gx * MODULE_SIZE) + (MODULE_SIZE / 2);
                    int centerY = (gy * MODULE_SIZE) + (MODULE_SIZE / 2);

                    int bSum = 0, gSum = 0, rSum = 0;
                    int count = 0;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int px = Math.Clamp(centerX + dx, 0, WARP_SIZE - 1);
                            int py = Math.Clamp(centerY + dy, 0, WARP_SIZE - 1);
                            Vec3b color = warped.At<Vec3b>(py, px);
                            bSum += color.Item0;
                            gSum += color.Item1;
                            rSum += color.Item2;
                            count++;
                        }
                    }

                    byte r = (byte)(rSum / count);
                    byte g = (byte)(gSum / count);
                    byte b = (byte)(bSum / count);

                    // 최근접 기준 색상 분류 (0~7)
                    byte symbolValue = JabColor.ClassifyColor(r, g, b, calibratedPalette);

                    // 3비트를 바이트 버퍼에 적재
                    for (int bi = 2; bi >= 0; bi--)
                    {
                        if (bitPos < totalBits)
                        {
                            int byteIdx = bitPos / 8;
                            int bitOffset = 7 - (bitPos % 8);
                            int bit = (symbolValue >> bi) & 1;
                            byteBuffer[byteIdx] |= (byte)(bit << bitOffset);
                            bitPos++;
                        }
                    }
                }
            }

            // 3. FecPacket 역직렬화 및 CRC32 검증
            if (FecPacket.TryDeserialize(byteBuffer, out var packet))
            {
                return packet;
            }

            return null;
        }

        private static RgbColor[] CalibratePalette(Mat warped)
        {
            var palette = new RgbColor[8];
            int[] counts = new int[8];
            int[] rSums = new int[8];
            int[] gSums = new int[8];
            int[] bSums = new int[8];

            // 상단 테두리 캘리브레이션 트랙 샘플링
            for (int gx = JabMatrix.FINDER_SIZE; gx < JabMatrix.GRID_SIZE - JabMatrix.FINDER_SIZE; gx++)
            {
                byte expectedColor = (byte)((gx - JabMatrix.FINDER_SIZE) % 8);
                int cx = (gx * MODULE_SIZE) + (MODULE_SIZE / 2);
                int cy = MODULE_SIZE / 2;

                Vec3b color = warped.At<Vec3b>(cy, cx);
                bSums[expectedColor] += color.Item0;
                gSums[expectedColor] += color.Item1;
                rSums[expectedColor] += color.Item2;
                counts[expectedColor]++;
            }

            for (int i = 0; i < 8; i++)
            {
                if (counts[i] > 0)
                {
                    palette[i] = new RgbColor(
                        (byte)(rSums[i] / counts[i]),
                        (byte)(gSums[i] / counts[i]),
                        (byte)(bSums[i] / counts[i]));
                }
                else
                {
                    palette[i] = JabColor.DefaultPalette[i];
                }
            }

            return palette;
        }

        private static Point2f[] OrderCorners(Point2f[] pts)
        {
            var sortedByY = new List<Point2f>(pts);
            sortedByY.Sort((a, b) => a.Y.CompareTo(b.Y));

            var topPts = new List<Point2f> { sortedByY[0], sortedByY[1] };
            topPts.Sort((a, b) => a.X.CompareTo(b.X));
            Point2f tl = topPts[0];
            Point2f tr = topPts[1];

            var bottomPts = new List<Point2f> { sortedByY[2], sortedByY[3] };
            bottomPts.Sort((a, b) => a.X.CompareTo(b.X));
            Point2f bl = bottomPts[0];
            Point2f br = bottomPts[1];

            return new Point2f[4] { tl, tr, br, bl };
        }
    }
}
