using System;
using OpenCvSharp;
using OpticalDataDiode.Core.Fec;
using ZXing;
using ZXing.Common;

namespace OpticalDataDiode.Core.Qr
{
    public class QrDecoderService
    {
        private readonly BarcodeReaderGeneric _barcodeReader;

        public QrDecoderService()
        {
            _barcodeReader = new BarcodeReaderGeneric
            {
                AutoRotate = false,
                Options = new DecodingOptions
                {
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                    TryHarder = false, // 실시간 스트림 처리를 위한 빠른 경로
                    CharacterSet = "ISO-8859-1"
                }
            };
        }

        /// <summary>
        /// OpenCV의 Mat 이미지 프레임에서 QR 코드를 검출하고 FecPacket으로 디코딩합니다.
        /// </summary>
        public FecPacket? DecodeFromMat(Mat frame)
        {
            if (frame == null || frame.Empty())
                return null;

            // 그레이스케일 변환 (속도 및 인식률 향상)
            using var gray = new Mat();
            if (frame.Channels() == 3)
            {
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                frame.CopyTo(gray);
            }

            int width = gray.Width;
            int height = gray.Height;
            byte[] rawBytes = new byte[width * height];
            System.Runtime.InteropServices.Marshal.Copy(gray.Data, rawBytes, 0, rawBytes.Length);

            var luminanceSource = new PlanarYUVLuminanceSource(rawBytes, width, height, 0, 0, width, height, false);
            var result = _barcodeReader.Decode(luminanceSource);
            if (result == null || string.IsNullOrEmpty(result.Text))
                return null;

            try
            {
                byte[] packetBytes = Convert.FromBase64String(result.Text);
                if (FecPacket.TryDeserialize(packetBytes, out var packet))
                {
                    return packet;
                }
            }
            catch
            {
                // 디코딩 실패 또는 비정상 포맷 무시
            }

            return null;
        }
    }
}
