using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace OpticalDataDiode.Core.Qr
{
    public class QrEncoderService
    {
        private readonly BarcodeWriterPixelData _writer;
        private readonly int _pixelSize;

        public QrEncoderService(int pixelSize = 600)
        {
            _pixelSize = pixelSize;
            _writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = pixelSize,
                    Height = pixelSize,
                    Margin = 1,
                    ErrorCorrection = ErrorCorrectionLevel.L,
                    CharacterSet = "ISO-8859-1"
                }
            };
        }

        /// <summary>
        /// 바이트 배열을 고밀도 QR 코드 WriteableBitmap으로 렌더링합니다.
        /// </summary>
        public BitmapSource GenerateQrBitmap(byte[] data)
        {
            // 바이너리 데이터를 안전하게 QR로 변환하기 위해 Base64 인코딩 사용
            string base64 = Convert.ToBase64String(data);
            var pixelData = _writer.Write(base64);

            var bitmap = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4);

            bitmap.Freeze(); // UI 스레드 교차 안전성 확보
            return bitmap;
        }
    }
}
