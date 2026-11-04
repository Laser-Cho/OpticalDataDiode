using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using JabOpticalDataDiode.Core.Clipboard;
using JabOpticalDataDiode.Core.Compression;
using JabOpticalDataDiode.Core.Fec;
using JabOpticalDataDiode.Core.JabCode;

namespace JabOpticalDataDiode.Views
{
    public partial class SenderView : UserControl
    {
        private readonly NativeClipboardService _clipboardService = new();
        private readonly JabEncoder _jabEncoder = new(650);
        private readonly DispatcherTimer _broadcastTimer = new();

        private FountainEncoder? _encoder;
        private bool _isBroadcasting = false;

        public SenderView()
        {
            InitializeComponent();
            _broadcastTimer.Tick += BroadcastTimer_Tick;
            UpdateInterval(12);
        }

        private void UpdateInterval(int fps)
        {
            if (fps <= 0) fps = 1;
            _broadcastTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        }

        private void FpsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateInterval((int)e.NewValue);
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var snapshot = _clipboardService.CaptureSnapshot();
                if (snapshot.Formats.Count == 0)
                {
                    MessageBox.Show("클립보드에 PPT 관련 데이터나 이미지가 존재하지 않습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                byte[] raw = snapshot.Serialize();
                byte[] compressed = CompressionHelper.Compress(raw);

                // JAB Code는 모듈당 3비트(8색)이므로 블록 크기를 800 바이트로 확장 (전송 효율 대폭 상승)
                _encoder = new FountainEncoder(compressed, 800);

                TxtFormatCount.Text = $"{snapshot.Formats.Count} 개 포맷";
                TxtDataSize.Text = $"{raw.Length / 1024.0:F1} KB ➔ {compressed.Length / 1024.0:F1} KB";
                TxtBlockK.Text = $"{_encoder.SourceBlockCount} 블록 (K)";
                TxtStatus.Text = $"캡처 완료! 세션 ID: 0x{_encoder.SessionId:X4}";

                BtnToggleBroadcast.IsEnabled = true;
                StartBroadcast();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"클립보드 캡처 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnToggleBroadcast_Click(object sender, RoutedEventArgs e)
        {
            if (_isBroadcasting)
            {
                StopBroadcast();
            }
            else
            {
                StartBroadcast();
            }
        }

        private void StartBroadcast()
        {
            if (_encoder == null) return;
            _isBroadcasting = true;
            BtnToggleBroadcast.Content = "⏸ 전송 일시정지";
            BtnToggleBroadcast.Background = System.Windows.Media.Brushes.OrangeRed;
            _broadcastTimer.Start();
        }

        private void StopBroadcast()
        {
            _isBroadcasting = false;
            BtnToggleBroadcast.Content = "▶ JAB 전송 재개";
            BtnToggleBroadcast.Background = System.Windows.Media.Brushes.SeaGreen;
            _broadcastTimer.Stop();
        }

        private void BroadcastTimer_Tick(object? sender, EventArgs e)
        {
            if (_encoder == null) return;

            var packet = _encoder.NextPacket();
            byte[] packetBytes = packet.Serialize();

            // 8색 JAB Code 비트맵 생성
            var jabBitmap = _jabEncoder.EncodeToBitmap(packetBytes);
            JabDisplayImage.Source = jabBitmap;

            TxtEmittedEsi.Text = $"#{packet.SequenceSeed} (총 K={_encoder.SourceBlockCount})";
        }
    }
}
