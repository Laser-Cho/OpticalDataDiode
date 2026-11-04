using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OpenCvSharp;
using JabOpticalDataDiode.Core.Camera;
using JabOpticalDataDiode.Core.Clipboard;
using JabOpticalDataDiode.Core.Compression;
using JabOpticalDataDiode.Core.Fec;
using JabOpticalDataDiode.Core.JabCode;

namespace JabOpticalDataDiode.Views
{
    public partial class ReceiverView : UserControl, IDisposable
    {
        private readonly CameraCaptureService _cameraService = new();
        private readonly JabDecoder _jabDecoder = new();
        private readonly FountainDecoder _fountainDecoder = new();
        private readonly NativeClipboardService _clipboardService = new();

        private int _frameCount = 0;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private bool _isProcessingFrame = false;
        private bool _isCompleted = false;

        public ReceiverView()
        {
            InitializeComponent();
            _cameraService.PreviewFrameReady += CameraService_PreviewFrameReady;
            _cameraService.FrameReceived += CameraService_FrameReceived;
        }

        private void BtnToggleCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_cameraService.IsRunning)
            {
                StopCamera();
            }
            else
            {
                StartCamera();
            }
        }

        private void StartCamera()
        {
            try
            {
                _isCompleted = false;
                int camIndex = CmbCameras.SelectedIndex >= 0 ? CmbCameras.SelectedIndex : 0;
                _cameraService.Start(camIndex);

                BtnToggleCamera.Content = "⏹ 웹캠 스캔 중지";
                BtnToggleCamera.Background = Brushes.OrangeRed;
                TxtStatus.Text = "웹캠 수신 중... JAB Code를 화면 중앙에 맞춰주세요.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"웹캠 시작 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopCamera()
        {
            _cameraService.Stop();
            BtnToggleCamera.Content = "📷 웹캠 스캔 시작";
            BtnToggleCamera.Background = Brushes.Indigo;
            TxtStatus.Text = "웹캠 스캔이 정지되었습니다.";
        }

        private void CameraService_PreviewFrameReady(System.Windows.Media.Imaging.BitmapSource preview)
        {
            Dispatcher.InvokeAsync(() =>
            {
                CamPreviewImage.Source = preview;
            }, DispatcherPriority.Render);
        }

        private void CameraService_FrameReceived(Mat frame)
        {
            if (_isCompleted || _isProcessingFrame)
                return;

            _isProcessingFrame = true;
            _frameCount++;

            try
            {
                var packet = _jabDecoder.DecodeFromMat(frame);
                if (packet != null)
                {
                    bool complete = _fountainDecoder.ProcessPacket(packet);

                    Dispatcher.InvokeAsync(() =>
                    {
                        TxtSessionId.Text = $"0x{_fountainDecoder.CurrentSessionId:X4}";
                        TxtRankProgress.Text = $"{_fountainDecoder.ReceivedRank} / {_fountainDecoder.RequiredBlocks}";
                        PrgProgress.Value = _fountainDecoder.ProgressRatio * 100.0;

                        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                        {
                            TxtScanFps.Text = $"{_frameCount} FPS";
                            _frameCount = 0;
                            _fpsStopwatch.Restart();
                        }
                    });

                    if (complete && !_isCompleted)
                    {
                        _isCompleted = true;
                        OnTransferCompleted();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"프레임 처리 오류: {ex.Message}");
            }
            finally
            {
                _isProcessingFrame = false;
            }
        }

        private void OnTransferCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    byte[] compressed = _fountainDecoder.ReconstructData();
                    byte[] serializedSnapshot = CompressionHelper.Decompress(compressed);
                    var snapshot = ClipboardSnapshot.Deserialize(serializedSnapshot);
                    _clipboardService.InjectSnapshot(snapshot);

                    TxtStatus.Text = $"✅ 전송 완료! {snapshot.Formats.Count}개 포맷이 클립보드에 주입되었습니다. PPT에서 Ctrl+V를 누르세요!";
                    BoxNotice.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245));
                    BoxNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208));
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));

                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = $"복원 실패: {ex.Message}";
                    BoxNotice.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                    BoxNotice.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
                }
            });
        }

        public void Dispose()
        {
            _cameraService.Dispose();
        }
    }
}
