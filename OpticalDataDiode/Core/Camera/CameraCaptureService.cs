using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace OpticalDataDiode.Core.Camera
{
    public class CameraCaptureService : IDisposable
    {
        private VideoCapture? _capture;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private bool _isDisposed;

        public event Action<Mat>? FrameReceived;
        public event Action<BitmapSource>? PreviewFrameReady;
        public bool IsRunning => _captureTask != null && !_captureTask.IsCompleted;

        /// <summary>
        /// 지정한 카메라 인덱스로 캡처를 시작합니다.
        /// </summary>
        public void Start(int cameraIndex = 0, int width = 1280, int height = 720)
        {
            if (IsRunning) return;

            _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                throw new InvalidOperationException($"카메라({cameraIndex})를 열 수 없습니다.");
            }

            _capture.Set(VideoCaptureProperties.FrameWidth, width);
            _capture.Set(VideoCaptureProperties.FrameHeight, height);
            _capture.Set(VideoCaptureProperties.Fps, 30);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _captureTask = Task.Run(() => CaptureLoop(token), token);
        }

        private void CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();

            while (!token.IsCancellationRequested && _capture != null && _capture.IsOpened())
            {
                if (!_capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(10);
                    continue;
                }

                // QR 디코딩 이벤트 발송
                FrameReceived?.Invoke(frame);

                // UI 프리뷰 생성
                try
                {
                    var preview = frame.ToWriteableBitmap();
                    preview.Freeze();
                    PreviewFrameReady?.Invoke(preview);
                }
                catch
                {
                    // UI 프리뷰 렌더링 스킵
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _captureTask?.Wait(1000);
            }
            catch {}

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Stop();
            _cts?.Dispose();
            _isDisposed = true;
        }
    }
}
