using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace JabOpticalDataDiode.Core.Clipboard
{
    public class NativeClipboardService
    {
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

        public void InjectSnapshot(ClipboardSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Formats.Count == 0)
                return;

            if (!SafeOpenClipboard())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "수신 PC 클립보드를 열 수 없습니다.");

            try
            {
                NativeMethods.EmptyClipboard();

                foreach (var item in snapshot.Formats)
                {
                    if (item.RawBytes == null || item.RawBytes.Length == 0)
                        continue;

                    uint targetFormat = item.FormatId;

                    if (item.FormatId >= 0xC000)
                    {
                        targetFormat = NativeMethods.RegisterClipboardFormat(item.FormatName);
                        if (targetFormat == 0) continue;
                    }

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

                    IntPtr result = NativeMethods.SetClipboardData(targetFormat, hGlobal);
                    if (result == IntPtr.Zero)
                    {
                        NativeMethods.GlobalFree(hGlobal);
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
