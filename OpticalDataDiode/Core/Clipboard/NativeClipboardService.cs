using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpticalDataDiode.Core.Clipboard
{
    public class NativeClipboardService
    {
        // PPT 전송 시 보존해야 할 핵심 포맷 이름 목록 (우선순위 순)
        private static readonly HashSet<string> TargetFormatNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Art::GVML ClipFormat",
            "PowerPoint 14.0 Slides Internal",
            "PowerPoint 12.0 Slides Internal",
            "XML Spreadsheet",
            "Office Open XML",
            "HTML Format",
            "PNG",
            "JFIF"
        };

        private const int MaxRetry = 5;
        private const int RetryDelayMs = 50;

        private static bool SafeOpenClipboard()
        {
            for (int i = 0; i < MaxRetry; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                    return true;
                Thread.Sleep(RetryDelayMs);
            }
            return false;
        }

        private static string GetFormatName(uint formatId)
        {
            char[] buffer = new char[256];
            int length = NativeMethods.GetClipboardFormatName(formatId, buffer, buffer.Length);
            if (length > 0)
                return new string(buffer, 0, length);

            return formatId switch
            {
                1 => "CF_TEXT",
                2 => "CF_BITMAP",
                8 => "CF_DIB",
                13 => "CF_UNICODETEXT",
                14 => "CF_ENHMETAFILE",
                17 => "CF_DIBV5",
                _ => $"FORMAT_{formatId}"
            };
        }

        /// <summary>
        /// [송신용] 현재 클립보드에서 PPT 관련 모든 바이너리 포맷 데이터를 스냅샷으로 추출합니다.
        /// </summary>
        public ClipboardSnapshot CaptureSnapshot()
        {
            var snapshot = new ClipboardSnapshot();

            if (!SafeOpenClipboard())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "클립보드를 열 수 없습니다.");

            try
            {
                uint format = 0;
                while ((format = NativeMethods.EnumClipboardFormats(format)) != 0)
                {
                    string formatName = GetFormatName(format);

                    // PPT 복원에 필수적인 포맷 또는 표준 DIB/텍스트만 필터링하여 페이로드 최적화
                    bool isTarget = TargetFormatNames.Contains(formatName) ||
                                    format == 8 /* CF_DIB */ ||
                                    format == 13 /* CF_UNICODETEXT */;

                    if (!isTarget) continue;

                    IntPtr hData = NativeMethods.GetClipboardData(format);
                    if (hData == IntPtr.Zero) continue;

                    IntPtr pData = NativeMethods.GlobalLock(hData);
                    if (pData == IntPtr.Zero) continue;

                    try
                    {
                        int size = (int)NativeMethods.GlobalSize(hData).ToUInt32();
                        byte[] rawBytes = new byte[size];
                        Marshal.Copy(pData, rawBytes, 0, size);

                        snapshot.Formats.Add(new ClipboardFormatData
                        {
                            FormatId = format,
                            FormatName = formatName,
                            RawBytes = rawBytes
                        });
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hData);
                    }
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }

            return snapshot;
        }

        /// <summary>
        /// [수신용] 전송받은 스냅샷 데이터를 수신 PC 클립보드에 저수준 Win32 메모리로 주입합니다.
        /// </summary>
        public void InjectSnapshot(ClipboardSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Formats.Count == 0)
                return;

            if (!SafeOpenClipboard())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "수신 PC 클립보드를 열 수 없습니다.");

            try
            {
                NativeMethods.EmptyClipboard(); // 기존 클립보드 초기화

                foreach (var item in snapshot.Formats)
                {
                    if (item.RawBytes == null || item.RawBytes.Length == 0)
                        continue;

                    uint targetFormat = item.FormatId;

                    // 사용자 지정 등록 포맷(Registered Format)인 경우 수신 PC에서 포맷 ID를 다시 등록/조회해야 일치함
                    if (item.FormatId >= 0xC000)
                    {
                        targetFormat = NativeMethods.RegisterClipboardFormat(item.FormatName);
                        if (targetFormat == 0) continue;
                    }

                    // Moveable Global Memory 할당
                    IntPtr hGlobal = NativeMethods.GlobalAlloc(
                        NativeMethods.GMEM_MOVEABLE | NativeMethods.GMEM_ZEROINIT,
                        new UIntPtr((uint)item.RawBytes.Length));

                    if (hGlobal == IntPtr.Zero) continue;

                    IntPtr pDest = NativeMethods.GlobalLock(hGlobal);
                    if (pDest == IntPtr.Zero)
                    {
                        NativeMethods.GlobalFree(hGlobal);
                        continue;
                    }

                    try
                    {
                        Marshal.Copy(item.RawBytes, 0, pDest, item.RawBytes.Length);
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hGlobal);
                    }

                    // SetClipboardData 호출 후 해당 메모리 블록의 소유권은 Windows OS(클립보드)로 이전됨
                    IntPtr result = NativeMethods.SetClipboardData(targetFormat, hGlobal);
                    if (result == IntPtr.Zero)
                    {
                        NativeMethods.GlobalFree(hGlobal); // 실패 시 메모리 누수 방지
                    }
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }
    }
}
