using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32.SafeHandles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("ADB Tool (ASN)")]
[assembly: AssemblyProduct("ADB Tool (ASN)")]
[assembly: AssemblyVersion("0.0.3.2")]
[assembly: AssemblyFileVersion("0.0.3.2")]

namespace AdbQuickTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class AppConfig
    {
        public string AdbPath { get; set; }
        public string ScrcpyPath { get; set; }
        public string PythonPath { get; set; }
        public string ScreenshotDir { get; set; }
        public string DownloadDir { get; set; }
        public string CmdBackgroundColor { get; set; }
        public string CmdOutputColor { get; set; }
        public string CmdPromptColor { get; set; }
        public string CmdInputColor { get; set; }
        public string LanScanSubnets { get; set; }
        public List<string> ScrcpyHighQualityDisabledSerials { get; set; }
        public List<string> ScrcpyHighQualityForcedSerials { get; set; }
        public List<QuickTool> QuickTools { get; set; }

        public AppConfig()
        {
            ScrcpyHighQualityDisabledSerials = new List<string>();
            ScrcpyHighQualityForcedSerials = new List<string>();
            QuickTools = new List<QuickTool>();
        }
    }

    public sealed class QuickTool
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }
    }

    public sealed class DeviceInfo
    {
        public string Serial { get; set; }
        public string State { get; set; }
        public string Model { get; set; }
        public string NetworkType { get; set; }
        public string EthernetIp { get; set; }
        public string EthernetMac { get; set; }
        public string BluetoothMac { get; set; }
        public string AndroidVersion { get; set; }
        public string Raw { get; set; }

        public DeviceInfo Clone()
        {
            return new DeviceInfo
            {
                Serial = Serial,
                State = State,
                Model = Model,
                NetworkType = NetworkType,
                EthernetIp = EthernetIp,
                EthernetMac = EthernetMac,
                BluetoothMac = BluetoothMac,
                AndroidVersion = AndroidVersion,
                Raw = Raw
            };
        }
    }

    public sealed class FileEntry
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public long SizeBytes { get; set; }
        public string Modified { get; set; }
        public string Permission { get; set; }
        public string FullPath { get; set; }
    }

    public sealed class AppEntry
    {
        public string Name { get; set; }
        public string PackageName { get; set; }
        public string VersionName { get; set; }
        public string VersionCode { get; set; }
        public string Type { get; set; }
        public string ApkPath { get; set; }
        public bool IsDisabled { get; set; }
    }

    public sealed class ScreenshotItem
    {
        public string FullPath { get; private set; }

        public ScreenshotItem(string fullPath)
        {
            FullPath = fullPath;
        }

        public override string ToString()
        {
            return Path.GetFileName(FullPath);
        }
    }

    public sealed class TerminalSession
    {
        public Process Process { get; set; }
        public PseudoConsoleSession FixedSession { get; set; }
        public TabPage Page { get; set; }
        public RichTextBox Output { get; set; }
        public TextBox Input { get; set; }
        public int InputStart { get; set; }
        public int TailPaddingLength { get; set; }
        public string Title { get; set; }
        public string Serial { get; set; }
        public bool IsAdbShell { get; set; }
        public bool UsesFixedCore { get; set; }
        public bool AdbShellLocalMode { get; set; }
        public int AdbShellInputStartIndex { get; set; }
        public bool WheelScrolledAwayFromBottom { get; set; }
        public int LastCursorIndex { get; set; }
        public bool FlushScheduled { get; set; }
        public bool SuppressLeadingBlankAfterClear { get; set; }
        public bool EscapePending { get; set; }
        public bool CsiPending { get; set; }
        public bool OscPending { get; set; }
        public bool OscEscapePending { get; set; }
        public int CurrentInputCursor { get; set; }
        public List<string> CommandHistory { get; private set; }
        public List<string> AdbShellHistory { get; private set; }
        public int HistoryIndex { get; set; }
        public string HistoryDraft { get; set; }
        public int AdbShellHistoryIndex { get; set; }
        public string AdbShellHistoryDraft { get; set; }
        public StringBuilder PendingOutput { get; private set; }
        public object PendingOutputLock { get; private set; }
        public StringBuilder CurrentInput { get; private set; }
        public StringBuilder ControlBuffer { get; private set; }

        public TerminalSession()
        {
            CommandHistory = new List<string>();
            AdbShellHistory = new List<string>();
            HistoryIndex = 0;
            HistoryDraft = "";
            AdbShellHistoryIndex = -1;
            AdbShellHistoryDraft = "";
            AdbShellInputStartIndex = -1;
            PendingOutput = new StringBuilder();
            PendingOutputLock = new object();
            CurrentInput = new StringBuilder();
            ControlBuffer = new StringBuilder();
        }
    }

    public sealed class VirtualTerminalBuffer
    {
        private readonly List<StringBuilder> lines = new List<StringBuilder>();
        private readonly StringBuilder csiBuffer = new StringBuilder();
        private readonly StringBuilder oscBuffer = new StringBuilder();
        private int cursorRow;
        private int cursorCol;
        private int columns;
        private int rows;
        private bool escapePending;
        private bool csiPending;
        private bool oscPending;
        private bool oscEscapePending;
        private bool clearLineOnNextPrint;

        public VirtualTerminalBuffer(int columns, int rows)
        {
            this.columns = Math.Max(40, columns);
            this.rows = Math.Max(10, rows);
            lines.Add(new StringBuilder());
        }

        public void Resize(int columns, int rows)
        {
            this.columns = Math.Max(40, columns);
            this.rows = Math.Max(10, rows);
            EnsureLine(cursorRow);
        }

        public void Clear()
        {
            lines.Clear();
            lines.Add(new StringBuilder());
            cursorRow = 0;
            cursorCol = 0;
            escapePending = false;
            csiPending = false;
            oscPending = false;
            oscEscapePending = false;
            clearLineOnNextPrint = false;
            csiBuffer.Length = 0;
            oscBuffer.Length = 0;
        }

        public void Append(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (oscPending)
                {
                    if (oscEscapePending)
                    {
                        oscEscapePending = false;
                        if (ch == '\\')
                        {
                            oscPending = false;
                            oscBuffer.Length = 0;
                            continue;
                        }
                    }

                    if (ch == '\a')
                    {
                        oscPending = false;
                        oscBuffer.Length = 0;
                        continue;
                    }

                    if (ch == 0x1b)
                    {
                        oscEscapePending = true;
                    }
                    else
                    {
                        oscBuffer.Append(ch);
                    }
                    continue;
                }

                if (csiPending)
                {
                    if (ch >= '@' && ch <= '~')
                    {
                        csiBuffer.Append(ch);
                        ApplyCsi(csiBuffer.ToString());
                        csiBuffer.Length = 0;
                        csiPending = false;
                    }
                    else
                    {
                        csiBuffer.Append(ch);
                    }
                    continue;
                }

                if (escapePending)
                {
                    escapePending = false;
                    if (ch == '[')
                    {
                        csiPending = true;
                        csiBuffer.Length = 0;
                    }
                    else if (ch == ']')
                    {
                        oscPending = true;
                        oscEscapePending = false;
                        oscBuffer.Length = 0;
                    }
                    else if (ch == 'c')
                    {
                        Clear();
                    }
                    continue;
                }

                if (ch == 0x1b)
                {
                    escapePending = true;
                    continue;
                }
                if (ch == '\ufeff')
                {
                    continue;
                }

                switch (ch)
                {
                    case '\r':
                        cursorCol = 0;
                        clearLineOnNextPrint = true;
                        break;
                    case '\n':
                        cursorRow++;
                        cursorCol = 0;
                        clearLineOnNextPrint = false;
                        EnsureLine(cursorRow);
                        TrimScrollback();
                        break;
                    case '\b':
                        if (cursorCol > 0)
                        {
                            cursorCol--;
                        }
                        break;
                    case '\t':
                        int spaces = 4 - (cursorCol % 4);
                        for (int s = 0; s < spaces; s++)
                        {
                            PutChar(' ');
                        }
                        break;
                    default:
                        if (!Char.IsControl(ch))
                        {
                            PutChar(ch);
                        }
                        break;
                }
            }
        }

        public string GetText(out int cursorIndex)
        {
            StringBuilder result = new StringBuilder();
            EnsureLine(cursorRow);
            cursorIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                string lineText = lines[i].ToString().TrimEnd();
                if (i == cursorRow)
                {
                    int safeCursorCol = Math.Max(0, cursorCol);
                    if (safeCursorCol > lineText.Length)
                    {
                        lineText = lineText.PadRight(safeCursorCol);
                    }
                    cursorIndex = result.Length + safeCursorCol;
                }

                result.Append(lineText);
                if (i < lines.Count - 1)
                {
                    result.Append(Environment.NewLine);
                }
            }
            return result.ToString();
        }

        public string GetText()
        {
            int ignored;
            return GetText(out ignored);
        }

        private void PutChar(char ch)
        {
            EnsureLine(cursorRow);
            StringBuilder line = lines[cursorRow];
            if (clearLineOnNextPrint && cursorCol == 0)
            {
                line.Length = 0;
                clearLineOnNextPrint = false;
            }
            while (line.Length < cursorCol)
            {
                line.Append(' ');
            }

            if (cursorCol < line.Length)
            {
                line[cursorCol] = ch;
            }
            else
            {
                line.Append(ch);
            }

            cursorCol++;
            if (cursorCol >= columns)
            {
                cursorRow++;
                cursorCol = 0;
                EnsureLine(cursorRow);
                TrimScrollback();
            }
        }

        private void EnsureLine(int row)
        {
            while (lines.Count <= row)
            {
                lines.Add(new StringBuilder());
            }
        }

        private void TrimScrollback()
        {
            int maxLines = Math.Max(rows * 8, 800);
            while (lines.Count > maxLines)
            {
                lines.RemoveAt(0);
                cursorRow = Math.Max(0, cursorRow - 1);
            }
        }

        private void ApplyCsi(string sequence)
        {
            if (String.IsNullOrEmpty(sequence))
            {
                return;
            }

            char command = sequence[sequence.Length - 1];
            string paramText = sequence.Substring(0, sequence.Length - 1);
            string[] parts = paramText.Length == 0 ? new string[0] : paramText.Split(';');
            int p1 = parts.Length > 0 && parts[0].Length > 0 ? SafeParse(parts[0], 1) : 1;
            int p2 = parts.Length > 1 && parts[1].Length > 0 ? SafeParse(parts[1], 1) : 1;

            switch (command)
            {
                case 'A':
                    cursorRow = Math.Max(0, cursorRow - p1);
                    break;
                case 'B':
                    cursorRow += p1;
                    EnsureLine(cursorRow);
                    break;
                case 'C':
                    cursorCol += p1;
                    break;
                case 'D':
                    cursorCol = Math.Max(0, cursorCol - p1);
                    break;
                case 'H':
                case 'f':
                    cursorRow = Math.Max(0, p1 - 1);
                    cursorCol = Math.Max(0, p2 - 1);
                    clearLineOnNextPrint = false;
                    EnsureLine(cursorRow);
                    break;
                case 'J':
                    if (paramText == "2" || paramText == "3")
                    {
                        Clear();
                    }
                    break;
                case 'K':
                    EnsureLine(cursorRow);
                    StringBuilder line = lines[cursorRow];
                    if (cursorCol < line.Length)
                    {
                        line.Length = cursorCol;
                    }
                    clearLineOnNextPrint = false;
                    break;
                case 'm':
                    break;
            }
        }

        private int SafeParse(string text, int fallback)
        {
            int value;
            return Int32.TryParse(text, out value) ? value : fallback;
        }
    }

    public sealed class PseudoConsoleSession : IDisposable
    {
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll")]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        public Process Process { get; private set; }
        public StreamWriter InputWriter { get; private set; }
        public StreamReader OutputReader { get; private set; }
        public VirtualTerminalBuffer Buffer { get; private set; }

        private IntPtr pseudoConsole;
        private IntPtr processHandle;
        private IntPtr threadHandle;
        private SafeFileHandle inputHandle;
        private SafeFileHandle outputHandle;

        public void Start(string applicationPath, string arguments, string workingDirectory, int columns, int rows)
        {
            Dispose();

            SECURITY_ATTRIBUTES attributes = new SECURITY_ATTRIBUTES();
            attributes.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
            attributes.bInheritHandle = true;

            IntPtr inputRead;
            IntPtr inputWrite;
            IntPtr outputRead;
            IntPtr outputWrite;
            if (!CreatePipe(out inputRead, out inputWrite, ref attributes, 0))
            {
                throw new InvalidOperationException("CreatePipe input failed.");
            }
            if (!CreatePipe(out outputRead, out outputWrite, ref attributes, 0))
            {
                CloseHandle(inputRead);
                CloseHandle(inputWrite);
                throw new InvalidOperationException("CreatePipe output failed.");
            }

            COORD size = new COORD();
            size.X = (short)Math.Max(40, columns);
            size.Y = (short)Math.Max(10, rows);
            int hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out pseudoConsole);
            CloseHandle(inputRead);
            CloseHandle(outputWrite);
            if (hr != 0)
            {
                CloseHandle(inputWrite);
                CloseHandle(outputRead);
                throw new InvalidOperationException("CreatePseudoConsole failed: " + hr);
            }

            IntPtr attributeListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            IntPtr attributeList = Marshal.AllocHGlobal(attributeListSize);
            try
            {
                InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize);
                IntPtr attribute = new IntPtr(PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE);
                if (!UpdateProcThreadAttribute(attributeList, 0, attribute, pseudoConsole, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new InvalidOperationException("UpdateProcThreadAttribute failed.");
                }

                STARTUPINFOEX startupInfo = new STARTUPINFOEX();
                startupInfo.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
                startupInfo.lpAttributeList = attributeList;

                PROCESS_INFORMATION processInfo;
                StringBuilder commandLineBuffer = new StringBuilder();
                commandLineBuffer.Append('"').Append(applicationPath).Append('"');
                if (!String.IsNullOrWhiteSpace(arguments))
                {
                    commandLineBuffer.Append(' ').Append(arguments);
                }
                bool created = CreateProcess(
                    applicationPath,
                    commandLineBuffer,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out processInfo);
                if (!created)
                {
                    throw new InvalidOperationException("CreateProcess failed: " + Marshal.GetLastWin32Error());
                }

                processHandle = processInfo.hProcess;
                threadHandle = processInfo.hThread;
                try
                {
                    Process = Process.GetProcessById(processInfo.dwProcessId);
                }
                catch
                {
                    Process = null;
                }
            }
            finally
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            inputHandle = new SafeFileHandle(inputWrite, true);
            outputHandle = new SafeFileHandle(outputRead, true);
            InputWriter = new StreamWriter(new FileStream(inputHandle, FileAccess.Write, 4096, false), new UTF8Encoding(false));
            InputWriter.AutoFlush = true;
            OutputReader = new StreamReader(new FileStream(outputHandle, FileAccess.Read, 4096, false), Encoding.UTF8);
            Buffer = new VirtualTerminalBuffer(size.X, size.Y);
        }

        public void Resize(int columns, int rows)
        {
            if (pseudoConsole == IntPtr.Zero)
            {
                return;
            }

            COORD size = new COORD();
            size.X = (short)Math.Max(40, columns);
            size.Y = (short)Math.Max(10, rows);
            ResizePseudoConsole(pseudoConsole, size);
            if (Buffer != null)
            {
                Buffer.Resize(size.X, size.Y);
            }
        }

        public void Dispose()
        {
            if (InputWriter != null)
            {
                InputWriter.Dispose();
                InputWriter = null;
            }
            if (OutputReader != null)
            {
                OutputReader.Dispose();
                OutputReader = null;
            }
            if (inputHandle != null)
            {
                inputHandle.Dispose();
                inputHandle = null;
            }
            if (outputHandle != null)
            {
                outputHandle.Dispose();
                outputHandle = null;
            }
            if (threadHandle != IntPtr.Zero)
            {
                CloseHandle(threadHandle);
                threadHandle = IntPtr.Zero;
            }
            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }
            if (pseudoConsole != IntPtr.Zero)
            {
                ClosePseudoConsole(pseudoConsole);
                pseudoConsole = IntPtr.Zero;
            }
            if (Process != null)
            {
                Process.Dispose();
                Process = null;
            }
            Buffer = null;
        }
    }

    public sealed class TerminalView : Control
    {
        private string[] lines = new string[0];
        private int cursorRow;
        private int cursorCol;

        public TerminalView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(12, 12, 12);
            ForeColor = Color.FromArgb(236, 236, 236);
            Font = new Font("Consolas", 9F);
            TabStop = true;
        }

        public void SetSnapshot(string[] lines, int cursorRow, int cursorCol)
        {
            this.lines = lines ?? new string[0];
            this.cursorRow = Math.Max(0, cursorRow);
            this.cursorCol = Math.Max(0, cursorCol);
            Invalidate();
        }

        public string GetAllText()
        {
            return lines == null ? "" : String.Join(Environment.NewLine, lines);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int lineHeight = Math.Max(Font.Height + 2, 14);
            int visibleRows = Math.Max(1, ClientSize.Height / lineHeight);
            int totalRows = lines == null ? 0 : lines.Length;
            int startRow = Math.Max(0, totalRows - visibleRows);

            for (int i = 0; i < visibleRows && startRow + i < totalRows; i++)
            {
                string text = lines[startRow + i] ?? "";
                Rectangle bounds = new Rectangle(0, i * lineHeight, ClientSize.Width, lineHeight);
                TextRenderer.DrawText(e.Graphics, text, Font, bounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            if (cursorRow >= startRow && cursorRow < startRow + visibleRows)
            {
                int drawRow = cursorRow - startRow;
                string lineText = cursorRow < totalRows ? (lines[cursorRow] ?? "") : "";
                int safeCol = Math.Max(0, Math.Min(cursorCol, lineText.Length));
                string prefix = safeCol > 0 && safeCol <= lineText.Length ? lineText.Substring(0, safeCol) : "";
                int caretX = 0;
                if (prefix.Length > 0)
                {
                    caretX = TextRenderer.MeasureText(e.Graphics, prefix, Font, new Size(Int32.MaxValue, lineHeight), TextFormatFlags.NoPadding).Width - 1;
                }
                int caretY = drawRow * lineHeight + 1;
                using (Pen pen = new Pen(Color.FromArgb(255, 244, 160), 1.6f))
                {
                    e.Graphics.DrawLine(pen, caretX, caretY, caretX, caretY + lineHeight - 3);
                }
            }
        }
    }

    public sealed class MainForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativeRect lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private const int DevicePanelHeight = 144;
        private const int WorkAreaHeight = 300;
        private const int QuickToolsHeight = 170;
        private const int QuickToolsMinVisibleHeight = QuickToolsHeight / 2;
        private const int QuickToolsToolbarRowHeight = 36;
        private const int QuickToolsToolbarAutoHideThreshold = 100;
        private const int DevicePanelMinHeight = 90;
        private const int CmdPanelMinHeight = 207;
        private const int OutputPanelDefaultHeight = 80;
        private const int OutputPanelMinVisibleHeight = 55;
        private const int EmGetRect = 0x00B2;
        private const int EmSetRectNp = 0x00B4;
        private const int EmSetCueBanner = 0x1501;
        private const int EmGetScrollPos = 0x04DD;
        private const int EmSetScrollPos = 0x04DE;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const int WmSetRedraw = 0x000B;
        private const int WmVScroll = 0x0115;
        private const int SbBottom = 7;
        private const int FixedCmdColumns = 120;
        private const int TerminalViewportBottomInset = 8;
        private const char CmdMarkerLeft = '\ue000';
        private const char CmdMarkerRight = '\ue001';
        private const char CmdMarkerDelete = '\ue002';
        private const char CmdMarkerClearLineTail = '\ue003';
        private const string TerminalTailPaddingText = "\r\n";
        private const int LanScanPort = 5555;
        private const int LanScanProbeTimeoutMs = 220;
        private const int LanScanMaxDegreeOfParallelism = 64;
        private const int LanScanMaxHostsPerSubnet = 4096;
        private const string ScrcpySameLanHighQualityArguments = "--max-size=0 --video-bit-rate=32M";
        private static readonly Color DefaultCmdBackgroundColor = Color.FromArgb(12, 12, 12);
        private static readonly Color DefaultCmdOutputColor = Color.FromArgb(218, 218, 218);
        private static readonly Color DefaultCmdPromptColor = Color.FromArgb(220, 72, 72);
        private static readonly Color DefaultCmdInputColor = Color.FromArgb(98, 209, 148);
        private static readonly string[] CmdBasicCompletionCommands = new string[]
        {
            "assoc", "attrib", "break", "call", "cd", "chdir", "cls", "color", "copy", "date",
            "del", "dir", "echo", "endlocal", "erase", "exit", "for", "ftype", "goto", "help",
            "if", "md", "mkdir", "mklink", "move", "path", "pause", "popd", "prompt", "pushd",
            "rd", "rem", "ren", "rename", "rmdir", "set", "setlocal", "shift", "start", "time",
            "title", "type", "ver", "verify", "vol", "where"
        };
        private static readonly string[] AdbShellBuiltinCompletionCommands = new string[]
        {
            "alias", "bg", "cd", "exit", "export", "fg", "jobs", "pwd", "set", "unset"
        };
        private static readonly TimeSpan AdbShellCommandCompletionCacheLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan AdbShellDirectoryCompletionCacheLifetime = TimeSpan.FromSeconds(8);
        private readonly string configPath;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly List<TerminalSession> terminalSessions = new List<TerminalSession>();
        private AppConfig config;

        private DataGridView devicesGrid;
        private RichTextBox outputBox;
        private TabControl mainTabs;
        private TabControl terminalTabs;
        private TextBox shellConnectTextBox;
        private Panel workAreaHost;
        private SplitContainer workAreaSplit;
        private SplitContainer lowerPanelSplit;
        private SplitContainer bottomPanelSplit;
        private SplitContainer mainMiddleBottomSplit;
        private Panel mainContentHost;
        private ContextMenuStrip devicesMenu;
        private ToolStripMenuItem deviceFreezeRestoreMenuItem;
        private ToolStripMenuItem deviceRebootMenuItem;
        private ToolStripMenuItem deviceInstallAppMenuItem;
        private ToolStripMenuItem deviceDeleteMenuItem;
        private ToolStripMenuItem deviceScrcpyHighQualityMenuItem;
        private int deviceContextRowIndex = -1;
        private string deviceContextColumnName;
        private FlowLayoutPanel quickToolsPanel;
        private FlowLayoutPanel quickToolsTopPanel;
        private TableLayoutPanel quickToolsLayout;
        private Panel quickToolEditorPanel;
        private Panel quickFilterPanel;
        private ComboBox quickFilterComboBox;
        private TextBox quickNameTextBox;
        private TextBox quickCommandTextBox;
        private ComboBox quickTypeComboBox;
        private bool quickTypeSelectionTouched;
        private bool quickTypeSelectionUpdating;
        private Point quickDragStartPoint;
        private DateTime quickToolSuppressClicksUntilUtc = DateTime.MinValue;

        private TextBox remotePathTextBox;
        private Label selectedDeviceHeaderLabel;
        private Button managementToggleButton;
        private ToolTip managementToggleToolTip;
        private DataGridView filesGrid;
        private ContextMenuStrip filesMenu;
        private System.Windows.Forms.Timer filesRightClickTimer;
        private Point pendingFilesMenuLocation;
        private PictureBox screenshotPictureBox;
        private ListBox screenshotListBox;
        private ContextMenuStrip screenshotsMenu;

        private ComboBox appFilterComboBox;
        private DataGridView appsGrid;
        private ContextMenuStrip appsMenu;
        private ToolStripMenuItem installAppMenuItem;
        private ToolStripMenuItem appFreezeEnableMenuItem;

        private RichTextBox cmdOutputBox;
        private ContextMenuStrip cmdContextMenu;
        private ContextMenuStrip outputContextMenu;
        private string cmdWorkingDirectory;
        private PseudoConsoleSession fixedCmdSession;
        private bool fixedCmdForcePassthrough;
        private bool fixedCmdAdbShellLocalMode;
        private int fixedCmdAdbShellInputStartIndex = -1;
        private string fixedCmdAdbShellSerial;
        private bool cmdWheelScrolledAwayFromBottom;
        private int cmdLastCursorIndex;
        private readonly object cmdPendingOutputLock = new object();
        private readonly StringBuilder cmdPendingOutput = new StringBuilder();
        private readonly StringBuilder cmdCurrentInput = new StringBuilder();
        private readonly List<string> cmdInputHistory = new List<string>();
        private readonly List<string> cmdAdbShellInputHistory = new List<string>();
        private readonly object adbShellCompletionCacheLock = new object();
        private readonly Dictionary<string, AdbShellCompletionCacheEntry> adbShellCommandCompletionCache = new Dictionary<string, AdbShellCompletionCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AdbShellCompletionCacheEntry> adbShellDirectoryCompletionCache = new Dictionary<string, AdbShellCompletionCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> adbShellCommandCompletionWarmups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> adbShellDirectoryCompletionWarmups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object embeddedShellDebugLogLock = new object();
        private int cmdCurrentInputCursor;
        private int cmdHistoryIndex = -1;
        private string cmdHistoryDraft = "";
        private int cmdAdbShellHistoryIndex = -1;
        private string cmdAdbShellHistoryDraft = "";
        private bool cmdFlushScheduled;
        private bool cmdSuppressLeadingBlankAfterClear;
        private bool cmdEscapePending;
        private bool cmdCsiPending;
        private bool cmdOscPending;
        private bool cmdOscEscapePending;
        private readonly StringBuilder cmdControlBuffer = new StringBuilder();
        private Color cmdBackgroundColor;
        private Color cmdOutputColor;
        private Color cmdPromptColor;
        private Color cmdInputColor;
        private string embeddedShellDebugLogPath;
        private List<FileEntry> currentFileEntries = new List<FileEntry>();
        private int fileRefreshRequestId;
        private System.Windows.Forms.Timer selectedDeviceDataRefreshTimer;
        private string lastSelectedDeviceDataSerial = "";
        private string pendingSelectedDeviceDataSerial = "";
        private bool lastSelectedDeviceDataWasFrozen;
        private string fileSortColumn = "Name";
        private bool fileSortAscending = true;
        private FileEntry fileClipboardEntry;
        private string fileClipboardOperation;
        private int deviceRefreshRequestId;
        private readonly object deletedDeviceSerialsLock = new object();
        private readonly HashSet<string> deletedDeviceSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastManualDeviceRefreshUtc = DateTime.MinValue;
        private readonly Dictionary<string, System.Windows.Forms.Timer> cmdAdbDeviceRefreshTimers = new Dictionary<string, System.Windows.Forms.Timer>(StringComparer.OrdinalIgnoreCase);
        private int appRefreshRequestId;
        private ToolTip railButtonToolTip;
        private int lanScanInProgressFlag;
        private int fileTransferInProgressFlag;
        private string outputProgressKey;
        private int outputProgressLineStart = -1;

        private sealed class AdbShellCompletionCacheEntry
        {
            public List<string> Items { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private sealed class QuickToolSwitchPair
        {
            public string DisplayName { get; set; }
            public string Type { get; set; }
            public QuickTool OnTool { get; set; }
            public QuickTool OffTool { get; set; }
        }

        private sealed class AdbFileTransferItem
        {
            public string Serial { get; set; }
            public string Operation { get; set; }
            public string Arguments { get; set; }
            public string Source { get; set; }
            public string Target { get; set; }
            public string DisplayName { get; set; }
            public long TotalBytes { get; set; }
        }

        private sealed class ApkInstallItem
        {
            public string LocalPath { get; set; }
            public string RemotePath { get; set; }
            public string DisplayName { get; set; }
        }

        private sealed class AdbFileTransferProgress
        {
            public readonly object SyncRoot = new object();
            public string Key { get; set; }
            public string Operation { get; set; }
            public int Index { get; set; }
            public int Total { get; set; }
            public string DisplayName { get; set; }
            public int Percent { get; set; }
            public long TotalBytes { get; set; }
            public long TransferredBytes { get; set; }
            public long LastSampleBytes { get; set; }
            public long SpeedBytesPerSecond { get; set; }
            public DateTime LastSampleUtc { get; set; }
            public DateTime LastUiUpdateUtc { get; set; }
            public DateTime LastObservedCheckUtc { get; set; }
            public string LocalProgressPath { get; set; }
            public string RemoteProgressSerial { get; set; }
            public string RemoteProgressPath { get; set; }
            public string AdbPath { get; set; }
        }

        public MainForm()
        {
            Text = "ADB Tool (ASN)";
            Width = 1280;
            Height = 1035;
            MinimumSize = new Size(1100, 880);
            StartPosition = FormStartPosition.CenterScreen;
            ApplyApplicationIcon();

            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            config = LoadConfig();
            if (FillDefaultsIfEmpty())
            {
                SaveConfig();
            }
            InitializeEmbeddedShellDebugLog();
            LoadCmdColorsFromConfig();
            BuildUi();
            StartFixedCmdSession();
            Shown += delegate
            {
                BeginInvoke((Action)delegate
                {
                    InitializeBottomLayout();
                    BindQuickTools();
                    RefreshScreenshots();
                    RefreshDevices();
                    UpdateSelectedDeviceHeader();
                    PositionSelectedDeviceHeader();
                });
            };
        }

        private void ApplyApplicationIcon()
        {
            try
            {
                using (Icon associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (associatedIcon != null)
                    {
                        Icon = (Icon)associatedIcon.Clone();
                    }
                }
            }
            catch
            {
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (System.Windows.Forms.Timer timer in cmdAdbDeviceRefreshTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            cmdAdbDeviceRefreshTimers.Clear();
            for (int i = 0; i < terminalSessions.Count; i++)
            {
                StopTerminalProcess(terminalSessions[i]);
            }
            if (fixedCmdSession != null)
            {
                fixedCmdSession.Dispose();
                fixedCmdSession = null;
            }
            base.OnFormClosing(e);
        }

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    AppConfig loaded = serializer.Deserialize<AppConfig>(File.ReadAllText(configPath, Encoding.UTF8));
                    if (loaded != null)
                    {
                        if (loaded.QuickTools == null)
                        {
                            loaded.QuickTools = new List<QuickTool>();
                        }
                        if (loaded.ScrcpyHighQualityDisabledSerials == null)
                        {
                            loaded.ScrcpyHighQualityDisabledSerials = new List<string>();
                        }
                        if (loaded.ScrcpyHighQualityForcedSerials == null)
                        {
                            loaded.ScrcpyHighQualityForcedSerials = new List<string>();
                        }
                        return loaded;
                    }
                }
            }
            catch
            {
            }

            return new AppConfig();
        }

        private void SaveConfig()
        {
            if (String.IsNullOrWhiteSpace(config.ScreenshotDir))
            {
                config.ScreenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
            }
            if (String.IsNullOrWhiteSpace(config.DownloadDir))
            {
                config.DownloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
            }
            if (String.IsNullOrWhiteSpace(config.CmdBackgroundColor))
            {
                config.CmdBackgroundColor = ColorToConfig(DefaultCmdBackgroundColor);
            }
            if (String.IsNullOrWhiteSpace(config.CmdOutputColor))
            {
                config.CmdOutputColor = ColorToConfig(DefaultCmdOutputColor);
            }
            if (String.IsNullOrWhiteSpace(config.CmdPromptColor))
            {
                config.CmdPromptColor = ColorToConfig(DefaultCmdPromptColor);
            }
            if (String.IsNullOrWhiteSpace(config.CmdInputColor))
            {
                config.CmdInputColor = ColorToConfig(DefaultCmdInputColor);
            }
            if (config.ScrcpyHighQualityDisabledSerials == null)
            {
                config.ScrcpyHighQualityDisabledSerials = new List<string>();
            }
            if (config.ScrcpyHighQualityForcedSerials == null)
            {
                config.ScrcpyHighQualityForcedSerials = new List<string>();
            }

            File.WriteAllText(configPath, PrettyPrintJson(serializer.Serialize(config)), Encoding.UTF8);
            AppendOutput("配置已保存: " + configPath);
        }

        private void BuildUi()
        {
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            cmdWorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, DevicePanelHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Padding = new Padding(10);
            Controls.Add(root);

            root.Controls.Add(BuildSettingsRail(), 0, 0);
            root.SetRowSpan(root.GetControlFromPosition(0, 0), 2);
            root.Controls.Add(BuildDevicePanel(), 1, 0);
            root.Controls.Add(BuildMainContent(), 1, 1);
        }

        private Control BuildMainContent()
        {
            mainContentHost = new Panel();
            mainContentHost.Dock = DockStyle.Fill;

            SplitContainer middleSplit = new SplitContainer();
            mainMiddleBottomSplit = middleSplit;
            middleSplit.Dock = DockStyle.Fill;
            middleSplit.Orientation = Orientation.Horizontal;
            middleSplit.SplitterWidth = 8;
            middleSplit.SizeChanged += delegate
            {
                if (!IsHandleCreated || !middleSplit.Visible || middleSplit.Height <= 0)
                {
                    return;
                }

                UpdateShellResizeLowerBounds();
                ApplySplitterDistance(middleSplit, middleSplit.SplitterDistance);
            };
            middleSplit.Panel1.Controls.Add(BuildWorkArea());
            middleSplit.Panel2.Controls.Add(BuildLowerPanel());
            mainContentHost.Controls.Add(middleSplit);
            return mainContentHost;
        }

        private Control BuildLowerPanel()
        {
            SplitContainer split = new SplitContainer();
            lowerPanelSplit = split;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterWidth = 8;
            split.SizeChanged += delegate
            {
                if (!IsHandleCreated || !split.Visible || split.Height <= 0)
                {
                    return;
                }

                int maxDistance = Math.Max(split.Panel1MinSize, split.Height - split.Panel2MinSize - split.SplitterWidth);
                if (split.SplitterDistance < split.Panel1MinSize || split.SplitterDistance > maxDistance)
                {
                    PositionLowerSplitter(split);
                }

                UpdateShellResizeLowerBounds();
            };

            split.Panel1.Controls.Add(BuildQuickToolsPanel());
            split.Panel2.Controls.Add(BuildBottomPanel());
            return split;
        }

        private void PositionLowerSplitter(SplitContainer split)
        {
            if (split == null || split.Height <= 0)
            {
                return;
            }

            if (split.Height <= split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth)
            {
                return;
            }

            int distance = QuickToolsHeight;
            int minDistance = split.Panel1MinSize;
            int maxDistance = Math.Max(minDistance, split.Height - split.Panel2MinSize - split.SplitterWidth);
            if (distance < minDistance)
            {
                distance = minDistance;
            }
            if (distance > maxDistance)
            {
                distance = maxDistance;
            }
            split.SplitterDistance = distance;
        }

        private Control BuildBottomPanel()
        {
            SplitContainer split = new SplitContainer();
            bottomPanelSplit = split;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterWidth = 8;
            split.SizeChanged += delegate
            {
                if (!IsHandleCreated || !split.Visible || split.Height <= 0)
                {
                    return;
                }

                int minimumDistance = Math.Max(0, split.Height - split.Panel2MinSize - split.SplitterWidth);
                if (split.SplitterDistance < split.Panel1MinSize || split.SplitterDistance > minimumDistance)
                {
                    PositionBottomSplitter(split);
                }

                UpdateShellResizeLowerBounds();
            };
            split.SplitterMoved += delegate { UpdateShellResizeLowerBounds(); };

            split.Panel1.Controls.Add(BuildCmdPanel());
            split.Panel2.Controls.Add(BuildOutputPanel());
            return split;
        }

        private void InitializeBottomLayout()
        {
            if (lowerPanelSplit != null)
            {
                lowerPanelSplit.FixedPanel = FixedPanel.Panel2;
                lowerPanelSplit.IsSplitterFixed = false;
                lowerPanelSplit.Panel1MinSize = QuickToolsMinVisibleHeight;
            }

            if (bottomPanelSplit != null)
            {
                bottomPanelSplit.FixedPanel = FixedPanel.Panel2;
                bottomPanelSplit.IsSplitterFixed = false;
                bottomPanelSplit.Panel1MinSize = CmdPanelMinHeight;
                bottomPanelSplit.Panel2MinSize = OutputPanelMinVisibleHeight;
            }

            if (mainMiddleBottomSplit != null)
            {
                mainMiddleBottomSplit.FixedPanel = FixedPanel.None;
                mainMiddleBottomSplit.IsSplitterFixed = false;
                mainMiddleBottomSplit.Panel1MinSize = WorkAreaHeight;
            }

            UpdateShellResizeLowerBounds();
            EnsureMainContentSplitBounds(true);
            PositionLowerSplitter(lowerPanelSplit);
            PositionBottomSplitter(bottomPanelSplit);
        }

        private void UpdateQuickToolsToolbarVisibility(Control host)
        {
            if (host == null || host.IsDisposed || quickToolsLayout == null || quickToolsTopPanel == null || quickToolsLayout.RowStyles.Count == 0)
            {
                return;
            }

            bool shouldHide = host.ClientSize.Height < QuickToolsToolbarAutoHideThreshold;
            RowStyle rowStyle = quickToolsLayout.RowStyles[0];
            if (rowStyle.SizeType != SizeType.Absolute)
            {
                rowStyle.SizeType = SizeType.Absolute;
            }

            float targetHeight = shouldHide ? 0F : QuickToolsToolbarRowHeight;
            if (Math.Abs(rowStyle.Height - targetHeight) > 0.1f)
            {
                rowStyle.Height = targetHeight;
            }

            bool targetVisible = !shouldHide;
            if (quickToolsTopPanel.Visible != targetVisible)
            {
                quickToolsTopPanel.Visible = targetVisible;
            }
        }

        private void PositionBottomSplitter(SplitContainer split)
        {
            if (split == null || split.Height <= 0)
            {
                return;
            }

            if (split.Height <= split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth)
            {
                return;
            }

            int distance = split.Height - OutputPanelDefaultHeight - split.SplitterWidth;
            int minDistance = split.Panel1MinSize;
            int maxDistance = Math.Max(minDistance, split.Height - split.Panel2MinSize - split.SplitterWidth);
            if (distance < minDistance)
            {
                distance = minDistance;
            }
            if (distance > maxDistance)
            {
                distance = maxDistance;
            }
            if (distance >= 0)
            {
                split.SplitterDistance = distance;
            }
        }

        private int GetLowerPanelMinimumHeight()
        {
            int quickToolsMin = lowerPanelSplit == null ? 120 : lowerPanelSplit.Panel1MinSize;
            int lowerSplitterWidth = lowerPanelSplit == null ? 8 : lowerPanelSplit.SplitterWidth;
            return quickToolsMin + lowerSplitterWidth + GetBottomPanelMinimumHeightForShellResize();
        }

        private int GetBottomPanelMinimumHeightForShellResize()
        {
            int cmdMin = bottomPanelSplit == null ? CmdPanelMinHeight : bottomPanelSplit.Panel1MinSize;
            int outputReserved = GetReservedOutputHeightForShellResize();
            int bottomSplitterWidth = bottomPanelSplit == null ? 8 : bottomPanelSplit.SplitterWidth;
            return cmdMin + outputReserved + bottomSplitterWidth;
        }

        private int GetReservedOutputHeightForShellResize()
        {
            if (bottomPanelSplit == null || bottomPanelSplit.IsDisposed)
            {
                return OutputPanelDefaultHeight;
            }

            int panel2Height = bottomPanelSplit.Panel2 == null ? 0 : bottomPanelSplit.Panel2.Height;
            if (panel2Height <= 0)
            {
                return Math.Max(bottomPanelSplit.Panel2MinSize, OutputPanelDefaultHeight);
            }

            return Math.Max(bottomPanelSplit.Panel2MinSize, panel2Height);
        }

        private void UpdateShellResizeLowerBounds()
        {
            if (lowerPanelSplit != null && !lowerPanelSplit.IsDisposed)
            {
                lowerPanelSplit.Panel2MinSize = GetBottomPanelMinimumHeightForShellResize();
            }

            if (mainMiddleBottomSplit != null && !mainMiddleBottomSplit.IsDisposed)
            {
                mainMiddleBottomSplit.Panel2MinSize = GetLowerPanelMinimumHeight();
            }
        }

        private void EnsureMainContentSplitBounds(bool forceDefaultDistance)
        {
            if (mainMiddleBottomSplit != null)
            {
                ApplySplitterDistance(mainMiddleBottomSplit, forceDefaultDistance ? WorkAreaHeight : mainMiddleBottomSplit.SplitterDistance);
            }
        }

        private void ApplySplitterDistance(SplitContainer split, int desiredDistance)
        {
            int minDistance;
            int maxDistance;
            if (!TryGetSplitterDistanceRange(split, out minDistance, out maxDistance))
            {
                return;
            }

            int clamped = desiredDistance;
            if (clamped < minDistance)
            {
                clamped = minDistance;
            }
            if (clamped > maxDistance)
            {
                clamped = maxDistance;
            }

            if (clamped >= 0 && split.SplitterDistance != clamped)
            {
                try
                {
                    split.SplitterDistance = clamped;
                }
                catch (InvalidOperationException)
                {
                    // 窗口快速缩放时 SplitContainer 可能短暂没有合法距离，跳过本次布局。
                }
            }
        }

        private bool TryGetSplitterDistanceRange(SplitContainer split, out int minDistance, out int maxDistance)
        {
            minDistance = 0;
            maxDistance = -1;
            if (split == null || split.IsDisposed)
            {
                return false;
            }

            int availableLength = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            if (availableLength <= 0)
            {
                return false;
            }

            minDistance = Math.Max(0, split.Panel1MinSize);
            maxDistance = availableLength - Math.Max(0, split.Panel2MinSize);
            return maxDistance >= minDistance;
        }

        private Control BuildSettingsRail()
        {
            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.ColumnCount = 1;
            bar.RowCount = 6;
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            railButtonToolTip = new ToolTip();
            railButtonToolTip.ShowAlways = true;
            Button refreshButton = MakeRailButton("⟳", RefreshDevicesFromButton);
            refreshButton.AccessibleName = "刷新设备";
            refreshButton.Font = new Font(refreshButton.Font.FontFamily, 14F, FontStyle.Bold, GraphicsUnit.Point);
            railButtonToolTip.SetToolTip(refreshButton, "刷新设备");
            bar.Controls.Add(refreshButton, 0, 0);
            Button scanButton = MakeRailButton("⌕", StartLanDeviceScan);
            scanButton.AccessibleName = "局域网扫描";
            scanButton.Font = new Font(scanButton.Font.FontFamily, 13F, FontStyle.Bold, GraphicsUnit.Point);
            railButtonToolTip.SetToolTip(scanButton, "局域网扫描");
            scanButton.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                {
                    ConfigureLanScanSubnets();
                }
            };
            bar.Controls.Add(scanButton, 0, 1);
            Button settingsButton = MakeRailButton("⚙", ShowSettingsDialog);
            settingsButton.AccessibleName = "设置";
            railButtonToolTip.SetToolTip(settingsButton, "路径设置");
            bar.Controls.Add(settingsButton, 0, 2);
            Button colorsButton = MakeRailButton("◐", ShowCmdColorDialog);
            colorsButton.AccessibleName = "CMD 配色";
            colorsButton.Font = new Font(colorsButton.Font.FontFamily, 13F, FontStyle.Bold, GraphicsUnit.Point);
            railButtonToolTip.SetToolTip(colorsButton, "配色设置");
            bar.Controls.Add(colorsButton, 0, 3);
            Button aboutButton = MakeRailButton("i", ShowAboutDialog);
            aboutButton.AccessibleName = "关于";
            aboutButton.Font = new Font(aboutButton.Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            railButtonToolTip.SetToolTip(aboutButton, "关于");
            bar.Controls.Add(aboutButton, 0, 5);
            return bar;
        }

        private Control BuildDevicePanel()
        {
            GroupBox group = new GroupBox();
            group.Text = "ADB 设备";
            group.Dock = DockStyle.Fill;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8);
            group.Controls.Add(layout);

            devicesGrid = new DataGridView();
            devicesGrid.Dock = DockStyle.Fill;
            devicesGrid.AllowUserToAddRows = false;
            devicesGrid.AllowUserToDeleteRows = false;
            devicesGrid.ReadOnly = true;
            devicesGrid.MultiSelect = false;
            devicesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            devicesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            devicesGrid.RowHeadersVisible = false;
            devicesGrid.ScrollBars = ScrollBars.Vertical;
            devicesGrid.Columns.Add("Serial", "序列号 / IP");
            devicesGrid.Columns.Add("State", "状态");
            devicesGrid.Columns.Add("Model", "型号");
            devicesGrid.Columns.Add("NetworkType", "网络");
            devicesGrid.Columns.Add("EthernetIp", "以太网 IP");
            devicesGrid.Columns.Add("EthernetMac", "以太网 MAC");
            devicesGrid.Columns.Add("BluetoothMac", "蓝牙 MAC");
            devicesGrid.Columns.Add("AndroidVersion", "安卓系统版本");
            devicesGrid.CellMouseDown += OnDevicesGridCellMouseDown;
            devicesGrid.SelectionChanged += delegate { OnSelectedDeviceChanged(); };
            devicesGrid.CellDoubleClick += delegate { OpenScrcpy(); };
            devicesGrid.ContextMenuStrip = BuildDeviceMenu();

            selectedDeviceDataRefreshTimer = new System.Windows.Forms.Timer();
            selectedDeviceDataRefreshTimer.Interval = 250;
            selectedDeviceDataRefreshTimer.Tick += delegate { RefreshSelectedDeviceDataAfterSelectionChange(); };

            layout.Controls.Add(devicesGrid, 0, 0);
            return group;
        }

        private ContextMenuStrip BuildDeviceMenu()
        {
            devicesMenu = new ContextMenuStrip();
            devicesMenu.Opening += OnDevicesMenuOpening;
            devicesMenu.Closed += OnDevicesMenuClosed;
            devicesMenu.Items.Add("复制 ADB 连接", null, delegate { CopySelectedAdbConnect(); });
            devicesMenu.Items.Add("复制ADB Shell", null, delegate { CopySelectedAdbShell(); });
            deviceFreezeRestoreMenuItem = new ToolStripMenuItem("冻结设备", null, delegate { FreezeOrRestoreSelectedDevice(); });
            devicesMenu.Items.Add(deviceFreezeRestoreMenuItem);
            deviceRebootMenuItem = new ToolStripMenuItem("重启设备", null, delegate { RebootSelectedDevice(); });
            devicesMenu.Items.Add(deviceRebootMenuItem);
            deviceInstallAppMenuItem = new ToolStripMenuItem("安装应用", null, delegate { PickAndInstallApps(); });
            devicesMenu.Items.Add(deviceInstallAppMenuItem);
            deviceDeleteMenuItem = new ToolStripMenuItem("删除", null, delegate { DeleteSelectedDevice(); });
            devicesMenu.Items.Add(deviceDeleteMenuItem);
            devicesMenu.Items.Add(new ToolStripSeparator());
            deviceScrcpyHighQualityMenuItem = new ToolStripMenuItem("禁用高画质", null, delegate { ToggleSelectedScrcpyHighQuality(); });
            devicesMenu.Items.Add(deviceScrcpyHighQualityMenuItem);
            devicesMenu.Items.Add("打开 scrcpy", null, delegate { OpenScrcpy(); });
            devicesMenu.Items.Add("新建 shell", null, delegate { OpenEmbeddedShell(); });
            devicesMenu.Items.Add("打开系统设置", null, delegate { OpenSettings(); });
            devicesMenu.Items.Add("截图并查看", null, delegate { ShowManagementTab("截图查看", CaptureAndViewScreenshot); });
            devicesMenu.Items.Add("文件浏览器", null, delegate { ShowManagementTab("文件浏览", RefreshFiles); });
            devicesMenu.Items.Add("应用列表", null, delegate { ShowManagementTab("应用列表", RefreshApps); });
            return devicesMenu;
        }

        private void OnDevicesGridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                devicesGrid.ClearSelection();
                devicesGrid.Rows[e.RowIndex].Selected = true;
                if (e.ColumnIndex >= 0)
                {
                    devicesGrid.CurrentCell = devicesGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            }
            else
            {
                devicesGrid.ClearSelection();
            }

            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                deviceContextRowIndex = e.RowIndex;
                deviceContextColumnName = devicesGrid.Columns[e.ColumnIndex].Name;
            }
            else
            {
                deviceContextRowIndex = -1;
                deviceContextColumnName = null;
            }
        }

        private void OnDevicesMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            deviceContextRowIndex = -1;
            deviceContextColumnName = null;
        }

        private void OnDevicesMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (devicesMenu == null)
            {
                return;
            }

            while (devicesMenu.Items.Count > 0 && Convert.ToString(devicesMenu.Items[0].Tag) == "device-copy-cell")
            {
                devicesMenu.Items.RemoveAt(0);
            }

            DeviceInfo selected = SelectedDeviceInfo();
            bool hasSelected = selected != null && !String.IsNullOrWhiteSpace(selected.Serial);
            if (deviceFreezeRestoreMenuItem != null)
            {
                bool canFreeze = IsConnectedDeviceState(selected);
                bool canRestore = IsFrozenDeviceState(selected);
                bool showFreezeRestore = hasSelected && (canFreeze || canRestore);
                deviceFreezeRestoreMenuItem.Visible = showFreezeRestore;
                deviceFreezeRestoreMenuItem.Enabled = showFreezeRestore;
                deviceFreezeRestoreMenuItem.Text = canRestore ? "恢复连接" : "冻结设备";
            }

            if (deviceDeleteMenuItem != null)
            {
                deviceDeleteMenuItem.Visible = hasSelected;
                deviceDeleteMenuItem.Enabled = hasSelected;
            }

            if (deviceInstallAppMenuItem != null)
            {
                bool canInstall = IsConnectedDeviceState(selected);
                deviceInstallAppMenuItem.Visible = hasSelected;
                deviceInstallAppMenuItem.Enabled = canInstall;
            }

            if (deviceRebootMenuItem != null)
            {
                bool canReboot = hasSelected && IsConnectedDeviceState(selected);
                deviceRebootMenuItem.Visible = canReboot;
                deviceRebootMenuItem.Enabled = canReboot;
            }

            if (deviceScrcpyHighQualityMenuItem != null)
            {
                bool canToggleHighQuality = hasSelected && IsConnectedDeviceState(selected);
                deviceScrcpyHighQualityMenuItem.Visible = canToggleHighQuality;
                deviceScrcpyHighQualityMenuItem.Enabled = canToggleHighQuality;
                deviceScrcpyHighQualityMenuItem.Text = GetScrcpyHighQualityMenuText(selected);
            }

            string copyLabel = GetDeviceCellCopyMenuLabel(deviceContextColumnName);
            if (String.IsNullOrWhiteSpace(copyLabel))
            {
                return;
            }

            int targetRowIndex = deviceContextRowIndex;
            string targetColumnName = deviceContextColumnName;
            ToolStripMenuItem item = new ToolStripMenuItem(copyLabel);
            item.Tag = "device-copy-cell";
            item.Click += delegate { CopySelectedDeviceCellValue(targetColumnName, targetRowIndex); };
            devicesMenu.Items.Insert(0, item);
        }

        private Control BuildWorkArea()
        {
            workAreaHost = new Panel();
            workAreaHost.Dock = DockStyle.Fill;

            SplitContainer split = new SplitContainer();
            workAreaSplit = split;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SizeChanged += delegate
            {
                if (!split.Panel2Collapsed)
                {
                    SetWorkAreaRatio(split);
                }
                PositionManagementToggleButton();
            };
            split.SplitterMoved += delegate { PositionManagementToggleButton(); };
            split.HandleCreated += delegate
            {
                split.Panel1MinSize = 260;
                split.Panel2MinSize = 450;
                SetManagementPanelCollapsed(true);
                PositionManagementToggleButton();
            };
            split.Panel1.Controls.Add(BuildShellPanel());
            split.Panel2.Controls.Add(BuildManagementTabs());

            managementToggleToolTip = new ToolTip();
            managementToggleButton = new Button();
            managementToggleButton.Width = 34;
            managementToggleButton.Height = 24;
            managementToggleButton.Margin = Padding.Empty;
            managementToggleButton.Padding = Padding.Empty;
            managementToggleButton.FlatStyle = FlatStyle.Flat;
            managementToggleButton.FlatAppearance.BorderColor = Color.Silver;
            managementToggleButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 248, 252);
            managementToggleButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 238, 248);
            managementToggleButton.BackColor = Color.WhiteSmoke;
            managementToggleButton.TabStop = false;
            managementToggleButton.Click += delegate { ToggleManagementPanel(); };
            UpdateManagementToggleButtonAppearance(false);

            workAreaHost.Controls.Add(split);
            workAreaHost.Controls.Add(managementToggleButton);
            managementToggleButton.BringToFront();
            workAreaHost.SizeChanged += delegate { PositionManagementToggleButton(); };
            return workAreaHost;
        }

        private void SetWorkAreaRatio(SplitContainer split)
        {
            int minDistance;
            int maxDistance;
            if (!TryGetSplitterDistanceRange(split, out minDistance, out maxDistance))
            {
                return;
            }

            int panel2Min = Math.Max(0, split.Panel2MinSize);
            int panel2Max = 600;
            int desired = split.Width - panel2Min - split.SplitterWidth;
            int min = Math.Max(minDistance, split.Width - panel2Max - split.SplitterWidth);
            int max = Math.Min(maxDistance, split.Width - panel2Min - split.SplitterWidth);
            if (desired < min)
            {
                desired = min;
            }
            if (desired > max)
            {
                desired = max;
            }
            ApplySplitterDistance(split, desired);
        }

        private void ToggleManagementPanel()
        {
            SetManagementPanelCollapsed(workAreaSplit == null ? false : !workAreaSplit.Panel2Collapsed);
        }

        private void SetManagementPanelCollapsed(bool collapsed)
        {
            if (workAreaSplit == null)
            {
                return;
            }

            workAreaSplit.Panel2Collapsed = collapsed;
            if (!collapsed)
            {
                SetWorkAreaRatio(workAreaSplit);
                PositionSelectedDeviceHeader();
            }

            if (managementToggleButton != null)
            {
                UpdateManagementToggleButtonAppearance(collapsed);
            }

            PositionManagementToggleButton();
        }

        private void PositionManagementToggleButton()
        {
            if (managementToggleButton == null || workAreaHost == null)
            {
                return;
            }

            int x = workAreaHost.Width - managementToggleButton.Width - 12;
            if (workAreaSplit != null && !workAreaSplit.Panel2Collapsed)
            {
                x = workAreaSplit.Left + workAreaSplit.SplitterDistance + workAreaSplit.SplitterWidth + workAreaSplit.Panel2.Width - managementToggleButton.Width - 10;
            }

            int y = 6;
            if (x < 8)
            {
                x = 8;
            }

            managementToggleButton.SetBounds(x, y, managementToggleButton.Width, managementToggleButton.Height);
            managementToggleButton.BringToFront();
        }

        private void UpdateManagementToggleButtonAppearance(bool collapsed)
        {
            if (managementToggleButton == null)
            {
                return;
            }

            Image oldImage = managementToggleButton.Image;
            managementToggleButton.Text = "";
            managementToggleButton.Image = CreateManagementToggleIcon(collapsed);
            managementToggleButton.AccessibleName = collapsed ? "展开右侧内容区" : "隐藏右侧内容区";
            if (managementToggleToolTip != null)
            {
                managementToggleToolTip.SetToolTip(managementToggleButton, collapsed ? "展开右侧内容区" : "隐藏右侧内容区");
            }
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        private Bitmap CreateManagementToggleIcon(bool collapsed)
        {
            Bitmap bitmap = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen borderPen = new Pen(Color.FromArgb(74, 94, 128), 1.4f))
            using (SolidBrush paneBrush = new SolidBrush(Color.FromArgb(224, 233, 247)))
            using (SolidBrush arrowBrush = new SolidBrush(Color.FromArgb(52, 94, 168)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                Rectangle frame = new Rectangle(1, 2, 16, 13);
                graphics.DrawRectangle(borderPen, frame);

                Rectangle leftPane = new Rectangle(frame.Left + 1, frame.Top + 1, 9, frame.Height - 1);
                Rectangle rightPane = new Rectangle(frame.Left + 11, frame.Top + 1, 4, frame.Height - 1);
                graphics.FillRectangle(paneBrush, collapsed ? leftPane : rightPane);
                graphics.DrawLine(borderPen, frame.Left + 10, frame.Top + 1, frame.Left + 10, frame.Bottom - 1);

                Point[] arrow = collapsed
                    ? new[] { new Point(6, 8), new Point(10, 5), new Point(10, 7), new Point(13, 7), new Point(13, 9), new Point(10, 9), new Point(10, 11) }
                    : new[] { new Point(12, 8), new Point(8, 5), new Point(8, 7), new Point(5, 7), new Point(5, 9), new Point(8, 9), new Point(8, 11) };
                graphics.FillPolygon(arrowBrush, arrow);
            }

            return bitmap;
        }

        private void ShowManagementTab(string tabText, Action refreshAction)
        {
            SetManagementPanelCollapsed(false);
            if (mainTabs != null)
            {
                mainTabs.SelectedTab = FindTabByText(mainTabs, tabText);
            }
            if (refreshAction != null)
            {
                refreshAction();
            }
            PositionSelectedDeviceHeader();
            PositionManagementToggleButton();
        }

        private Control BuildShellPanel()
        {
            GroupBox group = new GroupBox();
            group.Text = "ADB Shell";
            group.Dock = DockStyle.Fill;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8, 4, 8, 8);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.Margin = Padding.Empty;
            buttons.Padding = Padding.Empty;

            Button openShellButton = MakeButton("新建设备 Shell", 128, OpenEmbeddedShell);
            openShellButton.Height = 26;
            openShellButton.Margin = new Padding(0, 1, 6, 1);
            buttons.Controls.Add(openShellButton);

            Button closeShellButton = MakeButton("关闭当前 Shell", 128, CloseCurrentTerminal);
            closeShellButton.Height = 26;
            closeShellButton.Margin = new Padding(0, 1, 6, 1);
            buttons.Controls.Add(closeShellButton);

            shellConnectTextBox = new TextBox();
            shellConnectTextBox.Width = 180;
            shellConnectTextBox.Height = 26;
            shellConnectTextBox.Margin = new Padding(0, 2, 6, 1);
            SetTextBoxCueBanner(shellConnectTextBox, "192.168.137.110:55555");
            shellConnectTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    ConnectAdbShellEndpoint();
                }
            };
            buttons.Controls.Add(shellConnectTextBox);

            Button connectShellButton = MakeButton("连接", 70, ConnectAdbShellEndpoint);
            connectShellButton.Height = 26;
            connectShellButton.Margin = new Padding(0, 1, 6, 1);
            buttons.Controls.Add(connectShellButton);

            terminalTabs = new TabControl();
            terminalTabs.Dock = DockStyle.Fill;

            layout.Controls.Add(buttons, 0, 0);
            layout.Controls.Add(terminalTabs, 0, 1);
            group.Controls.Add(layout);
            return group;
        }

        private void ConnectAdbShellEndpoint()
        {
            string endpoint = shellConnectTextBox == null ? "" : (shellConnectTextBox.Text ?? "").Trim();
            if (String.IsNullOrWhiteSpace(endpoint))
            {
                MessageBox.Show(this, "请输入要连接的设备地址，例如 192.168.1.10:5555。", "缺少设备地址", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (shellConnectTextBox != null && !shellConnectTextBox.IsDisposed)
                {
                    shellConnectTextBox.Focus();
                }
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            string args = "connect " + QuoteArg(endpoint);
            RunAdbDeviceActionAsync(adb, args, delegate(ProcessResult result)
            {
                if (IsAdbConnectSuccess(result))
                {
                    RefreshDevicesIncrementalFromCmd();
                }

                if (shellConnectTextBox != null && !shellConnectTextBox.IsDisposed)
                {
                    shellConnectTextBox.Focus();
                    shellConnectTextBox.SelectAll();
                }
            });
        }

        private Control BuildManagementTabs()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;

            mainTabs = new TabControl();
            mainTabs.Dock = DockStyle.Fill;
            mainTabs.Controls.Add(BuildFileBrowserTab());
            mainTabs.Controls.Add(BuildAppsTab());
            mainTabs.Controls.Add(BuildScreenshotsTab());

            selectedDeviceHeaderLabel = MakeLabel("设备: 未选择");
            selectedDeviceHeaderLabel.AutoEllipsis = true;
            selectedDeviceHeaderLabel.BackColor = SystemColors.Control;
            selectedDeviceHeaderLabel.Height = 26;
            selectedDeviceHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            selectedDeviceHeaderLabel.Padding = new Padding(4, 0, 0, 0);

            mainTabs.HandleCreated += delegate { PositionSelectedDeviceHeader(); };
            mainTabs.SizeChanged += delegate { PositionSelectedDeviceHeader(); };
            mainTabs.SelectedIndexChanged += delegate { PositionSelectedDeviceHeader(); };
            mainTabs.Layout += delegate { PositionSelectedDeviceHeader(); };
            host.SizeChanged += delegate { PositionSelectedDeviceHeader(); };

            host.Controls.Add(mainTabs);
            host.Controls.Add(selectedDeviceHeaderLabel);
            selectedDeviceHeaderLabel.BringToFront();
            return host;
        }

        private void PositionSelectedDeviceHeader()
        {
            if (mainTabs == null || selectedDeviceHeaderLabel == null || mainTabs.TabPages.Count == 0 || !mainTabs.IsHandleCreated)
            {
                return;
            }

            Rectangle lastTab = mainTabs.GetTabRect(mainTabs.TabPages.Count - 1);
            int x = lastTab.Right + 12;
            int y = Math.Max(0, lastTab.Top + 1);
            int rightPadding = managementToggleButton != null && !workAreaSplit.Panel2Collapsed ? managementToggleButton.Width + 18 : 8;
            int width = Math.Max(120, mainTabs.Width - x - rightPadding);
            int height = Math.Max(24, lastTab.Height - 1);
            selectedDeviceHeaderLabel.SetBounds(x, y, width, height);
            selectedDeviceHeaderLabel.BringToFront();
        }

        private TabPage BuildTerminalsTab()
        {
            TabPage page = new TabPage("终端");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(6);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.AutoScroll = true;
            buttons.Controls.Add(MakeButton("新建 PC CMD", 112, OpenEmbeddedCmd));
            buttons.Controls.Add(MakeButton("新建设备 Shell", 128, OpenEmbeddedShell));
            buttons.Controls.Add(MakeButton("关闭当前终端", 128, CloseCurrentTerminal));

            terminalTabs = new TabControl();
            terminalTabs.Dock = DockStyle.Fill;

            layout.Controls.Add(buttons, 0, 0);
            layout.Controls.Add(terminalTabs, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private Control BuildQuickToolsPanel()
        {
            GroupBox group = new GroupBox();
            group.Text = "快捷执行";
            group.Dock = DockStyle.Fill;
            group.SizeChanged += delegate
            {
                UpdateQuickToolsToolbarVisibility(group);
            };

            TableLayoutPanel layout = new TableLayoutPanel();
            quickToolsLayout = layout;
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, QuickToolsToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8, 4, 8, 8);

            FlowLayoutPanel top = new FlowLayoutPanel();
            quickToolsTopPanel = top;
            top.Dock = DockStyle.Fill;
            top.WrapContents = false;
            top.Margin = Padding.Empty;
            top.Padding = Padding.Empty;

            Button customizeQuickToolButton = MakeButton("自定义快捷按钮", 136, ToggleQuickToolEditor);
            customizeQuickToolButton.Height = 26;
            customizeQuickToolButton.Margin = new Padding(0, 1, 8, 1);
            top.Controls.Add(customizeQuickToolButton);

            quickToolEditorPanel = new Panel();
            quickToolEditorPanel.Width = 980;
            quickToolEditorPanel.Height = 30;
            quickToolEditorPanel.Visible = false;
            quickToolEditorPanel.Margin = new Padding(0, 1, 0, 1);

            TableLayoutPanel editor = new TableLayoutPanel();
            editor.Dock = DockStyle.Fill;
            editor.ColumnCount = 8;
            editor.Margin = Padding.Empty;
            editor.Padding = Padding.Empty;
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

            quickNameTextBox = new TextBox();
            quickNameTextBox.Dock = DockStyle.Fill;
            quickNameTextBox.Margin = new Padding(0, 1, 4, 1);
            quickTypeComboBox = new ComboBox();
            quickTypeComboBox.Dock = DockStyle.Fill;
            quickTypeComboBox.Margin = new Padding(0, 1, 4, 1);
            quickTypeComboBox.Height = 24;
            quickTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            quickTypeComboBox.Items.AddRange(new object[] { "cmd", "shell", "py", "ps1" });
            quickTypeComboBox.SelectedIndex = 0;
            quickTypeComboBox.Enabled = false;
            quickTypeComboBox.SelectedIndexChanged += delegate
            {
                if (!quickTypeSelectionUpdating && quickTypeComboBox.Enabled)
                {
                    quickTypeSelectionTouched = true;
                }
            };
            quickCommandTextBox = new TextBox();
            quickCommandTextBox.Dock = DockStyle.Fill;
            quickCommandTextBox.Margin = new Padding(0, 1, 4, 1);
            quickCommandTextBox.TextChanged += delegate
            {
                AutoDetectQuickToolTypeFromEditorCommand();
            };

            Label quickNameLabel = MakeLabel("按钮名：", 62);
            quickNameLabel.Height = 24;
            quickNameLabel.Margin = new Padding(0, 1, 4, 1);
            editor.Controls.Add(quickNameLabel, 0, 0);
            editor.Controls.Add(quickNameTextBox, 1, 0);
            Label quickTypeLabel = MakeLabel("类型：", 46);
            quickTypeLabel.Height = 24;
            quickTypeLabel.Margin = new Padding(0, 1, 4, 1);
            editor.Controls.Add(quickTypeLabel, 2, 0);
            editor.Controls.Add(quickTypeComboBox, 3, 0);
            Label quickCommandLabel = MakeLabel("命令：", 46);
            quickCommandLabel.Height = 24;
            quickCommandLabel.Margin = new Padding(0, 1, 4, 1);
            editor.Controls.Add(quickCommandLabel, 4, 0);
            editor.Controls.Add(quickCommandTextBox, 5, 0);
            Button browseQuickCommandButton = MakeQuickToolCommandBrowseButton(quickCommandTextBox);
            browseQuickCommandButton.Height = 26;
            browseQuickCommandButton.Margin = new Padding(0, 1, 4, 1);
            editor.Controls.Add(browseQuickCommandButton, 6, 0);
            Button saveQuickToolButton = MakeButton("保存", 82, AddQuickTool);
            saveQuickToolButton.Height = 26;
            saveQuickToolButton.Margin = new Padding(0, 1, 0, 1);
            editor.Controls.Add(saveQuickToolButton, 7, 0);
            ResetQuickToolEditorTypeState();
            quickToolEditorPanel.Controls.Add(editor);
            top.Controls.Add(quickToolEditorPanel);

            quickFilterPanel = new Panel();
            quickFilterPanel.Width = 280;
            quickFilterPanel.Height = 30;
            quickFilterPanel.Margin = new Padding(0, 1, 0, 1);
            TableLayoutPanel filterLayout = new TableLayoutPanel();
            filterLayout.Dock = DockStyle.Fill;
            filterLayout.ColumnCount = 2;
            filterLayout.Margin = Padding.Empty;
            filterLayout.Padding = Padding.Empty;
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            Label quickFilterLabel = MakeLabel("命令类型：", 76);
            quickFilterLabel.Height = 24;
            quickFilterLabel.Margin = new Padding(0, 1, 4, 1);
            filterLayout.Controls.Add(quickFilterLabel, 0, 0);
            quickFilterComboBox = new ComboBox();
            quickFilterComboBox.Dock = DockStyle.Fill;
            quickFilterComboBox.Margin = new Padding(0, 1, 0, 1);
            quickFilterComboBox.Height = 24;
            quickFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            quickFilterComboBox.Items.AddRange(new object[] { "全部", "cmd", "shell", "py", "ps1" });
            quickFilterComboBox.SelectedIndex = 0;
            quickFilterComboBox.SelectedIndexChanged += delegate { BindQuickTools(); };
            filterLayout.Controls.Add(quickFilterComboBox, 1, 0);
            quickFilterPanel.Controls.Add(filterLayout);
            top.Controls.Add(quickFilterPanel);

            quickToolsPanel = new FlowLayoutPanel();
            quickToolsPanel.Dock = DockStyle.Fill;
            quickToolsPanel.AutoScroll = true;
            quickToolsPanel.WrapContents = true;
            quickToolsPanel.AllowDrop = true;
            quickToolsPanel.DragEnter += OnQuickToolDragEnter;
            quickToolsPanel.DragOver += OnQuickToolDragEnter;
            quickToolsPanel.DragDrop += OnQuickToolDragDrop;

            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(quickToolsPanel, 0, 1);
            group.Controls.Add(layout);
            UpdateQuickToolsToolbarVisibility(group);
            return group;
        }

        private Control BuildCmdPanel()
        {
            GroupBox group = new GroupBox();
            group.Text = "PC CMD";
            group.Dock = DockStyle.Fill;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8);

            cmdOutputBox = new RichTextBox();
            cmdOutputBox.Dock = DockStyle.Fill;
            cmdOutputBox.Font = new Font("Consolas", 9F);
            cmdOutputBox.BackColor = cmdBackgroundColor;
            cmdOutputBox.ForeColor = cmdOutputColor;
            cmdOutputBox.ReadOnly = true;
            cmdOutputBox.WordWrap = true;
            cmdOutputBox.BorderStyle = BorderStyle.None;
            cmdContextMenu = BuildCmdContextMenu();
            cmdOutputBox.ContextMenuStrip = cmdContextMenu;
            cmdOutputBox.HideSelection = false;
            cmdOutputBox.ShortcutsEnabled = false;
            cmdOutputBox.AllowDrop = true;
            cmdOutputBox.DragEnter += OnTerminalFileDragEnter;
            cmdOutputBox.DragOver += OnTerminalFileDragEnter;
            cmdOutputBox.DragDrop += OnCmdFileDragDrop;
            cmdOutputBox.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && cmdOutputBox.SelectionLength == 0)
                {
                    cmdOutputBox.Focus();
                    cmdOutputBox.BeginInvoke((Action)delegate
                    {
                        RestoreCmdInputCaretFromModel();
                    });
                }
            };
            cmdOutputBox.KeyDown += OnCmdOutputKeyDown;
            cmdOutputBox.KeyPress += OnCmdOutputKeyPress;
            cmdOutputBox.MouseWheel += delegate
            {
                BeginInvoke((Action)delegate
                {
                    cmdWheelScrolledAwayFromBottom = !IsCmdScrolledToBottom();
                });
            };
            layout.Controls.Add(BuildTerminalHost(cmdOutputBox), 0, 0);
            group.Controls.Add(layout);
            return group;
        }

        private TabPage BuildQuickToolsTab()
        {
            TabPage page = new TabPage("快捷执行");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8);

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.WrapContents = false;
            top.Controls.Add(MakeButton("自定义快捷按钮", 136, ToggleQuickToolEditor));

            quickToolEditorPanel = new Panel();
            quickToolEditorPanel.Dock = DockStyle.Fill;
            quickToolEditorPanel.Visible = false;

            TableLayoutPanel editor = new TableLayoutPanel();
            editor.Dock = DockStyle.Fill;
            editor.ColumnCount = 7;
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

            quickNameTextBox = new TextBox();
            quickNameTextBox.Dock = DockStyle.Fill;
            quickTypeComboBox = new ComboBox();
            quickTypeComboBox.Dock = DockStyle.Fill;
            quickTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            quickTypeComboBox.Items.AddRange(new object[] { "cmd", "shell", "py", "ps1" });
            quickTypeComboBox.SelectedIndex = 0;
            quickCommandTextBox = new TextBox();
            quickCommandTextBox.Dock = DockStyle.Fill;

            editor.Controls.Add(MakeLabel("按钮名"), 0, 0);
            editor.Controls.Add(quickNameTextBox, 1, 0);
            editor.Controls.Add(MakeLabel("类型"), 2, 0);
            editor.Controls.Add(quickTypeComboBox, 3, 0);
            editor.Controls.Add(MakeLabel("命令"), 4, 0);
            editor.Controls.Add(quickCommandTextBox, 5, 0);
            editor.Controls.Add(MakeButton("保存", 82, AddQuickTool), 6, 0);

            quickToolEditorPanel.Controls.Add(editor);

            quickToolsPanel = new FlowLayoutPanel();
            quickToolsPanel.Dock = DockStyle.Fill;
            quickToolsPanel.AutoScroll = true;
            quickToolsPanel.WrapContents = true;

            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(quickToolEditorPanel, 0, 1);
            layout.Controls.Add(quickToolsPanel, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildFileBrowserTab()
        {
            TabPage page = new TabPage("文件浏览");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8);

            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.ColumnCount = 3;
            top.RowCount = 1;
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));

            remotePathTextBox = new TextBox();
            remotePathTextBox.Dock = DockStyle.Fill;
            remotePathTextBox.Text = "/sdcard/";
            remotePathTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    RefreshFiles();
                }
            };

            top.Controls.Add(remotePathTextBox, 0, 0);
            top.Controls.Add(MakeButton("刷新", 68, RefreshFiles), 1, 0);
            top.Controls.Add(MakeButton("上级", 68, GoParentDirectory), 2, 0);

            filesGrid = new DataGridView();
            filesGrid.Dock = DockStyle.Fill;
            filesGrid.AllowUserToAddRows = false;
            filesGrid.AllowUserToDeleteRows = false;
            filesGrid.ReadOnly = true;
            filesGrid.MultiSelect = false;
            filesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            filesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            filesGrid.RowHeadersVisible = false;
            filesGrid.ScrollBars = ScrollBars.Both;
            filesGrid.AllowDrop = true;
            filesGrid.Columns.Add("Name", "名称");
            filesGrid.Columns.Add("Type", "类型");
            filesGrid.Columns.Add("Size", "大小");
            filesGrid.Columns.Add("Modified", "修改时间");
            filesGrid.Columns["Name"].SortMode = DataGridViewColumnSortMode.Programmatic;
            filesGrid.Columns["Type"].SortMode = DataGridViewColumnSortMode.Programmatic;
            filesGrid.Columns["Size"].SortMode = DataGridViewColumnSortMode.Programmatic;
            filesGrid.Columns["Modified"].SortMode = DataGridViewColumnSortMode.Programmatic;
            filesGrid.CellMouseDown += OnFilesGridCellMouseDown;
            filesGrid.CellMouseDoubleClick += OnFilesGridCellMouseDoubleClick;
            filesGrid.MouseDown += OnFilesGridMouseDown;
            filesGrid.MouseDoubleClick += OnFilesGridMouseDoubleClick;
            filesGrid.ColumnHeaderMouseClick += OnFilesGridColumnHeaderMouseClick;
            filesGrid.ColumnWidthChanged += delegate { ApplyFileTailEllipsis(); };
            filesGrid.SizeChanged += delegate
            {
                AdjustFilesGridColumns();
                ApplyFileTailEllipsis();
            };
            filesGrid.DragEnter += OnFilesGridDragEnter;
            filesGrid.DragDrop += OnFilesGridDragDrop;
            filesMenu = BuildFilesMenu();
            filesRightClickTimer = new System.Windows.Forms.Timer();
            filesRightClickTimer.Interval = 300;
            filesRightClickTimer.Tick += OnFilesRightClickTimerTick;

            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(filesGrid, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildScreenshotsTab()
        {
            TabPage page = new TabPage("截图查看");
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 220;

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 2;
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.Padding = new Padding(8);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.Controls.Add(MakeButton("刷新", 70, RefreshScreenshots));
            buttons.Controls.Add(MakeButton("截屏", 70, CaptureAndViewScreenshot));
            buttons.Controls.Add(MakeButton("目录", 70, OpenScreenshotFolder));

            screenshotListBox = new ListBox();
            screenshotListBox.Dock = DockStyle.Fill;
            screenshotListBox.DrawMode = DrawMode.OwnerDrawFixed;
            screenshotListBox.ItemHeight = screenshotListBox.Font.Height + 8;
            screenshotListBox.IntegralHeight = false;
            screenshotListBox.DrawItem += OnScreenshotListBoxDrawItem;
            screenshotListBox.MouseDown += OnScreenshotListBoxMouseDown;
            screenshotListBox.SelectedIndexChanged += delegate { ShowSelectedScreenshot(); };
            screenshotsMenu = BuildScreenshotsMenu();
            screenshotListBox.ContextMenuStrip = screenshotsMenu;

            screenshotPictureBox = new PictureBox();
            screenshotPictureBox.Dock = DockStyle.Fill;
            screenshotPictureBox.BackColor = Color.FromArgb(32, 32, 32);
            screenshotPictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            left.Controls.Add(buttons, 0, 0);
            left.Controls.Add(screenshotListBox, 0, 1);
            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(screenshotPictureBox);
            page.Controls.Add(split);
            return page;
        }

        private ContextMenuStrip BuildFilesMenu()
        {
            filesMenu = new ContextMenuStrip();
            filesMenu.Opening += OnFilesMenuOpening;
            filesMenu.Items.Add("复制", null, delegate { CopySelectedFileEntry(); });
            filesMenu.Items.Add("剪切", null, delegate { CutSelectedFileEntry(); });
            filesMenu.Items.Add("粘贴", null, delegate { PasteFileEntry(); });
            filesMenu.Items.Add("新建文件", null, delegate { CreateRemoteFileEntry(false); });
            filesMenu.Items.Add("新建文件夹", null, delegate { CreateRemoteFileEntry(true); });
            filesMenu.Items.Add(new ToolStripSeparator());
            filesMenu.Items.Add("删除", null, delegate { DeleteSelectedFileEntry(); });
            filesMenu.Items.Add("重命名", null, delegate { RenameSelectedFileEntry(); });
            filesMenu.Items.Add(new ToolStripSeparator());
            filesMenu.Items.Add("下载", null, delegate { DownloadSelectedFile(); });
            filesMenu.Items.Add("打开目录", null, delegate { OpenDownloadedFileDirectory(); });
            filesMenu.Items.Add("编辑文本", null, delegate { EditSelectedTextFile(); });
            filesMenu.Items.Add("上传", null, delegate { UploadToSelectedFileEntry(); });
            filesMenu.Items.Add(new ToolStripSeparator());
            filesMenu.Items.Add("属性", null, delegate { ShowSelectedFileProperties(); });
            return filesMenu;
        }

        private ContextMenuStrip BuildScreenshotsMenu()
        {
            screenshotsMenu = new ContextMenuStrip();
            screenshotsMenu.Opening += OnScreenshotsMenuOpening;
            screenshotsMenu.Items.Add("复制", null, delegate { CopySelectedScreenshot(); });
            screenshotsMenu.Items.Add("删除", null, delegate { DeleteSelectedScreenshot(); });
            screenshotsMenu.Items.Add("重命名", null, delegate { RenameSelectedScreenshot(); });
            screenshotsMenu.Items.Add("复制绝对路径", null, delegate { CopySelectedScreenshotPath(); });
            screenshotsMenu.Items.Add("属性", null, delegate { ShowSelectedScreenshotProperties(); });
            return screenshotsMenu;
        }

        private TabPage BuildAppsTab()
        {
            TabPage page = new TabPage("应用列表");
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(8);

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.WrapContents = false;
            top.AutoScroll = false;
            appFilterComboBox = new ComboBox();
            appFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            appFilterComboBox.Width = 120;
            appFilterComboBox.Items.AddRange(new object[] { "全部", "用户应用", "系统应用" });
            appFilterComboBox.SelectedIndex = 1;
            appFilterComboBox.SelectedIndexChanged += delegate
            {
                if (IsHandleCreated)
                {
                    RefreshApps();
                }
            };
            top.Controls.Add(MakeLabel("类型：", 44));
            top.Controls.Add(appFilterComboBox);
            top.Controls.Add(MakeButton("刷新", 70, RefreshApps));

            appsGrid = new DataGridView();
            appsGrid.Dock = DockStyle.Fill;
            appsGrid.AllowUserToAddRows = false;
            appsGrid.AllowUserToDeleteRows = false;
            appsGrid.ReadOnly = true;
            appsGrid.MultiSelect = false;
            appsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            appsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            appsGrid.RowHeadersVisible = false;
            appsGrid.ScrollBars = ScrollBars.Vertical;
            appsGrid.AllowDrop = true;
            appsGrid.Columns.Add("Name", "名称");
            appsGrid.Columns.Add("PackageName", "包名");
            appsGrid.Columns.Add("VersionName", "版本号");
            appsGrid.Columns.Add("Type", "类型");
            appsGrid.ColumnWidthChanged += delegate { ApplyAppTailEllipsis(); };
            appsGrid.SizeChanged += delegate
            {
                AdjustAppsGridColumns();
                ApplyAppTailEllipsis();
            };

            appsMenu = new ContextMenuStrip();
            appsMenu.Opening += OnAppsMenuOpening;
            installAppMenuItem = new ToolStripMenuItem("安装应用", null, delegate { PickAndInstallApps(); });
            appsMenu.Items.Add(installAppMenuItem);
            appsMenu.Items.Add(new ToolStripSeparator());
            appsMenu.Items.Add("打开应用", null, delegate { OpenSelectedApp(); });
            appsMenu.Items.Add("重启应用", null, delegate { RestartSelectedApp(); });
            appsMenu.Items.Add("杀死应用", null, delegate { KillSelectedApp(); });
            appFreezeEnableMenuItem = new ToolStripMenuItem("冻结应用", null, delegate { FreezeOrEnableSelectedApp(); });
            appsMenu.Items.Add(appFreezeEnableMenuItem);
            appsMenu.Items.Add("清除数据", null, delegate { ClearSelectedAppData(); });
            appsMenu.Items.Add("卸载", null, delegate { UninstallSelectedApp(); });
            appsMenu.Items.Add("复制包名", null, delegate { CopySelectedAppPackageName(); });
            appsMenu.Items.Add("属性", null, delegate { ShowSelectedAppProperties(); });
            appsGrid.ContextMenuStrip = appsMenu;
            appsGrid.DragEnter += OnAppsGridDragEnter;
            appsGrid.DragOver += OnAppsGridDragEnter;
            appsGrid.DragDrop += OnAppsGridDragDrop;
            appsGrid.CellMouseDown += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    appsGrid.ClearSelection();
                    appsGrid.Rows[e.RowIndex].Selected = true;
                }
            };

            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(appsGrid, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private void OnAppsMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AppEntry app = CurrentSelectedApp();
            bool hasSelection = app != null && !String.IsNullOrWhiteSpace(app.PackageName);
            if (appFreezeEnableMenuItem != null)
            {
                appFreezeEnableMenuItem.Text = app != null && app.IsDisabled ? "激活应用" : "冻结应用";
                appFreezeEnableMenuItem.Enabled = hasSelection;
            }
        }

        private Control BuildOutputPanel()
        {
            GroupBox group = new GroupBox();
            group.Text = "输出";
            group.Dock = DockStyle.Fill;

            outputBox = new RichTextBox();
            outputBox.Dock = DockStyle.Fill;
            outputBox.Font = new Font("Consolas", 9F);
            outputBox.ReadOnly = true;
            outputBox.ShortcutsEnabled = true;
            outputBox.HideSelection = false;
            outputBox.WordWrap = false;
            outputContextMenu = BuildOutputContextMenu();
            outputBox.ContextMenuStrip = outputContextMenu;
            outputBox.KeyDown += OnOutputBoxKeyDown;
            group.Controls.Add(outputBox);
            return group;
        }

        private Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Width = Math.Max(42, text.Length * 16);
            label.Height = 28;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(3, 5, 3, 3);
            return label;
        }

        private Label MakeLabel(string text, int width)
        {
            Label label = MakeLabel(text);
            label.Width = width;
            return label;
        }

        private Button MakeButton(string text, int width, Action action)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.Margin = new Padding(3, 4, 6, 4);
            button.Click += delegate { action(); };
            return button;
        }

        private Button MakeRailButton(string text, Action action)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 38;
            button.Height = 34;
            button.Margin = new Padding(0, 3, 0, 3);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Click += delegate { action(); };
            return button;
        }

        private void SetTextBoxCueBanner(TextBox textBox, string cueText)
        {
            if (textBox == null)
            {
                return;
            }

            Action apply = delegate
            {
                if (!textBox.IsDisposed && textBox.IsHandleCreated)
                {
                    SendMessage(textBox.Handle, EmSetCueBanner, (IntPtr)0, cueText ?? "");
                }
            };

            if (textBox.IsHandleCreated)
            {
                apply();
            }
            else
            {
                textBox.HandleCreated += delegate { apply(); };
            }
        }

        private TabPage FindTabByText(TabControl tabs, string text)
        {
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                if (tabs.TabPages[i].Text == text)
                {
                    return tabs.TabPages[i];
                }
            }
            return tabs.TabPages[0];
        }

        private bool FillDefaultsIfEmpty()
        {
            bool changed = false;
            if (String.IsNullOrWhiteSpace(config.AdbPath))
            {
                string resolved = FindOnPath("adb.exe");
                if (!String.IsNullOrWhiteSpace(resolved))
                {
                    config.AdbPath = resolved;
                    changed = true;
                }
            }
            if (String.IsNullOrWhiteSpace(config.ScrcpyPath))
            {
                string resolved = FindOnPath("scrcpy.exe");
                if (!String.IsNullOrWhiteSpace(resolved))
                {
                    config.ScrcpyPath = resolved;
                    changed = true;
                }
            }
            if (String.IsNullOrWhiteSpace(config.PythonPath))
            {
                string resolved = FirstNonEmpty(
                    FindOnPath("python.exe"),
                    FindOnPath("python"));
                if (!String.IsNullOrWhiteSpace(resolved))
                {
                    config.PythonPath = resolved;
                    changed = true;
                }
            }
            if (String.IsNullOrWhiteSpace(config.ScreenshotDir))
            {
                config.ScreenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
                changed = true;
            }
            if (String.IsNullOrWhiteSpace(config.DownloadDir))
            {
                config.DownloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                changed = true;
            }
            if (String.IsNullOrWhiteSpace(config.CmdBackgroundColor))
            {
                config.CmdBackgroundColor = ColorToConfig(DefaultCmdBackgroundColor);
                changed = true;
            }
            if (String.IsNullOrWhiteSpace(config.CmdOutputColor))
            {
                config.CmdOutputColor = ColorToConfig(DefaultCmdOutputColor);
                changed = true;
            }
            if (String.IsNullOrWhiteSpace(config.CmdPromptColor))
            {
                config.CmdPromptColor = ColorToConfig(DefaultCmdPromptColor);
                changed = true;
            }
            if (String.IsNullOrWhiteSpace(config.CmdInputColor))
            {
                config.CmdInputColor = ColorToConfig(DefaultCmdInputColor);
                changed = true;
            }
            if (config.ScrcpyHighQualityDisabledSerials == null)
            {
                config.ScrcpyHighQualityDisabledSerials = new List<string>();
                changed = true;
            }
            if (config.ScrcpyHighQualityForcedSerials == null)
            {
                config.ScrcpyHighQualityForcedSerials = new List<string>();
                changed = true;
            }
            return changed;
        }

        private void LoadCmdColorsFromConfig()
        {
            cmdBackgroundColor = ParseConfigColor(config.CmdBackgroundColor, DefaultCmdBackgroundColor);
            cmdOutputColor = ParseConfigColor(config.CmdOutputColor, DefaultCmdOutputColor);
            cmdPromptColor = ParseConfigColor(config.CmdPromptColor, DefaultCmdPromptColor);
            cmdInputColor = ParseConfigColor(config.CmdInputColor, DefaultCmdInputColor);
        }

        private Color ParseConfigColor(string value, Color fallback)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            try
            {
                return ColorTranslator.FromHtml(value.Trim());
            }
            catch
            {
                return fallback;
            }
        }

        private string ColorToConfig(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private void ApplyCmdColorsToOutput()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int selectionStart = cmdOutputBox.SelectionStart;
            int selectionLength = cmdOutputBox.SelectionLength;
            cmdOutputBox.BackColor = cmdBackgroundColor;
            cmdOutputBox.ForeColor = cmdOutputColor;
            ColorizeCmdCurrentLine();
            cmdOutputBox.SelectionStart = Math.Min(selectionStart, cmdOutputBox.TextLength);
            cmdOutputBox.SelectionLength = Math.Min(selectionLength, Math.Max(0, cmdOutputBox.TextLength - cmdOutputBox.SelectionStart));
        }

        private string FindOnPath(string fileName)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (String.IsNullOrEmpty(path))
            {
                return "";
            }

            string[] parts = path.Split(Path.PathSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                try
                {
                    string candidate = Path.Combine(parts[i].Trim(), fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
            return "";
        }

        private void ShowSettingsDialog()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "设置";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 760;
                dialog.Height = 270;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                TableLayoutPanel layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 3;
                layout.RowCount = 6;
                layout.Padding = new Padding(12);
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
                for (int i = 0; i < 6; i++)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                }

                TextBox adb = new TextBox();
                adb.Dock = DockStyle.Fill;
                adb.Text = config.AdbPath;
                TextBox scrcpy = new TextBox();
                scrcpy.Dock = DockStyle.Fill;
                scrcpy.Text = config.ScrcpyPath;
                TextBox python = new TextBox();
                python.Dock = DockStyle.Fill;
                python.Text = config.PythonPath;
                TextBox screenshot = new TextBox();
                screenshot.Dock = DockStyle.Fill;
                screenshot.Text = config.ScreenshotDir;
                TextBox download = new TextBox();
                download.Dock = DockStyle.Fill;
                download.Text = config.DownloadDir;

                layout.Controls.Add(MakeLabel("adb"), 0, 0);
                layout.Controls.Add(adb, 1, 0);
                layout.Controls.Add(MakeBrowseButton(adb, "选择 adb", "exe"), 2, 0);
                layout.Controls.Add(MakeLabel("scrcpy"), 0, 1);
                layout.Controls.Add(scrcpy, 1, 1);
                layout.Controls.Add(MakeBrowseButton(scrcpy, "选择 scrcpy", "exe"), 2, 1);
                layout.Controls.Add(MakeLabel("python"), 0, 2);
                layout.Controls.Add(python, 1, 2);
                layout.Controls.Add(MakeBrowseButton(python, "选择 python", "exe"), 2, 2);
                layout.Controls.Add(MakeLabel("截图目录"), 0, 3);
                layout.Controls.Add(screenshot, 1, 3);
                layout.Controls.Add(MakeBrowseButton(screenshot, "选择截图目录", "folder"), 2, 3);
                layout.Controls.Add(MakeLabel("下载目录"), 0, 4);
                layout.Controls.Add(download, 1, 4);
                layout.Controls.Add(MakeBrowseButton(download, "选择下载目录", "folder"), 2, 4);

                FlowLayoutPanel bottom = new FlowLayoutPanel();
                bottom.Dock = DockStyle.Fill;
                bottom.FlowDirection = FlowDirection.RightToLeft;
                Button save = MakeButton("保存", 80, delegate
                {
                    config.AdbPath = adb.Text.Trim();
                    config.ScrcpyPath = scrcpy.Text.Trim();
                    config.PythonPath = python.Text.Trim();
                    config.ScreenshotDir = screenshot.Text.Trim();
                    config.DownloadDir = download.Text.Trim();
                    SaveConfig();
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                });
                bottom.Controls.Add(save);
                layout.Controls.Add(bottom, 1, 5);

                dialog.Controls.Add(layout);
                dialog.ShowDialog(this);
            }
        }

        private void ShowCmdColorDialog()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "PC CMD 配色";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 420;
                dialog.Height = 245;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                Color background = cmdBackgroundColor;
                Color output = cmdOutputColor;
                Color prompt = cmdPromptColor;
                Color input = cmdInputColor;

                TableLayoutPanel layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 2;
                layout.RowCount = 5;
                layout.Padding = new Padding(12);
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 5; i++)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                }

                Button backgroundButton = MakeColorButton(background);
                Button outputButton = MakeColorButton(output);
                Button promptButton = MakeColorButton(prompt);
                Button inputButton = MakeColorButton(input);

                backgroundButton.Click += delegate { PickColor("背景色", backgroundButton, ref background); };
                outputButton.Click += delegate { PickColor("输出文字", outputButton, ref output); };
                promptButton.Click += delegate { PickColor("提示符", promptButton, ref prompt); };
                inputButton.Click += delegate { PickColor("输入文字", inputButton, ref input); };

                layout.Controls.Add(MakeLabel("背景色"), 0, 0);
                layout.Controls.Add(backgroundButton, 1, 0);
                layout.Controls.Add(MakeLabel("输出文字"), 0, 1);
                layout.Controls.Add(outputButton, 1, 1);
                layout.Controls.Add(MakeLabel("提示符"), 0, 2);
                layout.Controls.Add(promptButton, 1, 2);
                layout.Controls.Add(MakeLabel("输入文字"), 0, 3);
                layout.Controls.Add(inputButton, 1, 3);

                FlowLayoutPanel bottom = new FlowLayoutPanel();
                bottom.Dock = DockStyle.Fill;
                bottom.FlowDirection = FlowDirection.RightToLeft;
                Button save = MakeButton("保存", 80, delegate
                {
                    cmdBackgroundColor = background;
                    cmdOutputColor = output;
                    cmdPromptColor = prompt;
                    cmdInputColor = input;
                    config.CmdBackgroundColor = ColorToConfig(cmdBackgroundColor);
                    config.CmdOutputColor = ColorToConfig(cmdOutputColor);
                    config.CmdPromptColor = ColorToConfig(cmdPromptColor);
                    config.CmdInputColor = ColorToConfig(cmdInputColor);
                    SaveConfig();
                    ApplyCmdColorsToOutput();
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                });
                Button reset = MakeButton("默认", 80, delegate
                {
                    background = DefaultCmdBackgroundColor;
                    output = DefaultCmdOutputColor;
                    prompt = DefaultCmdPromptColor;
                    input = DefaultCmdInputColor;
                    UpdateColorButton(backgroundButton, background);
                    UpdateColorButton(outputButton, output);
                    UpdateColorButton(promptButton, prompt);
                    UpdateColorButton(inputButton, input);
                });
                bottom.Controls.Add(save);
                bottom.Controls.Add(reset);
                layout.Controls.Add(bottom, 1, 4);

                dialog.Controls.Add(layout);
                dialog.ShowDialog(this);
            }
        }

        private Button MakeColorButton(Color color)
        {
            Button button = new Button();
            button.Dock = DockStyle.Fill;
            button.FlatStyle = FlatStyle.Flat;
            UpdateColorButton(button, color);
            return button;
        }

        private void UpdateColorButton(Button button, Color color)
        {
            button.BackColor = color;
            button.Text = ColorToConfig(color);
            button.ForeColor = GetReadableTextColor(color);
        }

        private Color GetReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 140 ? Color.Black : Color.White;
        }

        private void PickColor(string title, Button button, ref Color target)
        {
            using (ColorDialog picker = new ColorDialog())
            {
                picker.FullOpen = true;
                picker.Color = target;
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    target = picker.Color;
                    UpdateColorButton(button, target);
                }
            }
        }

        private void ShowAboutDialog()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionText = version == null ? "未知" : version.ToString();
            string buildTime = File.GetLastWriteTime(Application.ExecutablePath).ToString("yyyy-MM-dd HH:mm:ss");
            MessageBox.Show(
                this,
                "@AnubisASN" + Environment.NewLine +
                "应用名称: ADB Tool (ASN)" + Environment.NewLine +
                "编译时间: " + buildTime + Environment.NewLine +
                "邮件: anubisasn@gmail.com" + Environment.NewLine +
                "版本号: " + versionText,
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string PromptForText(string title, string label, string initialValue)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 460;
                dialog.Height = 150;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                TableLayoutPanel layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 1;
                layout.RowCount = 3;
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                layout.Padding = new Padding(12);

                Label prompt = new Label();
                prompt.Text = label;
                prompt.Dock = DockStyle.Fill;
                prompt.TextAlign = ContentAlignment.MiddleLeft;

                TextBox input = new TextBox();
                input.Dock = DockStyle.Fill;
                input.Text = initialValue;

                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = FlowDirection.RightToLeft;

                Button ok = new Button();
                ok.Text = "确定";
                ok.Width = 80;
                ok.Click += delegate
                {
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.Width = 80;
                cancel.Click += delegate
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                };

                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                layout.Controls.Add(prompt, 0, 0);
                layout.Controls.Add(input, 0, 1);
                layout.Controls.Add(buttons, 0, 2);
                dialog.Controls.Add(layout);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return input.Text.Trim();
                }
            }

            return null;
        }

        private string PromptForMultilineText(string title, string label, string initialValue, string placeholder)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 520;
                dialog.Height = 250;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                TableLayoutPanel layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 1;
                layout.RowCount = 3;
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.Padding = new Padding(12);

                Label prompt = new Label();
                prompt.Text = label;
                prompt.Dock = DockStyle.Fill;
                prompt.TextAlign = ContentAlignment.MiddleLeft;

                Panel inputHost = new Panel();
                inputHost.Dock = DockStyle.Fill;

                TextBox input = new TextBox();
                input.Dock = DockStyle.Fill;
                input.Multiline = true;
                input.ScrollBars = ScrollBars.Vertical;
                input.AcceptsReturn = true;
                input.Text = initialValue ?? "";

                Label cue = new Label();
                cue.Text = placeholder ?? "";
                cue.ForeColor = SystemColors.GrayText;
                cue.BackColor = Color.Transparent;
                cue.AutoSize = false;
                cue.Dock = DockStyle.Fill;
                cue.Padding = new Padding(4, 4, 4, 4);
                cue.TextAlign = ContentAlignment.TopLeft;
                cue.Click += delegate { input.Focus(); };

                EventHandler refreshCue = delegate
                {
                    cue.Visible = String.IsNullOrEmpty(input.Text);
                    cue.BringToFront();
                };
                input.TextChanged += delegate { refreshCue(null, EventArgs.Empty); };
                inputHost.Resize += delegate { cue.BringToFront(); };
                inputHost.Controls.Add(input);
                inputHost.Controls.Add(cue);
                refreshCue(null, EventArgs.Empty);

                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = FlowDirection.RightToLeft;

                Button ok = new Button();
                ok.Text = "确定";
                ok.Width = 80;
                ok.Click += delegate
                {
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.Width = 80;
                cancel.Click += delegate
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                };

                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                layout.Controls.Add(prompt, 0, 0);
                layout.Controls.Add(inputHost, 0, 1);
                layout.Controls.Add(buttons, 0, 2);
                dialog.Controls.Add(layout);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return input.Text.Trim();
                }
            }

            return null;
        }

        private void ConfigureLanScanSubnets()
        {
            string currentValue = String.IsNullOrWhiteSpace(config.LanScanSubnets)
                ? String.Join(Environment.NewLine, DetectCurrentLanSubnets().ToArray())
                : config.LanScanSubnets;
            string placeholder =
                "示例：" + Environment.NewLine +
                "192.168.31.0/24" + Environment.NewLine +
                "192.168.137.0/24" + Environment.NewLine +
                "10.0.0.0/24";
            string result = PromptForMultilineText(
                "局域网扫描网段",
                "支持多个网段，使用换行、逗号或分号分隔。留空则恢复为自动扫描当前网段。",
                currentValue,
                placeholder);
            if (result == null)
            {
                return;
            }

            string normalized = NormalizeLanScanSubnetConfigText(result);
            string invalidMessage;
            if (!TryValidateLanScanSubnetConfigText(normalized, out invalidMessage))
            {
                MessageBox.Show(this, invalidMessage, "网段格式无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            config.LanScanSubnets = normalized;
            SaveConfig();
            AppendOutput(String.IsNullOrWhiteSpace(normalized)
                ? "局域网扫描网段已恢复为自动检测当前网段。"
                : "局域网扫描网段已更新: " + normalized.Replace(Environment.NewLine, ", "));
        }

        private void StartLanDeviceScan()
        {
            if (Interlocked.CompareExchange(ref lanScanInProgressFlag, 1, 0) != 0)
            {
                AppendOutput("局域网扫描正在进行中。");
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                Interlocked.Exchange(ref lanScanInProgressFlag, 0);
                return;
            }

            List<string> subnetSpecs = GetLanScanSubnetSpecs();
            if (subnetSpecs.Count == 0)
            {
                Interlocked.Exchange(ref lanScanInProgressFlag, 0);
                MessageBox.Show(this, "未找到可扫描的局域网网段，请先右键扫描图标配置网段。", "缺少扫描网段", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            AppendOutput("开始扫描局域网 5555 端口: " + String.Join(", ", subnetSpecs.ToArray()));
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> warnings = new List<string>();
                List<string> hosts = BuildLanScanHosts(subnetSpecs, warnings);
                List<string> openEndpoints = ProbeLanScanEndpoints(hosts);
                List<KeyValuePair<string, ProcessResult>> connectResults = new List<KeyValuePair<string, ProcessResult>>();
                bool anyConnected = false;

                for (int i = 0; i < openEndpoints.Count; i++)
                {
                    string endpoint = openEndpoints[i];
                    string args = "connect " + QuoteArg(endpoint);
                    ProcessResult result = RunProcess(adb, args, null, false);
                    connectResults.Add(new KeyValuePair<string, ProcessResult>(endpoint, result));
                    if (IsAdbConnectSuccess(result))
                    {
                        anyConnected = true;
                    }
                }

                BeginInvokeIfAvailable(delegate
                {
                    try
                    {
                        for (int i = 0; i < warnings.Count; i++)
                        {
                            AppendOutput(warnings[i]);
                        }

                        AppendOutput("局域网扫描目标数: " + hosts.Count + "，开放 5555 端口: " + openEndpoints.Count);
                        for (int i = 0; i < connectResults.Count; i++)
                        {
                            AppendOutput("> " + adb + " connect " + connectResults[i].Key);
                            AppendProcessResult(connectResults[i].Value);
                        }

                        if (anyConnected)
                        {
                            RefreshDevicesIncrementalFromCmd();
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref lanScanInProgressFlag, 0);
                    }
                });
            });
        }

        private string NormalizeLanScanSubnetConfigText(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            string[] parts = text.Replace("\r\n", "\n").Replace(",", "\n").Replace(";", "\n").Split('\n');
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (String.IsNullOrWhiteSpace(item) || seen.Contains(item))
                {
                    continue;
                }
                seen.Add(item);
                result.Add(item);
            }

            return String.Join(Environment.NewLine, result.ToArray());
        }

        private bool TryValidateLanScanSubnetConfigText(string text, out string message)
        {
            message = null;
            if (String.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string[] parts = text.Replace("\r\n", "\n").Split('\n');
            List<string> invalid = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (String.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                uint networkAddress;
                int prefixLength;
                if (LooksLikeIpv4(item) || TryParseIpv4Cidr(item, out networkAddress, out prefixLength))
                {
                    continue;
                }

                invalid.Add(item);
            }

            if (invalid.Count == 0)
            {
                return true;
            }

            message = "以下网段格式无法识别：" + Environment.NewLine + String.Join(Environment.NewLine, invalid.ToArray());
            return false;
        }

        private List<string> GetLanScanSubnetSpecs()
        {
            string source = String.IsNullOrWhiteSpace(config.LanScanSubnets)
                ? String.Join(Environment.NewLine, DetectCurrentLanSubnets().ToArray())
                : config.LanScanSubnets;
            source = NormalizeLanScanSubnetConfigText(source);
            if (String.IsNullOrWhiteSpace(source))
            {
                return new List<string>();
            }

            return new List<string>(source.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
        }

        private List<string> DetectCurrentLanSubnets()
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < adapters.Length; i++)
            {
                NetworkInterface adapter = adapters[i];
                if (adapter == null || adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties properties;
                try
                {
                    properties = adapter.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                for (int j = 0; j < properties.UnicastAddresses.Count; j++)
                {
                    UnicastIPAddressInformation addressInfo = properties.UnicastAddresses[j];
                    if (addressInfo == null || addressInfo.Address == null || addressInfo.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    byte[] addressBytes = addressInfo.Address.GetAddressBytes();
                    if (addressBytes.Length != 4 || addressBytes[0] == 127 || (addressBytes[0] == 169 && addressBytes[1] == 254))
                    {
                        continue;
                    }

                    IPAddress mask = addressInfo.IPv4Mask;
                    int prefixLength = mask == null ? 24 : GetIpv4PrefixLength(mask);
                    uint addressValue = Ipv4ToUInt32(addressInfo.Address);
                    uint maskValue = PrefixLengthToMask(prefixLength);
                    uint networkValue = addressValue & maskValue;
                    string cidr = UInt32ToIpv4(networkValue) + "/" + prefixLength;
                    if (seen.Add(cidr))
                    {
                        result.Add(cidr);
                    }
                }
            }

            return result;
        }

        private List<string> BuildLanScanHosts(List<string> subnetSpecs, List<string> warnings)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < subnetSpecs.Count; i++)
            {
                string spec = subnetSpecs[i];
                if (String.IsNullOrWhiteSpace(spec))
                {
                    continue;
                }

                uint networkAddress;
                int prefixLength;
                if (LooksLikeIpv4(spec))
                {
                    if (seen.Add(spec))
                    {
                        result.Add(spec);
                    }
                    continue;
                }

                if (!TryParseIpv4Cidr(spec, out networkAddress, out prefixLength))
                {
                    warnings.Add("已忽略无法识别的扫描网段: " + spec);
                    continue;
                }

                long hostCount = prefixLength >= 31 ? 1L << (32 - prefixLength) : (1L << (32 - prefixLength)) - 2;
                if (hostCount > LanScanMaxHostsPerSubnet)
                {
                    warnings.Add("网段过大，已跳过扫描: " + spec + "（主机数 " + hostCount + " > " + LanScanMaxHostsPerSubnet + "）");
                    continue;
                }

                uint first = networkAddress;
                uint last = networkAddress + (uint)((1L << (32 - prefixLength)) - 1);
                if (prefixLength < 31)
                {
                    first++;
                    last--;
                }

                for (uint current = first; current <= last; current++)
                {
                    string host = UInt32ToIpv4(current);
                    if (seen.Add(host))
                    {
                        result.Add(host);
                    }

                    if (current == UInt32.MaxValue)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private List<string> ProbeLanScanEndpoints(List<string> hosts)
        {
            List<string> result = new List<string>();
            if (hosts == null || hosts.Count == 0)
            {
                return result;
            }

            object sync = new object();
            Parallel.ForEach(
                hosts,
                new ParallelOptions { MaxDegreeOfParallelism = LanScanMaxDegreeOfParallelism },
                delegate(string host)
                {
                    if (ProbeTcpPort(host, LanScanPort, LanScanProbeTimeoutMs))
                    {
                        lock (sync)
                        {
                            result.Add(host + ":" + LanScanPort);
                        }
                    }
                });
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private bool ProbeTcpPort(string host, int port, int timeoutMs)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult asyncResult = client.BeginConnect(host, port, null, null);
                    try
                    {
                        if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
                        {
                            return false;
                        }

                        client.EndConnect(asyncResult);
                        return client.Connected;
                    }
                    finally
                    {
                        asyncResult.AsyncWaitHandle.Close();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseIpv4Cidr(string value, out uint networkAddress, out int prefixLength)
        {
            networkAddress = 0;
            prefixLength = 0;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Trim().Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(parts[0].Trim(), out ipAddress) || ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            if (!Int32.TryParse(parts[1].Trim(), out prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            uint mask = PrefixLengthToMask(prefixLength);
            networkAddress = Ipv4ToUInt32(ipAddress) & mask;
            return true;
        }

        private uint Ipv4ToUInt32(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private string UInt32ToIpv4(uint value)
        {
            return String.Format(
                "{0}.{1}.{2}.{3}",
                (value >> 24) & 0xFF,
                (value >> 16) & 0xFF,
                (value >> 8) & 0xFF,
                value & 0xFF);
        }

        private uint PrefixLengthToMask(int prefixLength)
        {
            if (prefixLength <= 0)
            {
                return 0;
            }
            if (prefixLength >= 32)
            {
                return UInt32.MaxValue;
            }

            return UInt32.MaxValue << (32 - prefixLength);
        }

        private int GetIpv4PrefixLength(IPAddress mask)
        {
            if (mask == null)
            {
                return 24;
            }

            byte[] bytes = mask.GetAddressBytes();
            int count = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                for (int bit = 7; bit >= 0; bit--)
                {
                    if ((value & (1 << bit)) != 0)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private Button MakeBrowseButton(TextBox target, string title, string mode)
        {
            Button button = new Button();
            button.Text = "浏览";
            button.Dock = DockStyle.Fill;
            button.Click += delegate
            {
                if (mode == "folder")
                {
                    using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = title;
                        if (Directory.Exists(target.Text))
                        {
                            dialog.SelectedPath = target.Text;
                        }
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            target.Text = dialog.SelectedPath;
                        }
                    }
                }
                else
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Title = title;
                        dialog.Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*";
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            target.Text = dialog.FileName;
                        }
                    }
                }
            };
            return button;
        }

        private Button MakeQuickToolCommandBrowseButton(TextBox target)
        {
            Button button = new Button();
            button.Text = "浏览";
            button.Dock = DockStyle.Fill;
            button.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择快捷命令文件";
                    dialog.Filter = "脚本/程序 (*.py;*.ps1;*.cmd;*.bat;*.exe;*.com)|*.py;*.ps1;*.cmd;*.bat;*.exe;*.com|所有文件 (*.*)|*.*";
                    if (!String.IsNullOrWhiteSpace(target.Text))
                    {
                        try
                        {
                            string initialFile = target.Text.Trim().Trim('"');
                            if (File.Exists(initialFile))
                            {
                                dialog.FileName = initialFile;
                            }
                        }
                        catch
                        {
                        }
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        target.Text = dialog.FileName;
                    }
                }
            };
            return button;
        }

        private string GetAdbPath()
        {
            if (String.IsNullOrWhiteSpace(config.AdbPath))
            {
                return "adb.exe";
            }
            return config.AdbPath;
        }

        private string GetScrcpyPath()
        {
            if (String.IsNullOrWhiteSpace(config.ScrcpyPath))
            {
                return "scrcpy.exe";
            }
            return config.ScrcpyPath;
        }

        private string GetPythonPath()
        {
            if (String.IsNullOrWhiteSpace(config.PythonPath))
            {
                return "python";
            }
            return config.PythonPath;
        }

        private string SelectedSerial()
        {
            if (devicesGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "请先选择一台 adb 设备。", "未选择设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return Convert.ToString(devicesGrid.SelectedRows[0].Cells["Serial"].Value);
        }

        private string SelectedSerialOrEmpty()
        {
            if (devicesGrid == null || devicesGrid.SelectedRows.Count == 0)
            {
                return "";
            }
            return Convert.ToString(devicesGrid.SelectedRows[0].Cells["Serial"].Value);
        }

        private DeviceInfo SelectedDeviceInfo()
        {
            return GetDeviceInfoFromContextRowOrSelection();
        }

        private DeviceInfo GetDeviceInfoFromContextRowOrSelection()
        {
            DataGridViewRow row = GetDeviceContextRowOrSelectedRow();
            if (row == null)
            {
                return null;
            }
            return row.Tag as DeviceInfo;
        }

        private DataGridViewRow GetDeviceContextRowOrSelectedRow()
        {
            if (devicesGrid == null || devicesGrid.IsDisposed)
            {
                return null;
            }

            if (deviceContextRowIndex >= 0 && deviceContextRowIndex < devicesGrid.Rows.Count)
            {
                return devicesGrid.Rows[deviceContextRowIndex];
            }

            if (devicesGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            return devicesGrid.SelectedRows[0];
        }

        private void CopySelectedAdbConnect()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial))
            {
                return;
            }

            string command = "adb connect " + info.Serial.Trim();
            Clipboard.SetText(command);
            AppendOutput("已复制 ADB 连接命令: " + command);
        }

        private void CopySelectedAdbShell()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial))
            {
                return;
            }

            string command = "adb -s " + info.Serial.Trim() + " shell";
            Clipboard.SetText(command);
            AppendOutput("已复制 ADB Shell 命令: " + command);
        }

        private void FreezeOrRestoreSelectedDevice()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial))
            {
                return;
            }

            if (IsFrozenDeviceState(info))
            {
                RestoreFrozenDevice(info.Serial);
                return;
            }

            if (IsConnectedDeviceState(info))
            {
                FreezeDevice(info.Serial);
            }
        }

        private void FreezeDevice(string serial)
        {
            if (String.IsNullOrWhiteSpace(serial))
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            string args = "disconnect " + QuoteArg(serial.Trim());
            RunAdbDeviceActionWithReconnectAsync(adb, serial, args, "冻结设备", IsAdbDisconnectSuccess, delegate(ProcessResult result)
            {
                MarkDeviceFrozen(serial);
            });
        }

        private void RestoreFrozenDevice(string serial)
        {
            if (String.IsNullOrWhiteSpace(serial))
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            string args = "connect " + QuoteArg(serial.Trim());
            RunAdbDeviceActionAsync(adb, args, delegate(ProcessResult result)
            {
                if (IsAdbConnectSuccess(result))
                {
                    MarkDeviceConnected(serial);
                    RefreshDevicesIncrementalFromCmd();
                }
                else
                {
                    RemoveDeviceRowBySerial(serial);
                    AppendOutput("恢复连接失败，已删除冻结设备: " + serial);
                }
            });
        }

        private void DeleteSelectedDevice()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial))
            {
                return;
            }

            string serial = info.Serial.Trim();
            if (IsFrozenDeviceState(info))
            {
                // freeze 状态已无 ADB 连接，只需要移除本地保留行。
                SetDeviceReconnectSuppressed(serial, true);
                RemoveDeviceRowBySerial(serial);
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            string args = "disconnect " + QuoteArg(serial);
            SetDeviceReconnectSuppressed(serial, true);
            AppendOutput("> " + adb + " " + args);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult disconnectResult = RunProcess(adb, args, null, false);
                bool disconnected = IsAdbDisconnectSuccess(disconnectResult);
                ProcessResult devicesResult = null;
                bool stillConnected = true;
                if (disconnected)
                {
                    devicesResult = RunProcess(adb, "devices -l", null, false);
                    stillConnected = devicesResult == null || devicesResult.ExitCode != 0 ||
                        IsDeviceListed(ParseDevices(devicesResult.Output), serial);
                }

                BeginInvokeIfAvailable(delegate
                {
                    AppendProcessResult(disconnectResult);
                    if (devicesResult != null)
                    {
                        AppendOutput("> " + adb + " devices -l [确认断开]");
                        AppendProcessResult(devicesResult);
                    }

                    if (disconnected && !stillConnected)
                    {
                        RemoveDeviceRowBySerial(serial);
                        return;
                    }

                    if (!disconnected)
                    {
                        SetDeviceReconnectSuppressed(serial, false);
                        AppendOutput("删除失败，ADB 未确认断开设备，已保留设备行: " + serial);
                    }
                    else
                    {
                        SetDeviceReconnectSuppressed(serial, false);
                        AppendOutput("删除失败，设备仍在 ADB 连接列表中，已保留设备行: " + serial);
                    }

                    RefreshDevicesIncrementalFromCmd();
                });
            });
        }

        private void RebootSelectedDevice()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial) || !IsConnectedDeviceState(info))
            {
                return;
            }

            string serial = info.Serial.Trim();
            if (MessageBox.Show(this, "确认重启设备「" + serial + "」？", "确认重启设备", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            string args = "-s " + QuoteArg(serial) + " reboot";
            RunAdbDeviceActionWithReconnectAsync(adb, serial, args, "重启设备", delegate(ProcessResult result)
            {
                return result != null && result.ExitCode == 0 && !ContainsAdbFailureText(((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant());
            }, delegate(ProcessResult result)
            {
                AppendOutput("已发送设备重启命令: " + serial);
            });
        }

        private void RunAdbDeviceActionAsync(string adb, string arguments, Action<ProcessResult> onCompleted)
        {
            AppendOutput("> " + adb + " " + arguments);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult result = RunProcess(adb, arguments, null, false);
                BeginInvokeIfAvailable(delegate
                {
                    AppendProcessResult(result);
                    if (onCompleted != null)
                    {
                        onCompleted(result);
                    }
                });
            });
        }

        private void RunAdbDeviceActionWithReconnectAsync(string adb, string serial, string arguments, string actionName, Func<ProcessResult, bool> isSuccess, Action<ProcessResult> onSuccess)
        {
            AppendOutput("> " + adb + " " + arguments);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult firstResult = RunProcess(adb, arguments, null, false);
                bool success = isSuccess != null && isSuccess(firstResult);
                string connectArguments = null;
                ProcessResult connectResult = null;
                ProcessResult stateResult = null;
                ProcessResult retryResult = null;
                bool reconnectAttempted = false;
                bool reconnectSucceeded = false;
                if (!success)
                {
                    reconnectAttempted = true;
                    reconnectSucceeded = TryReconnectDeviceCore(adb, serial, out connectArguments, out connectResult, out stateResult);
                    if (reconnectSucceeded)
                    {
                        retryResult = RunProcess(adb, arguments, null, false);
                        success = isSuccess != null && isSuccess(retryResult);
                    }
                }

                BeginInvokeIfAvailable(delegate
                {
                    AppendProcessResult(firstResult);
                    if (success)
                    {
                        if (reconnectSucceeded)
                        {
                            AppendOutput(actionName + " 失败后已重新连接设备: " + serial);
                            if (!String.IsNullOrWhiteSpace(connectArguments))
                            {
                                AppendOutput("> " + adb + " " + connectArguments);
                            }
                            if (connectResult != null)
                            {
                                AppendProcessResult(connectResult);
                            }
                            MarkDeviceConnected(serial);
                        }
                        if (retryResult != null)
                        {
                            AppendOutput("> " + adb + " " + arguments + " [重试]");
                            AppendProcessResult(retryResult);
                            if (onSuccess != null)
                            {
                                onSuccess(retryResult);
                            }
                        }
                        else if (onSuccess != null)
                        {
                            onSuccess(firstResult);
                        }
                        return;
                    }

                    if (reconnectAttempted)
                    {
                        AppendOutput(actionName + " 失败，尝试重新连接设备: " + serial);
                        if (!String.IsNullOrWhiteSpace(connectArguments))
                        {
                            AppendOutput("> " + adb + " " + connectArguments);
                        }
                        if (connectResult != null)
                        {
                            AppendProcessResult(connectResult);
                        }
                        if (reconnectSucceeded && retryResult != null)
                        {
                            MarkDeviceConnected(serial);
                            AppendOutput("> " + adb + " " + arguments + " [重试]");
                            AppendProcessResult(retryResult);
                        }
                    }

                    AppendOutput(actionName + " 仍失败，已刷新设备状态: " + serial);
                    RefreshDevicesIncrementalFromCmd();
                });
            });
        }

        private void CopySelectedDeviceCellValue(string columnName, int rowIndex)
        {
            string value = GetSelectedDeviceCellFullText(columnName, rowIndex);
            string label = GetDeviceColumnDisplayName(columnName);
            if (String.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Clipboard.SetText(value);
            AppendOutput("已复制" + label + ": " + value);
        }

        private string GetSelectedDeviceCellFullText(string columnName, int rowIndex)
        {
            DataGridViewRow row = GetDeviceRowByIndexOrSelection(rowIndex);
            DeviceInfo info = row == null ? SelectedDeviceInfo() : (row.Tag as DeviceInfo);
            if (info == null)
            {
                return GetDeviceCellDisplayedText(row, columnName);
            }

            if (columnName == "Serial")
            {
                return FirstNonEmpty(info.Serial, GetDeviceCellDisplayedText(row, columnName));
            }
            if (columnName == "Model")
            {
                return FirstNonEmpty(info.Model, GetDeviceCellDisplayedText(row, columnName));
            }
            if (columnName == "NetworkType")
            {
                return FirstNonEmpty(info.NetworkType, GetDeviceCellDisplayedText(row, columnName));
            }
            if (columnName == "EthernetIp")
            {
                return FirstNonEmpty(info.EthernetIp, GetDeviceCellDisplayedText(row, columnName));
            }
            if (columnName == "EthernetMac")
            {
                return FirstNonEmpty(info.EthernetMac, GetDeviceCellDisplayedText(row, columnName));
            }
            if (columnName == "BluetoothMac")
            {
                return FirstNonEmpty(info.BluetoothMac, GetDeviceCellDisplayedText(row, columnName));
            }
            return GetDeviceCellDisplayedText(row, columnName);
        }

        private DataGridViewRow GetDeviceRowByIndexOrSelection(int rowIndex)
        {
            if (devicesGrid == null || devicesGrid.IsDisposed)
            {
                return null;
            }

            if (rowIndex >= 0 && rowIndex < devicesGrid.Rows.Count)
            {
                return devicesGrid.Rows[rowIndex];
            }

            return GetDeviceContextRowOrSelectedRow();
        }

        private string GetDeviceCellDisplayedText(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || String.IsNullOrWhiteSpace(columnName) || !row.DataGridView.Columns.Contains(columnName))
            {
                return "";
            }

            object value = row.Cells[columnName].Value;
            return value == null ? "" : Convert.ToString(value);
        }

        private string FirstNonEmpty(string preferred, string fallback)
        {
            return String.IsNullOrWhiteSpace(preferred) ? (fallback ?? "") : preferred;
        }

        private string GetDeviceCellCopyMenuLabel(string columnName)
        {
            if (columnName == "NetworkType")
            {
                return "";
            }

            string displayName = GetDeviceColumnDisplayName(columnName);
            if (String.IsNullOrWhiteSpace(displayName))
            {
                return "";
            }
            return "复制" + displayName;
        }

        private string GetDeviceColumnDisplayName(string columnName)
        {
            if (columnName == "Serial")
            {
                return "序列号/IP";
            }
            if (columnName == "Model")
            {
                return "型号";
            }
            if (columnName == "NetworkType")
            {
                return "网络";
            }
            if (columnName == "EthernetIp")
            {
                return "以太网 IP";
            }
            if (columnName == "EthernetMac")
            {
                return "以太网 MAC";
            }
            if (columnName == "BluetoothMac")
            {
                return "蓝牙 MAC";
            }
            return "";
        }

        private bool IsConnectedDeviceState(DeviceInfo info)
        {
            return info != null && IsConnectedDeviceState(info.State);
        }

        private bool IsConnectedDeviceState(string state)
        {
            return String.Equals(state, "device", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsFrozenDeviceState(DeviceInfo info)
        {
            return info != null && IsFrozenDeviceState(info.State);
        }

        private bool IsFrozenDeviceState(string state)
        {
            return String.Equals(state, "freeze", StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeDeviceSerialKey(string serial)
        {
            return String.IsNullOrWhiteSpace(serial) ? "" : serial.Trim();
        }

        private bool ContainsDeviceSerial(List<string> serials, string serial)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key) || serials == null)
            {
                return false;
            }

            for (int i = 0; i < serials.Count; i++)
            {
                if (String.Equals(NormalizeDeviceSerialKey(serials[i]), key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void RemoveDeviceSerial(List<string> serials, string serial)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key) || serials == null)
            {
                return;
            }

            for (int i = serials.Count - 1; i >= 0; i--)
            {
                if (String.Equals(NormalizeDeviceSerialKey(serials[i]), key, StringComparison.OrdinalIgnoreCase))
                {
                    serials.RemoveAt(i);
                }
            }
        }

        private bool IsScrcpyHighQualityDisabled(string serial)
        {
            return config != null && ContainsDeviceSerial(config.ScrcpyHighQualityDisabledSerials, serial);
        }

        private bool IsScrcpyHighQualityForced(string serial)
        {
            return config != null && ContainsDeviceSerial(config.ScrcpyHighQualityForcedSerials, serial);
        }

        private void ClearScrcpyHighQualityOverride(string serial)
        {
            if (config.ScrcpyHighQualityDisabledSerials == null)
            {
                config.ScrcpyHighQualityDisabledSerials = new List<string>();
            }
            if (config.ScrcpyHighQualityForcedSerials == null)
            {
                config.ScrcpyHighQualityForcedSerials = new List<string>();
            }

            RemoveDeviceSerial(config.ScrcpyHighQualityDisabledSerials, serial);
            RemoveDeviceSerial(config.ScrcpyHighQualityForcedSerials, serial);
            SaveConfig();
        }

        private void SetScrcpyHighQualityDisabled(string serial)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (config.ScrcpyHighQualityDisabledSerials == null)
            {
                config.ScrcpyHighQualityDisabledSerials = new List<string>();
            }
            if (config.ScrcpyHighQualityForcedSerials == null)
            {
                config.ScrcpyHighQualityForcedSerials = new List<string>();
            }

            RemoveDeviceSerial(config.ScrcpyHighQualityDisabledSerials, key);
            RemoveDeviceSerial(config.ScrcpyHighQualityForcedSerials, key);
            config.ScrcpyHighQualityDisabledSerials.Add(key);
            SaveConfig();
        }

        private void SetScrcpyHighQualityForced(string serial)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (config.ScrcpyHighQualityDisabledSerials == null)
            {
                config.ScrcpyHighQualityDisabledSerials = new List<string>();
            }
            if (config.ScrcpyHighQualityForcedSerials == null)
            {
                config.ScrcpyHighQualityForcedSerials = new List<string>();
            }

            RemoveDeviceSerial(config.ScrcpyHighQualityDisabledSerials, key);
            RemoveDeviceSerial(config.ScrcpyHighQualityForcedSerials, key);
            config.ScrcpyHighQualityForcedSerials.Add(key);
            SaveConfig();
        }

        private string GetScrcpyHighQualityMenuText(DeviceInfo info)
        {
            string serial = info == null ? "" : info.Serial;
            return ShouldUseScrcpyHighQuality(info, serial) ? "禁用高画质" : "启用高画质";
        }

        private void ToggleSelectedScrcpyHighQuality()
        {
            DeviceInfo info = SelectedDeviceInfo();
            if (info == null || String.IsNullOrWhiteSpace(info.Serial))
            {
                return;
            }

            if (ShouldUseScrcpyHighQuality(info, info.Serial))
            {
                SetScrcpyHighQualityDisabled(info.Serial);
                AppendOutput("已禁用高画质: " + info.Serial);
            }
            else
            {
                SetScrcpyHighQualityForced(info.Serial);
                AppendOutput("已启用高画质: " + info.Serial);
            }
        }

        private bool IsAdbConnectSuccess(ProcessResult result)
        {
            if (result == null || result.ExitCode != 0)
            {
                return false;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant();
            if (ContainsAdbFailureText(text))
            {
                return false;
            }

            return text.IndexOf("connected to", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("already connected", StringComparison.Ordinal) >= 0;
        }

        private bool IsAdbDisconnectSuccess(ProcessResult result)
        {
            if (result == null || result.ExitCode != 0)
            {
                return false;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant();
            if (ContainsAdbFailureText(text))
            {
                return false;
            }

            return text.IndexOf("disconnected", StringComparison.Ordinal) >= 0 ||
                String.IsNullOrWhiteSpace(text);
        }

        /// <summary>
        /// 判断目标序列号是否仍存在于 adb devices -l 的当前结果中。
        /// </summary>
        private bool IsDeviceListed(List<DeviceInfo> devices, string serial)
        {
            if (devices == null || String.IsNullOrWhiteSpace(serial))
            {
                return false;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo item = devices[i];
                if (item != null && String.Equals(item.Serial, serial, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 设置设备是否禁止被后台失败重试逻辑自动重新连接。
        /// </summary>
        private void SetDeviceReconnectSuppressed(string serial, bool suppressed)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (deletedDeviceSerialsLock)
            {
                if (suppressed)
                {
                    deletedDeviceSerials.Add(key);
                }
                else
                {
                    deletedDeviceSerials.Remove(key);
                }
            }
        }

        /// <summary>
        /// 判断是否应禁止后台为该设备执行自动重连。
        /// </summary>
        private bool IsDeviceReconnectSuppressed(string serial)
        {
            string key = NormalizeDeviceSerialKey(serial);
            if (String.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (deletedDeviceSerialsLock)
            {
                return deletedDeviceSerials.Contains(key);
            }
        }

        private bool ContainsAdbFailureText(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.IndexOf("failed", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("unable", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("cannot", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("offline", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("unauthorized", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("no devices/emulators found", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("not found", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("no such", StringComparison.Ordinal) >= 0;
        }

        private bool ShouldReconnectForDeviceCommandResult(ProcessResult result)
        {
            if (result == null)
            {
                return true;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant();
            if (result.ExitCode != 0)
            {
                return true;
            }

            return text.IndexOf("device offline", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("device unauthorized", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("device '", StringComparison.Ordinal) >= 0 && text.IndexOf("not found", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("no devices/emulators found", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("more than one device/emulator", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("closed", StringComparison.Ordinal) >= 0;
        }

        private string BuildAdbGetStateArguments(string serial)
        {
            return "-s " + QuoteArg((serial ?? "").Trim()) + " get-state";
        }

        private bool IsAdbGetStateOnline(ProcessResult result)
        {
            if (result == null || result.ExitCode != 0)
            {
                return false;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).Trim();
            return text.IndexOf("device", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("offline", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private bool TryReconnectDeviceCore(string adb, string serial, out string connectArguments, out ProcessResult connectResult, out ProcessResult stateResult)
        {
            if (IsDeviceReconnectSuppressed(serial))
            {
                connectArguments = null;
                connectResult = null;
                stateResult = null;
                return false;
            }

            connectArguments = "connect " + QuoteArg((serial ?? "").Trim());
            connectResult = RunProcess(adb, connectArguments, null, true);
            stateResult = null;
            if (!IsAdbConnectSuccess(connectResult))
            {
                return false;
            }

            stateResult = RunProcess(adb, BuildAdbGetStateArguments(serial), null, false);
            return IsAdbGetStateOnline(stateResult);
        }

        private void EnsureDeviceOnlineBeforeActionAsync(string serial, string actionName, Action<bool> onCompleted)
        {
            if (String.IsNullOrWhiteSpace(serial))
            {
                if (onCompleted != null)
                {
                    onCompleted(false);
                }
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                if (onCompleted != null)
                {
                    onCompleted(false);
                }
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult stateResult = RunProcess(adb, BuildAdbGetStateArguments(serial), null, false);
                string connectArguments = null;
                ProcessResult connectResult = null;
                ProcessResult reconnectStateResult = null;
                bool online = IsAdbGetStateOnline(stateResult);
                if (!online)
                {
                    AppendOutput(actionName + " 前检测到设备可能已离线，尝试重新连接: " + serial);
                    online = TryReconnectDeviceCore(adb, serial, out connectArguments, out connectResult, out reconnectStateResult);
                }

                BeginInvokeIfAvailable(delegate
                {
                    if (connectResult != null)
                    {
                        AppendOutput("> " + adb + " " + connectArguments);
                        AppendProcessResult(connectResult);
                    }
                    if (online)
                    {
                        if (connectResult != null)
                        {
                            MarkDeviceConnected(serial);
                            RefreshDevicesIncrementalFromCmd();
                        }
                    }
                    else
                    {
                        AppendOutput(actionName + " 前重新连接失败，已刷新设备状态: " + serial);
                        RefreshDevicesIncrementalFromCmd();
                    }
                    if (onCompleted != null)
                    {
                        onCompleted(online);
                    }
                });
            });
        }

        private void UpdateSelectedDeviceHeader()
        {
            if (selectedDeviceHeaderLabel == null)
            {
                return;
            }

            string serial = SelectedSerialOrEmpty();
            selectedDeviceHeaderLabel.Text = String.IsNullOrWhiteSpace(serial) ? "设备: 未选择" : "设备: " + serial;
            PositionSelectedDeviceHeader();
        }

        /// <summary>
        /// 响应设备选择变化，立即撤销旧设备数据并在选择稳定后刷新文件和应用列表。
        /// </summary>
        private void OnSelectedDeviceChanged()
        {
            UpdateSelectedDeviceHeader();

            string serial = SelectedSerialOrEmpty();
            DeviceInfo selectedDevice = SelectedGridDeviceInfo();
            bool isFrozen = IsFrozenDeviceState(selectedDevice);
            if (String.Equals(serial, lastSelectedDeviceDataSerial, StringComparison.OrdinalIgnoreCase)
                && isFrozen == lastSelectedDeviceDataWasFrozen)
            {
                return;
            }

            lastSelectedDeviceDataSerial = serial;
            lastSelectedDeviceDataWasFrozen = isFrozen;
            pendingSelectedDeviceDataSerial = serial;
            ClearManagedDeviceData();

            if (selectedDeviceDataRefreshTimer == null)
            {
                return;
            }

            selectedDeviceDataRefreshTimer.Stop();
            if (isFrozen)
            {
                // freeze 设备不能执行后续 adb 查询，避免选择操作触发隐式重连。
                pendingSelectedDeviceDataSerial = "";
                return;
            }

            if (!String.IsNullOrWhiteSpace(serial))
            {
                selectedDeviceDataRefreshTimer.Start();
            }
        }

        /// <summary>
        /// 在设备选择完成后，读取当前设备的文件与应用数据。
        /// </summary>
        private void RefreshSelectedDeviceDataAfterSelectionChange()
        {
            if (selectedDeviceDataRefreshTimer != null)
            {
                selectedDeviceDataRefreshTimer.Stop();
            }

            string serial = pendingSelectedDeviceDataSerial;
            if (String.IsNullOrWhiteSpace(serial) ||
                !String.Equals(serial, SelectedSerialOrEmpty(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (IsFrozenDeviceState(SelectedGridDeviceInfo()))
            {
                return;
            }

            RefreshFiles();
            RefreshApps();
        }

        /// <summary>
        /// 直接读取当前选中设备行，避免使用右键菜单上下文行。
        /// </summary>
        private DeviceInfo SelectedGridDeviceInfo()
        {
            if (devicesGrid == null || devicesGrid.IsDisposed || devicesGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            return devicesGrid.SelectedRows[0].Tag as DeviceInfo;
        }

        /// <summary>
        /// 清除旧设备的右侧数据，并使尚未完成的查询结果失效。
        /// </summary>
        private void ClearManagedDeviceData()
        {
            unchecked
            {
                fileRefreshRequestId++;
                appRefreshRequestId++;
            }

            currentFileEntries.Clear();
            if (filesGrid != null && !filesGrid.IsDisposed)
            {
                filesGrid.Rows.Clear();
            }
            if (appsGrid != null && !appsGrid.IsDisposed)
            {
                appsGrid.Rows.Clear();
            }
        }

        private void RefreshDevices()
        {
            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            int requestId = ++deviceRefreshRequestId;
            AppendOutput("> " + adb + " devices -l");
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult result = RunProcess(adb, "devices -l", null, false);
                List<DeviceInfo> devices = ParseDevices(result.Output);
                BeginInvokeIfAvailable(delegate
                {
                    ApplyDeviceList(requestId, result, devices);
                });
            });
        }

        private void RefreshDevicesFromButton()
        {
            DateTime now = DateTime.UtcNow;
            if (now - lastManualDeviceRefreshUtc < TimeSpan.FromSeconds(5))
            {
                AppendOutput("刷新设备已忽略: 5 秒内重复点击");
                return;
            }

            lastManualDeviceRefreshUtc = now;
            RefreshDevices();
        }

        private void RefreshDevicesIncrementalFromCmd()
        {
            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            int requestId = ++deviceRefreshRequestId;
            AppendOutput("> " + adb + " devices -l (增量)");
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult result = RunProcess(adb, "devices -l", null, false);
                List<DeviceInfo> devices = ParseDevices(result.Output);
                BeginInvokeIfAvailable(delegate
                {
                    ApplyDeviceListIncremental(requestId, result, devices);
                });
            });
        }

        private void ApplyDeviceList(int requestId, ProcessResult result, List<DeviceInfo> devices)
        {
            if (requestId != deviceRefreshRequestId || devicesGrid == null || devicesGrid.IsDisposed)
            {
                return;
            }

            if (!String.IsNullOrWhiteSpace(result.Error))
            {
                AppendOutput(result.Error);
            }

            string selectedSerial = SelectedSerialOrEmpty();
            Dictionary<string, DeviceInfo> frozen = GetFrozenDevicesBySerial();
            Dictionary<string, DeviceInfo> latest = BuildDeviceDictionary(devices);
            List<DeviceInfo> detailDevices = new List<DeviceInfo>();

            devicesGrid.Rows.Clear();
            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo d = devices[i];
                DeviceInfo frozenInfo;
                if (frozen.TryGetValue(d.Serial, out frozenInfo))
                {
                    if (!IsConnectedDeviceState(d))
                    {
                        continue;
                    }
                    PreserveDeviceDetails(frozenInfo, d);
                }

                AddDeviceRow(d, selectedSerial);
                if (IsConnectedDeviceState(d))
                {
                    detailDevices.Add(d);
                }
            }

            foreach (DeviceInfo frozenInfo in frozen.Values)
            {
                DeviceInfo incoming;
                if (!latest.TryGetValue(frozenInfo.Serial, out incoming) || !IsConnectedDeviceState(incoming))
                {
                    AddDeviceRow(frozenInfo, selectedSerial);
                }
            }

            AppendOutput("发现设备: " + devicesGrid.Rows.Count);
            UpdateSelectedDeviceHeader();
            StartPopulateDeviceDetails(requestId, detailDevices);
        }

        private void ApplyDeviceListIncremental(int requestId, ProcessResult result, List<DeviceInfo> devices)
        {
            if (requestId != deviceRefreshRequestId || devicesGrid == null || devicesGrid.IsDisposed)
            {
                return;
            }

            if (!String.IsNullOrWhiteSpace(result.Error))
            {
                AppendOutput(result.Error);
            }

            Dictionary<string, DeviceInfo> latest = new Dictionary<string, DeviceInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < devices.Count; i++)
            {
                if (!String.IsNullOrWhiteSpace(devices[i].Serial))
                {
                    latest[devices[i].Serial] = devices[i];
                }
            }

            string selectedSerial = SelectedSerialOrEmpty();
            int removedCount = 0;
            for (int i = devicesGrid.Rows.Count - 1; i >= 0; i--)
            {
                DataGridViewRow row = devicesGrid.Rows[i];
                DeviceInfo existing = row.Tag as DeviceInfo;
                string serial = existing == null ? Convert.ToString(row.Cells["Serial"].Value) : existing.Serial;
                if (String.IsNullOrWhiteSpace(serial) || !latest.ContainsKey(serial))
                {
                    if (IsFrozenDeviceState(existing))
                    {
                        continue;
                    }

                    devicesGrid.Rows.RemoveAt(i);
                    removedCount++;
                }
            }

            int addedCount = 0;
            List<DeviceInfo> detailDevices = new List<DeviceInfo>();
            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo incoming = devices[i];
                DataGridViewRow row = FindDeviceRowBySerial(incoming.Serial);
                if (row == null)
                {
                    row = AddDeviceRow(incoming, selectedSerial);
                    if (IsConnectedDeviceState(incoming))
                    {
                        detailDevices.Add(incoming);
                    }
                    addedCount++;
                }
                else
                {
                    DeviceInfo existing = row.Tag as DeviceInfo;
                    if (IsFrozenDeviceState(existing) && !IsConnectedDeviceState(incoming))
                    {
                        if (!String.IsNullOrEmpty(selectedSerial) && String.Equals(selectedSerial, incoming.Serial, StringComparison.OrdinalIgnoreCase))
                        {
                            row.Selected = true;
                        }
                        continue;
                    }

                    bool wasFrozen = IsFrozenDeviceState(existing);
                    if (wasFrozen)
                    {
                        PreserveDeviceDetails(existing, incoming);
                        UpdateDeviceRow(row, incoming);
                        detailDevices.Add(incoming);
                    }
                    else
                    {
                        if (existing == null)
                        {
                            existing = incoming;
                            row.Tag = existing;
                        }
                        else
                        {
                            PreserveDeviceDetails(existing, incoming);
                            existing.State = incoming.State;
                            existing.Model = incoming.Model;
                            existing.Raw = incoming.Raw;
                        }
                        UpdateDeviceRow(row, existing);
                    }
                }

                if (!String.IsNullOrEmpty(selectedSerial) && String.Equals(selectedSerial, incoming.Serial, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                }
            }

            AppendOutput("设备列表增量刷新: 新增 " + addedCount + "，移除 " + removedCount + "，当前 " + devicesGrid.Rows.Count);
            UpdateSelectedDeviceHeader();
            StartPopulateDeviceDetails(requestId, detailDevices);
        }

        private Dictionary<string, DeviceInfo> BuildDeviceDictionary(List<DeviceInfo> devices)
        {
            Dictionary<string, DeviceInfo> result = new Dictionary<string, DeviceInfo>(StringComparer.OrdinalIgnoreCase);
            if (devices == null)
            {
                return result;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo info = devices[i];
                if (info != null && !String.IsNullOrWhiteSpace(info.Serial))
                {
                    result[info.Serial] = info;
                }
            }
            return result;
        }

        private Dictionary<string, DeviceInfo> GetFrozenDevicesBySerial()
        {
            Dictionary<string, DeviceInfo> result = new Dictionary<string, DeviceInfo>(StringComparer.OrdinalIgnoreCase);
            if (devicesGrid == null || devicesGrid.IsDisposed)
            {
                return result;
            }

            for (int i = 0; i < devicesGrid.Rows.Count; i++)
            {
                DeviceInfo info = devicesGrid.Rows[i].Tag as DeviceInfo;
                if (IsFrozenDeviceState(info) && !String.IsNullOrWhiteSpace(info.Serial))
                {
                    result[info.Serial] = info.Clone();
                }
            }
            return result;
        }

        private DataGridViewRow AddDeviceRow(DeviceInfo info, string selectedSerial)
        {
            if (info == null || devicesGrid == null || devicesGrid.IsDisposed)
            {
                return null;
            }

            int rowIndex = devicesGrid.Rows.Add(info.Serial, info.State, info.Model, info.NetworkType, info.EthernetIp, info.EthernetMac, info.BluetoothMac, info.AndroidVersion);
            DataGridViewRow row = devicesGrid.Rows[rowIndex];
            row.Tag = info;
            if (!String.IsNullOrEmpty(selectedSerial) && String.Equals(selectedSerial, info.Serial, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
            }
            return row;
        }

        private void UpdateDeviceRow(DataGridViewRow row, DeviceInfo info)
        {
            if (row == null || info == null)
            {
                return;
            }

            row.Tag = info;
            row.Cells["Serial"].Value = info.Serial;
            row.Cells["State"].Value = info.State;
            row.Cells["Model"].Value = info.Model;
            row.Cells["NetworkType"].Value = info.NetworkType;
            row.Cells["EthernetIp"].Value = info.EthernetIp;
            row.Cells["EthernetMac"].Value = info.EthernetMac;
            row.Cells["BluetoothMac"].Value = info.BluetoothMac;
            row.Cells["AndroidVersion"].Value = info.AndroidVersion;
        }

        private void PreserveDeviceDetails(DeviceInfo source, DeviceInfo target)
        {
            if (source == null || target == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(target.Model))
            {
                target.Model = source.Model;
            }
            if (String.IsNullOrWhiteSpace(target.NetworkType))
            {
                target.NetworkType = source.NetworkType;
            }
            if (String.IsNullOrWhiteSpace(target.EthernetIp))
            {
                target.EthernetIp = source.EthernetIp;
            }
            if (String.IsNullOrWhiteSpace(target.EthernetMac))
            {
                target.EthernetMac = source.EthernetMac;
            }
            if (String.IsNullOrWhiteSpace(target.BluetoothMac))
            {
                target.BluetoothMac = source.BluetoothMac;
            }
            if (String.IsNullOrWhiteSpace(target.AndroidVersion))
            {
                target.AndroidVersion = source.AndroidVersion;
            }
        }

        private DataGridViewRow FindDeviceRowBySerial(string serial)
        {
            if (devicesGrid == null || String.IsNullOrWhiteSpace(serial))
            {
                return null;
            }

            for (int i = 0; i < devicesGrid.Rows.Count; i++)
            {
                DataGridViewRow row = devicesGrid.Rows[i];
                DeviceInfo info = row.Tag as DeviceInfo;
                string rowSerial = info == null ? Convert.ToString(row.Cells["Serial"].Value) : info.Serial;
                if (String.Equals(rowSerial, serial, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
            return null;
        }

        private void MarkDeviceFrozen(string serial)
        {
            DataGridViewRow row = FindDeviceRowBySerial(serial);
            if (row == null)
            {
                return;
            }

            DeviceInfo info = row.Tag as DeviceInfo;
            if (info == null)
            {
                info = new DeviceInfo();
                info.Serial = serial;
            }

            info.State = "freeze";
            UpdateDeviceRow(row, info);
            deviceRefreshRequestId++;
            UpdateSelectedDeviceHeader();
            AppendOutput("设备已冻结: " + serial);
        }

        private void MarkDeviceConnected(string serial)
        {
            SetDeviceReconnectSuppressed(serial, false);
            DataGridViewRow row = FindDeviceRowBySerial(serial);
            if (row == null)
            {
                return;
            }

            DeviceInfo info = row.Tag as DeviceInfo;
            if (info == null)
            {
                info = new DeviceInfo();
                info.Serial = serial;
            }

            info.State = "device";
            UpdateDeviceRow(row, info);
            UpdateSelectedDeviceHeader();
            AppendOutput("设备已恢复连接: " + serial);
        }

        private void RemoveDeviceRowBySerial(string serial)
        {
            DataGridViewRow row = FindDeviceRowBySerial(serial);
            if (row == null || devicesGrid == null || devicesGrid.IsDisposed)
            {
                return;
            }

            devicesGrid.Rows.RemoveAt(row.Index);
            deviceRefreshRequestId++;
            UpdateSelectedDeviceHeader();
            AppendOutput("设备已删除: " + serial);
        }

        private void StartPopulateDeviceDetails(int requestId, List<DeviceInfo> devices)
        {
            if (devices == null || devices.Count == 0)
            {
                return;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo info = devices[i];
                ThreadPool.QueueUserWorkItem(delegate
                {
                    if (requestId != deviceRefreshRequestId)
                    {
                        return;
                    }

                    PopulateDeviceDetails(info);
                    BeginInvokeIfAvailable(delegate
                    {
                        ApplyDeviceDetails(requestId, info);
                    });
                });
            }
        }

        private void ApplyDeviceDetails(int requestId, DeviceInfo info)
        {
            if (requestId != deviceRefreshRequestId || info == null || devicesGrid == null || devicesGrid.IsDisposed)
            {
                return;
            }

            for (int i = 0; i < devicesGrid.Rows.Count; i++)
            {
                DataGridViewRow row = devicesGrid.Rows[i];
                DeviceInfo rowInfo = row.Tag as DeviceInfo;
                if (rowInfo == null || !String.Equals(rowInfo.Serial, info.Serial, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsFrozenDeviceState(rowInfo))
                {
                    break;
                }

                DeviceInfo target = rowInfo ?? new DeviceInfo();
                target.Serial = FirstNonEmpty(info.Serial, target.Serial);
                target.State = FirstNonEmpty(info.State, target.State);
                target.Model = FirstNonEmpty(info.Model, target.Model);
                target.EthernetIp = FirstNonEmpty(info.EthernetIp, target.EthernetIp);
                target.EthernetMac = FirstNonEmpty(info.EthernetMac, target.EthernetMac);
                target.BluetoothMac = FirstNonEmpty(info.BluetoothMac, target.BluetoothMac);
                target.AndroidVersion = FirstNonEmpty(info.AndroidVersion, target.AndroidVersion);
                target.NetworkType = FirstNonEmpty(info.NetworkType, target.NetworkType);
                target.Raw = FirstNonEmpty(info.Raw, target.Raw);
                if (String.IsNullOrWhiteSpace(target.NetworkType) && !String.IsNullOrWhiteSpace(target.EthernetIp))
                {
                    target.NetworkType = "有线";
                }

                UpdateDeviceRow(row, target);
                break;
            }
        }

        private void BeginInvokeIfAvailable(Action action)
        {
            if (action == null || IsDisposed)
            {
                return;
            }

            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(action);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private List<DeviceInfo> ParseDevices(string output)
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();
            if (String.IsNullOrWhiteSpace(output))
            {
                return devices;
            }

            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = SplitWhitespace(line);
                if (parts.Length < 2)
                {
                    continue;
                }

                DeviceInfo info = new DeviceInfo();
                info.Serial = parts[0];
                info.State = parts[1];
                info.Raw = line;

                for (int p = 2; p < parts.Length; p++)
                {
                    string item = parts[p];
                    int idx = item.IndexOf(':');
                    if (idx <= 0)
                    {
                        continue;
                    }

                    string key = item.Substring(0, idx);
                    string value = item.Substring(idx + 1);
                    if (key == "model")
                    {
                        info.Model = value;
                    }
                }
                devices.Add(info);
            }
            return devices;
        }

        private void PopulateDeviceDetails(DeviceInfo info)
        {
            if (info == null || String.IsNullOrWhiteSpace(info.Serial) || !IsConnectedDeviceState(info))
            {
                return;
            }

            Dictionary<string, string> props = ReadDeviceGetPropMap(info.Serial);
            string eth0IpAddrOutput = ReadDeviceShellRawValue(info.Serial, "ip addr show eth0 2>/dev/null");
            string eth0IfconfigOutput = ReadDeviceShellRawValue(info.Serial, "ifconfig eth0 2>/dev/null");
            string wlan0IpAddrOutput = ReadDeviceShellRawValue(info.Serial, "ip addr show wlan0 2>/dev/null");
            string wlan0IfconfigOutput = ReadDeviceShellRawValue(info.Serial, "ifconfig wlan0 2>/dev/null");
            string defaultRouteOutput = ReadDeviceShellRawValue(info.Serial, "ip route get 8.8.8.8 2>/dev/null || ip route show default 2>/dev/null");
            string connectivityOutput = ReadDeviceShellRawValue(info.Serial, "dumpsys connectivity 2>/dev/null");
            info.EthernetIp = FirstNonEmpty(
                GetDevicePropValue(props, "dhcp.eth0.ipaddress"),
                GetDevicePropValue(props, "dhcp.eth1.ipaddress"),
                ExtractFirstIpv4(eth0IpAddrOutput),
                ExtractFirstIpv4(eth0IfconfigOutput));
            string wifiIp = FirstNonEmpty(
                GetDevicePropValue(props, "dhcp.wlan0.ipaddress"),
                GetDevicePropValue(props, "dhcp.wlan1.ipaddress"),
                ExtractFirstIpv4(wlan0IpAddrOutput),
                ExtractFirstIpv4(wlan0IfconfigOutput));
            string mobileType = FirstNonEmpty(
                GetDevicePropValue(props, "gsm.network.type"),
                GetDevicePropValue(props, "gsm.network.type.0"));
            info.EthernetMac = FirstNonEmpty(
                ReadDeviceShellValue(info.Serial, "cat /sys/class/net/eth0/address 2>/dev/null"),
                ReadDeviceShellValue(info.Serial, "cat /sys/class/net/eth1/address 2>/dev/null"));
            info.BluetoothMac = FirstNonEmpty(
                ReadDeviceShellValue(info.Serial, "settings get secure bluetooth_address 2>/dev/null"),
                GetDevicePropValue(props, "bluetooth.bdaddr"),
                GetDevicePropValue(props, "persist.service.bdroid.bdaddr"),
                GetDevicePropValue(props, "ro.boot.btmacaddr"));
            info.AndroidVersion = FirstNonEmpty(
                GetDevicePropValue(props, "ro.build.version.release"),
                GetDevicePropValue(props, "ro.system.build.version.release"),
                GetDevicePropValue(props, "ro.build.version.release_or_codename"),
                GetDevicePropValue(props, "ro.vendor.build.version.release"));
            info.NetworkType = DetectNetworkType(defaultRouteOutput, connectivityOutput, info.EthernetIp, wifiIp, mobileType);
            if (String.IsNullOrWhiteSpace(info.EthernetIp))
            {
                info.EthernetIp = ExtractIpv4FromAdbSerial(info.Serial);
            }
        }

        private string DetectNetworkType(string defaultRouteOutput, string connectivityOutput, string ethernetIp, string wifiIp, string mobileType)
        {
            string routeInterfaceName = ExtractDefaultRouteInterfaceName(defaultRouteOutput);
            string connectivityNetworkType = DetectNetworkTypeFromConnectivity(connectivityOutput, routeInterfaceName, mobileType);
            if (!String.IsNullOrWhiteSpace(connectivityNetworkType))
            {
                return connectivityNetworkType;
            }

            string routeNetworkType = DetectNetworkTypeFromInterfaceName(routeInterfaceName, mobileType);
            if (!String.IsNullOrWhiteSpace(routeNetworkType))
            {
                return routeNetworkType;
            }

            if (!String.IsNullOrWhiteSpace(ethernetIp))
            {
                return "有线";
            }

            if (!String.IsNullOrWhiteSpace(wifiIp))
            {
                return "WiFi";
            }

            string normalized = NormalizeMobileNetworkType(mobileType);
            if (!String.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            return "";
        }

        private string DetectNetworkTypeFromConnectivity(string connectivityOutput, string routeInterfaceName, string mobileType)
        {
            if (String.IsNullOrWhiteSpace(connectivityOutput))
            {
                return "";
            }

            string[] lines = connectivityOutput.Replace("\r\n", "\n").Split('\n');
            string normalizedRouteInterface = NormalizeNetworkInterfaceName(routeInterfaceName);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!String.IsNullOrWhiteSpace(normalizedRouteInterface) &&
                    line.IndexOf("InterfaceName: " + normalizedRouteInterface, StringComparison.OrdinalIgnoreCase) < 0 &&
                    line.IndexOf("InterfaceName=" + normalizedRouteInterface, StringComparison.OrdinalIgnoreCase) < 0 &&
                    line.IndexOf(" " + normalizedRouteInterface + " ", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string detected = DetectNetworkTypeFromConnectivityLine(line, mobileType);
                if (!String.IsNullOrWhiteSpace(detected))
                {
                    return detected;
                }
            }

            return "";
        }

        private string DetectNetworkTypeFromConnectivityLine(string line, string mobileType)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                return "";
            }

            if (line.IndexOf("Transports: CELLULAR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("TRANSPORT_CELLULAR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("MOBILE[", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("type: MOBILE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string normalized = NormalizeMobileNetworkType(mobileType);
                return String.IsNullOrWhiteSpace(normalized) ? "移动网络" : normalized;
            }

            if (line.IndexOf("Transports: WIFI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("TRANSPORT_WIFI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("type: WIFI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "WiFi";
            }

            if (line.IndexOf("Transports: ETHERNET", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("TRANSPORT_ETHERNET", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("type: ETHERNET", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "有线";
            }

            if (line.IndexOf("Transports: VPN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("TRANSPORT_VPN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "VPN";
            }

            return "";
        }

        private string ExtractDefaultRouteInterfaceName(string routeOutput)
        {
            if (String.IsNullOrWhiteSpace(routeOutput))
            {
                return "";
            }

            string[] lines = routeOutput.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = SplitWhitespace(line);
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    if (!String.Equals(parts[p], "dev", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string interfaceName = NormalizeNetworkInterfaceName(parts[p + 1]);
                    if (!String.IsNullOrWhiteSpace(interfaceName))
                    {
                        return interfaceName;
                    }
                }
            }

            return "";
        }

        private string NormalizeNetworkInterfaceName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return value.Trim().Trim('"', '\'', ',', ';', ':');
        }

        private string DetectNetworkTypeFromInterfaceName(string interfaceName, string mobileType)
        {
            if (String.IsNullOrWhiteSpace(interfaceName))
            {
                return "";
            }

            string lower = interfaceName.Trim().ToLowerInvariant();
            if (lower.StartsWith("wlan", StringComparison.Ordinal) ||
                lower.StartsWith("wifi", StringComparison.Ordinal))
            {
                return "WiFi";
            }

            if (lower.StartsWith("rmnet", StringComparison.Ordinal) ||
                lower.StartsWith("ccmni", StringComparison.Ordinal) ||
                lower.StartsWith("ccinet", StringComparison.Ordinal) ||
                lower.StartsWith("pdp", StringComparison.Ordinal) ||
                lower.StartsWith("wwan", StringComparison.Ordinal))
            {
                string normalized = NormalizeMobileNetworkType(mobileType);
                return String.IsNullOrWhiteSpace(normalized) ? "移动网络" : normalized;
            }

            if (lower.StartsWith("eth", StringComparison.Ordinal) ||
                lower.StartsWith("en", StringComparison.Ordinal) ||
                lower.StartsWith("lan", StringComparison.Ordinal) ||
                lower.StartsWith("br", StringComparison.Ordinal))
            {
                return "有线";
            }

            if (lower.StartsWith("rndis", StringComparison.Ordinal) ||
                lower.StartsWith("usb", StringComparison.Ordinal))
            {
                string normalized = NormalizeMobileNetworkType(mobileType);
                if (!String.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }

                return "USB";
            }

            if (lower.StartsWith("tun", StringComparison.Ordinal) ||
                lower.StartsWith("tap", StringComparison.Ordinal))
            {
                return "VPN";
            }

            return "";
        }

        private string NormalizeMobileNetworkType(string mobileType)
        {
            if (String.IsNullOrWhiteSpace(mobileType))
            {
                return "";
            }

            string[] parts = mobileType.Replace("/", ",").Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (String.IsNullOrWhiteSpace(part) || String.Equals(part, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string upper = part.ToUpperInvariant();
                if (upper == "NR" || upper.IndexOf("5G", StringComparison.Ordinal) >= 0)
                {
                    return "5G";
                }
                if (upper.IndexOf("LTE", StringComparison.Ordinal) >= 0 || upper.IndexOf("IWLAN", StringComparison.Ordinal) >= 0)
                {
                    return "4G";
                }
                if (upper.IndexOf("HSPA", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("UMTS", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("WCDMA", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("EVDO", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("TD-SCDMA", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("CDMA2000", StringComparison.Ordinal) >= 0)
                {
                    return "3G";
                }
                if (upper.IndexOf("EDGE", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("GPRS", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("CDMA", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("1XRTT", StringComparison.Ordinal) >= 0 ||
                    upper.IndexOf("GSM", StringComparison.Ordinal) >= 0)
                {
                    return "2G";
                }

                return part;
            }

            return "";
        }

        private string ReadDeviceShellValue(string serial, string command)
        {
            ProcessResult result = RunAdbShellCaptureForDeviceProbe(serial, command);
            return NormalizeDeviceValue(result.Output);
        }

        private Dictionary<string, string> ReadDeviceGetPropMap(string serial)
        {
            ProcessResult result = RunAdbShellCaptureForDeviceProbe(serial, "getprop");
            return ParseDeviceGetPropMap(result == null ? "" : result.Output);
        }

        private Dictionary<string, string> ParseDeviceGetPropMap(string output)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrWhiteSpace(output))
            {
                return result;
            }

            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("[", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf("]: [", StringComparison.Ordinal);
                if (separatorIndex <= 1 || !line.EndsWith("]", StringComparison.Ordinal))
                {
                    continue;
                }

                string key = line.Substring(1, separatorIndex - 1).Trim();
                int valueStart = separatorIndex + 4;
                string value = valueStart >= line.Length - 1 ? "" : line.Substring(valueStart, line.Length - valueStart - 1);
                if (!String.IsNullOrWhiteSpace(key))
                {
                    result[key] = NormalizeDeviceValue(value);
                }
            }
            return result;
        }

        private string GetDevicePropValue(Dictionary<string, string> props, string key)
        {
            if (props == null || String.IsNullOrWhiteSpace(key))
            {
                return "";
            }

            string value;
            if (!props.TryGetValue(key.Trim(), out value))
            {
                return "";
            }

            return NormalizeDeviceValue(value);
        }

        private string ReadDeviceShellRawValue(string serial, string command)
        {
            ProcessResult result = RunAdbShellCaptureForDeviceProbe(serial, command);
            return result == null ? "" : (result.Output ?? "");
        }

        private ProcessResult RunAdbShellCaptureForDeviceProbe(string serial, string command)
        {
            string adb = GetAdbPath();
            string args = "-s " + QuoteArg(serial) + " shell " + command;
            ProcessResult result = RunProcess(adb, args, null, false);
            if (!ShouldReconnectForDeviceProbeResult(result))
            {
                return result;
            }
            if (IsDeviceReconnectSuppressed(serial))
            {
                return result;
            }

            string connectArguments;
            ProcessResult connectResult;
            ProcessResult stateResult;
            bool reconnectSucceeded = TryReconnectDeviceCore(adb, serial, out connectArguments, out connectResult, out stateResult);
            if (reconnectSucceeded)
            {
                result = RunProcess(adb, args, null, false);
            }
            return result;
        }

        private bool ShouldReconnectForDeviceProbeResult(ProcessResult result)
        {
            if (result == null)
            {
                return true;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant();
            if (text.IndexOf("device offline", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("device unauthorized", StringComparison.Ordinal) >= 0 ||
                (text.IndexOf("device '", StringComparison.Ordinal) >= 0 && text.IndexOf("not found", StringComparison.Ordinal) >= 0) ||
                text.IndexOf("no devices/emulators found", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("more than one device/emulator", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("closed", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
        }

        private string NormalizeDeviceValue(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string[] lines = value.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (String.Equals(line, "null", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(line, "unknown", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(line, "unavailable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return line;
            }

            return "";
        }

        private string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return "";
            }

            for (int i = 0; i < values.Length; i++)
            {
                string value = NormalizeDeviceValue(values[i]);
                if (!String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return "";
        }

        private string ExtractFirstIpv4(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string[] tokens = value.Replace("\r\n", "\n").Replace('\n', ' ').Split(new[] { ' ', '\t', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.StartsWith("addr:", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring("addr:".Length);
                }

                if (LooksLikeIpv4(token))
                {
                    return token;
                }
            }

            return "";
        }

        private bool LooksLikeIpv4(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                int number;
                if (!Int32.TryParse(parts[i], out number) || number < 0 || number > 255)
                {
                    return false;
                }
            }

            return true;
        }

        private string ExtractIpv4FromAdbSerial(string serial)
        {
            if (String.IsNullOrWhiteSpace(serial))
            {
                return "";
            }

            string value = serial.Trim();
            int colonIndex = value.LastIndexOf(':');
            if (colonIndex > 0)
            {
                value = value.Substring(0, colonIndex);
            }

            return LooksLikeIpv4(value) ? value : "";
        }

        private bool IsSameLanAdbConnection(string serial)
        {
            string deviceIpv4 = ExtractIpv4FromAdbSerial(serial);
            if (String.IsNullOrWhiteSpace(deviceIpv4))
            {
                return false;
            }

            IPAddress deviceAddress;
            if (!IPAddress.TryParse(deviceIpv4, out deviceAddress) || deviceAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            uint deviceValue = Ipv4ToUInt32(deviceAddress);
            NetworkInterface[] adapters;
            try
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                return false;
            }

            for (int i = 0; i < adapters.Length; i++)
            {
                NetworkInterface adapter = adapters[i];
                if (adapter == null || adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties properties;
                try
                {
                    properties = adapter.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                for (int j = 0; j < properties.UnicastAddresses.Count; j++)
                {
                    UnicastIPAddressInformation addressInfo = properties.UnicastAddresses[j];
                    if (addressInfo == null || addressInfo.Address == null || addressInfo.Address.AddressFamily != AddressFamily.InterNetwork || addressInfo.IPv4Mask == null)
                    {
                        continue;
                    }

                    uint maskValue = Ipv4ToUInt32(addressInfo.IPv4Mask);
                    if (maskValue == 0)
                    {
                        continue;
                    }

                    uint localValue = Ipv4ToUInt32(addressInfo.Address);
                    if ((deviceValue & maskValue) == (localValue & maskValue))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsWiredDeviceNetwork(DeviceInfo info)
        {
            if (info == null || String.IsNullOrWhiteSpace(info.NetworkType))
            {
                return false;
            }

            string networkType = info.NetworkType.Trim();
            return String.Equals(networkType, "有线", StringComparison.OrdinalIgnoreCase) ||
                networkType.IndexOf("ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                networkType.IndexOf("以太网", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsEmulatorAdbSerial(string serial)
        {
            return !String.IsNullOrWhiteSpace(serial) &&
                serial.Trim().StartsWith("emulator-", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsNetworkAdbSerial(string serial)
        {
            if (String.IsNullOrWhiteSpace(serial))
            {
                return false;
            }

            string value = serial.Trim();
            int colonIndex = value.LastIndexOf(':');
            if (colonIndex <= 0 || colonIndex >= value.Length - 1)
            {
                return false;
            }

            int port;
            return Int32.TryParse(value.Substring(colonIndex + 1), out port);
        }

        private bool IsUsbAdbConnection(DeviceInfo info, string serial)
        {
            if (String.IsNullOrWhiteSpace(serial) || IsEmulatorAdbSerial(serial) || IsNetworkAdbSerial(serial))
            {
                return false;
            }

            if (info != null && !String.IsNullOrWhiteSpace(info.Raw))
            {
                if (info.Raw.IndexOf("usb:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // 部分 adb 版本的 devices -l 不输出 usb: 字段；非网络、非模拟器序列号按 USB 直连处理。
            return true;
        }

        private bool ShouldUseScrcpyHighQuality(DeviceInfo info, string serial)
        {
            if (IsScrcpyHighQualityDisabled(serial))
            {
                return false;
            }

            if (IsScrcpyHighQualityForced(serial))
            {
                return true;
            }

            return IsScrcpyHighQualityAutoEnabled(info, serial);
        }

        private bool IsScrcpyHighQualityAutoEnabled(DeviceInfo info, string serial)
        {
            if (IsUsbAdbConnection(info, serial))
            {
                return true;
            }

            if (!IsSameLanAdbConnection(serial))
            {
                return false;
            }

            return IsWiredDeviceNetwork(info);
        }

        private string BuildScrcpyArguments(string serial, bool useHighQuality)
        {
            string arguments = "-s " + QuoteArg(serial);
            if (useHighQuality)
            {
                arguments += " " + ScrcpySameLanHighQualityArguments;
            }
            return arguments;
        }

        private string GetScrcpyHighQualityLaunchLabel(DeviceInfo info, string serial)
        {
            if (IsScrcpyHighQualityForced(serial))
            {
                return "（强制高画质）";
            }

            if (IsUsbAdbConnection(info, serial))
            {
                return "（USB高画质）";
            }

            return "（有线高画质）";
        }

        private void OpenScrcpy()
        {
            DeviceInfo selected = SelectedDeviceInfo();
            string serial = selected == null || String.IsNullOrWhiteSpace(selected.Serial) ? SelectedSerial() : selected.Serial;
            if (serial == null)
            {
                return;
            }

            string scrcpy = GetScrcpyPath();
            if (!ValidateExeOrCommand(scrcpy, "scrcpy.exe"))
            {
                return;
            }

            Rectangle toolBounds;
            Rectangle workArea;
            CaptureScrcpyPlacementContext(out toolBounds, out workArea);
            EnsureDeviceOnlineBeforeActionAsync(serial, "启动 scrcpy", delegate(bool online)
            {
                if (!online)
                {
                    return;
                }

                bool useHighQuality = ShouldUseScrcpyHighQuality(selected, serial);
                string arguments = BuildScrcpyArguments(serial, useHighQuality);
                ProcessStartInfo psi = new ProcessStartInfo(scrcpy, arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process process = Process.Start(psi);
                MoveScrcpyWindowWhenReady(process, toolBounds, workArea);
                AppendOutput("已启动 scrcpy: " + serial + (useHighQuality ? GetScrcpyHighQualityLaunchLabel(selected, serial) : ""));
            });
        }

        private void CaptureScrcpyPlacementContext(out Rectangle toolBounds, out Rectangle workArea)
        {
            toolBounds = Rectangle.Empty;
            workArea = Rectangle.Empty;
            try
            {
                Screen screen = Screen.FromControl(this);
                workArea = screen == null ? Screen.PrimaryScreen.WorkingArea : screen.WorkingArea;
                toolBounds = WindowState == FormWindowState.Minimized ? RestoreBounds : Bounds;
            }
            catch
            {
            }
        }

        private void MoveScrcpyWindowWhenReady(Process process, Rectangle toolBounds, Rectangle workArea)
        {
            if (process == null || toolBounds.Width <= 0 || toolBounds.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    IntPtr handle = IntPtr.Zero;
                    DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline)
                    {
                        if (process.HasExited)
                        {
                            return;
                        }

                        process.Refresh();
                        handle = process.MainWindowHandle;
                        if (handle != IntPtr.Zero)
                        {
                            break;
                        }
                        Thread.Sleep(100);
                    }

                    if (handle == IntPtr.Zero)
                    {
                        return;
                    }

                    NativeRect rect;
                    if (!GetWindowRect(handle, out rect))
                    {
                        return;
                    }

                    int windowWidth = Math.Max(1, rect.Right - rect.Left);
                    int windowHeight = Math.Max(1, rect.Bottom - rect.Top);
                    int targetHeight = workArea.Height;
                    int targetWidth = Math.Max(1, (int)Math.Round((double)windowWidth * targetHeight / windowHeight));
                    int leftSpace = Math.Max(0, toolBounds.Left - workArea.Left);
                    int rightSpace = Math.Max(0, workArea.Right - toolBounds.Right);
                    bool canPlaceRight = rightSpace >= targetWidth;
                    bool canPlaceLeft = leftSpace >= targetWidth;
                    bool placeRight;
                    if (canPlaceRight != canPlaceLeft)
                    {
                        placeRight = canPlaceRight;
                    }
                    else if (rightSpace != leftSpace)
                    {
                        placeRight = rightSpace > leftSpace;
                    }
                    else
                    {
                        placeRight = toolBounds.Left + toolBounds.Width / 2 <= workArea.Left + workArea.Width / 2;
                    }

                    int targetX = placeRight ? toolBounds.Right : toolBounds.Left - targetWidth;
                    int targetY = workArea.Top;
                    targetX = ClampInt(targetX, workArea.Left, workArea.Right - targetWidth);
                    SetWindowPos(handle, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight, SwpNoZOrder | SwpNoActivate);
                }
                catch
                {
                }
            });
        }

        private int ClampInt(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        private void OpenSettings()
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            RunAdbShell(serial, "am start -a android.settings.SETTINGS");
        }

        private void OpenEmbeddedCmd()
        {
            if (fixedCmdSession == null || fixedCmdSession.Process == null || fixedCmdSession.Process.HasExited)
            {
                StartFixedCmdSession();
                return;
            }

            if (cmdOutputBox != null)
            {
                cmdOutputBox.Focus();
            }
        }

        private void OpenEmbeddedShell()
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            EnsureDeviceOnlineBeforeActionAsync(serial, "新建设备 Shell", delegate(bool online)
            {
                if (online)
                {
                    StartShellTab(serial);
                }
            });
        }

        private TerminalSession StartShellTab(string serial)
        {
            TabPage page = new TabPage(serial);
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            RichTextBox terminalOutput = new RichTextBox();
            terminalOutput.Dock = DockStyle.Fill;
            terminalOutput.Font = new Font("Consolas", 9F);
            terminalOutput.BackColor = cmdBackgroundColor;
            terminalOutput.ForeColor = cmdOutputColor;
            terminalOutput.ReadOnly = true;
            terminalOutput.WordWrap = false;
            terminalOutput.BorderStyle = BorderStyle.None;
            terminalOutput.HideSelection = false;
            terminalOutput.ShortcutsEnabled = false;

            Control terminalHost = BuildTerminalHost(terminalOutput);
            layout.Controls.Add(terminalHost, 0, 0);
            page.Controls.Add(layout);
            terminalTabs.TabPages.Add(page);
            terminalTabs.SelectedTab = page;

            TerminalSession session = new TerminalSession();
            session.Page = page;
            session.Output = terminalOutput;
            session.Title = serial;
            session.Serial = serial;
            session.IsAdbShell = true;
            session.UsesFixedCore = true;
            session.AdbShellLocalMode = true;
            terminalOutput.ContextMenuStrip = BuildEmbeddedShellContextMenu(session);
            terminalSessions.Add(session);
            ConfigureEmbeddedShellEditor(terminalOutput, session);
            ConfigureEmbeddedShellFileDropTargets(session, page, layout, terminalHost);

            try
            {
                string comSpec = Environment.GetEnvironmentVariable("ComSpec");
                if (String.IsNullOrWhiteSpace(comSpec))
                {
                    comSpec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                }

                session.FixedSession = new PseudoConsoleSession();
                string shellCommand = QuoteCmdToken(GetAdbPath()) + " -s " + QuoteCmdToken(serial) + " shell";
                string args = "/Q /S /C " + QuoteCmdToken(shellCommand);
                LogEmbeddedShellDebug(session, "start-shell-tab", "serial=" + serial + " args=" + args);
                session.FixedSession.Start(comSpec, args, cmdWorkingDirectory, GetShellTabColumns(terminalOutput), GetShellTabRows(terminalOutput));
                session.Process = session.FixedSession.Process;
                if (session.Process != null)
                {
                    session.Process.EnableRaisingEvents = true;
                    session.Process.Exited += delegate
                    {
                        if (IsDisposed || terminalTabs == null || terminalTabs.IsDisposed)
                        {
                            return;
                        }

                        try
                        {
                            BeginInvoke((Action)delegate
                            {
                                if (terminalSessions.Contains(session))
                                {
                                    CloseTerminalSession(session);
                                }
                            });
                        }
                        catch
                        {
                        }
                    };
                }

                StartEmbeddedShellReader(session);
                StartAdbShellCommandCompletionWarmup(serial);
                terminalOutput.Focus();
                return session;
            }
            catch (Exception ex)
            {
                CloseTerminalSession(session);
                AppendOutput("启动设备 Shell 失败: " + ex.Message);
                return null;
            }
        }

        private void StartTerminal(string title, string fileName, string arguments, string workingDirectory)
        {
            TabPage page = new TabPage(title);
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            RichTextBox terminalOutput = new RichTextBox();
            terminalOutput.Dock = DockStyle.Fill;
            terminalOutput.Font = new Font("Consolas", 9F);
            terminalOutput.BackColor = Color.Black;
            terminalOutput.ForeColor = Color.Gainsboro;
            terminalOutput.ReadOnly = false;
            terminalOutput.WordWrap = false;

            layout.Controls.Add(BuildTerminalHost(terminalOutput), 0, 0);
            page.Controls.Add(layout);
            terminalTabs.TabPages.Add(page);
            terminalTabs.SelectedTab = page;

            TerminalSession session = new TerminalSession();
            session.Page = page;
            session.Output = terminalOutput;
            session.Title = title;
            ConfigureTerminalEditor(terminalOutput, delegate { return session; });
            EnsureTerminalTailPadding(session);

            ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            if (!String.IsNullOrWhiteSpace(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }

            Process process = new Process();
            process.StartInfo = psi;
            process.EnableRaisingEvents = true;
            process.Exited += delegate
            {
                AppendTerminal(session, Environment.NewLine + "[process exited]" + Environment.NewLine);
            };

            session.Process = process;
            terminalSessions.Add(session);

            try
            {
                process.Start();
                StartStreamReader(session, process.StandardOutput);
                StartStreamReader(session, process.StandardError);
                AppendTerminal(session, "> " + fileName + " " + arguments + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AppendTerminal(session, "启动终端失败: " + ex.Message + Environment.NewLine);
            }
        }

        private void StartStreamReader(TerminalSession session, StreamReader reader)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                char[] buffer = new char[512];
                try
                {
                    while (session.Process != null && !session.Process.HasExited)
                    {
                        int read = reader.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }
                        string text = new string(buffer, 0, read);
                        AppendTerminal(session, text);
                    }
                }
                catch
                {
                }
            });
        }

        private void SendTerminalInput(TerminalSession session)
        {
            string command = GetPendingTerminalInput(session);
            if (String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            if (session.IsAdbShell && String.Equals(command.Trim(), "exit", StringComparison.Ordinal))
            {
                CloseTerminalSession(session);
                return;
            }

            PushTerminalHistory(session, command);

            if (session.Process == null || session.Process.HasExited)
            {
                AppendTerminal(session, "[terminal closed]" + Environment.NewLine);
                return;
            }

            try
            {
                if (session.Output != null)
                {
                    RemoveTerminalTailPadding(session);
                    session.Output.AppendText(Environment.NewLine);
                    session.InputStart = session.Output.TextLength;
                    EnsureTerminalTailPadding(session);
                    session.Output.SelectionStart = GetTerminalEditableEnd(session);
                    session.Output.SelectionLength = 0;
                    SafeScrollTerminalToCaret(session.Output);
                }
                session.Process.StandardInput.WriteLine(command);
            }
            catch (Exception ex)
            {
                AppendTerminal(session, "发送命令失败: " + ex.Message + Environment.NewLine);
            }
        }

        private void AppendTerminal(TerminalSession session, string text)
        {
            if (session.Output.InvokeRequired)
            {
                session.Output.BeginInvoke(new Action<TerminalSession, string>(AppendTerminal), session, text);
                return;
            }

            string pendingInput = GetPendingTerminalInput(session);
            if (pendingInput.Length > 0)
            {
                session.Output.Select(session.InputStart, GetTerminalEditableEnd(session) - session.InputStart);
                session.Output.SelectedText = "";
            }

            RemoveTerminalTailPadding(session);
            session.Output.AppendText(text);
            session.InputStart = session.Output.TextLength;

            if (pendingInput.Length > 0)
            {
                session.Output.AppendText(pendingInput);
                session.InputStart = session.Output.TextLength - pendingInput.Length;
            }

            EnsureTerminalTailPadding(session);
            session.Output.SelectionStart = GetTerminalEditableEnd(session);
            session.Output.SelectionLength = 0;
            SafeScrollTerminalToCaret(session.Output);
        }

        private void CloseCurrentTerminal()
        {
            if (terminalTabs.SelectedTab == null)
            {
                return;
            }

            TerminalSession found = null;
            for (int i = 0; i < terminalSessions.Count; i++)
            {
                if (terminalSessions[i].Page == terminalTabs.SelectedTab)
                {
                    found = terminalSessions[i];
                    break;
                }
            }

            if (found != null)
            {
                CloseTerminalSession(found);
                return;
            }
            terminalTabs.TabPages.Remove(terminalTabs.SelectedTab);
        }

        private void CloseTerminalSession(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            StopTerminalProcess(session);
            terminalSessions.Remove(session);
            if (terminalTabs != null && session.Page != null && terminalTabs.TabPages.Contains(session.Page))
            {
                terminalTabs.TabPages.Remove(session.Page);
            }
        }

        private void StartFixedCmdSession()
        {
            if (fixedCmdSession != null)
            {
                fixedCmdSession.Dispose();
                fixedCmdSession = null;
            }

            fixedCmdSession = new PseudoConsoleSession();

            if (cmdOutputBox != null)
            {
                cmdOutputBox.Clear();
            }
            ResetCmdOutputFilter();
            fixedCmdForcePassthrough = false;
            fixedCmdAdbShellLocalMode = false;
            fixedCmdAdbShellInputStartIndex = -1;
            fixedCmdAdbShellSerial = null;
            ResetAdbShellHistoryNavigation();

            try
            {
                string comSpec = Environment.GetEnvironmentVariable("ComSpec");
                if (String.IsNullOrWhiteSpace(comSpec))
                {
                    comSpec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                }
                fixedCmdSession.Start(comSpec, "/Q /K", cmdWorkingDirectory, GetCmdColumns(), GetCmdRows());
                StartFixedCmdReader();
                if (cmdOutputBox != null)
                {
                    cmdOutputBox.Focus();
                }
            }
            catch (Exception ex)
            {
                AppendCmd("启动 PC CMD 失败: " + ex.Message + Environment.NewLine);
            }
        }

        private void SendFixedCmdInput()
        {
            if (fixedCmdSession == null || fixedCmdSession.Process == null || fixedCmdSession.Process.HasExited)
            {
                StartFixedCmdSession();
                return;
            }
        }

        private void StartFixedCmdReader()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                char[] buffer = new char[512];
                try
                {
                    while (fixedCmdSession != null)
                    {
                        StreamReader reader = fixedCmdSession.OutputReader;
                        if (reader == null)
                        {
                            break;
                        }

                        int read = reader.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }

                        string text = new string(buffer, 0, read);
                        AppendCmd(text);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException)
                {
                }
                catch (NullReferenceException)
                {
                    // StreamReader can transiently throw during concurrent disposal when the shell closes.
                }

                if (fixedCmdSession != null)
                {
                    AppendCmd(Environment.NewLine + "[cmd exited]" + Environment.NewLine);
                }
            });
        }

        private void UpdateFixedCmdTerminalView()
        {
            return;
        }

        private int GetCmdColumns()
        {
            return FixedCmdColumns;
        }

        private int GetCmdRows()
        {
            if (cmdOutputBox == null)
            {
                return 30;
            }

            int lineHeight = Math.Max(cmdOutputBox.Font.Height + 2, 12);
            return Math.Max(10, cmdOutputBox.ClientSize.Height / lineHeight);
        }

        private int MeasureTerminalCharWidth(Control control)
        {
            if (control == null)
            {
                return 8;
            }

            using (Graphics graphics = control.CreateGraphics())
            {
                SizeF size = graphics.MeasureString(
                    "MMMMMMMMMM",
                    control.Font,
                    Int32.MaxValue,
                    StringFormat.GenericTypographic);
                return Math.Max(1, (int)Math.Ceiling(size.Width / 10F));
            }
        }

        private int EstimateTerminalColumns(Control control, int fallbackColumns)
        {
            if (control == null || control.IsDisposed || control.ClientSize.Width <= 0)
            {
                return fallbackColumns;
            }

            int charWidth = MeasureTerminalCharWidth(control);
            if (charWidth <= 0)
            {
                return fallbackColumns;
            }

            int usableWidth = Math.Max(1, control.ClientSize.Width - 4);
            return Math.Max(40, usableWidth / charWidth);
        }

        private void ResizeFixedCmdPseudoConsole()
        {
        }

        private int GetShellTabColumns(RichTextBox output)
        {
            return EstimateTerminalColumns(output, FixedCmdColumns);
        }

        private int GetShellTabRows(RichTextBox output)
        {
            if (output == null)
            {
                return 30;
            }

            int lineHeight = Math.Max(output.Font.Height + 2, 12);
            return Math.Max(10, output.ClientSize.Height / lineHeight);
        }

        private void ResizeEmbeddedShellPseudoConsole(TerminalSession session)
        {
            if (session == null || session.FixedSession == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            try
            {
                session.FixedSession.Resize(GetShellTabColumns(session.Output), GetShellTabRows(session.Output));
            }
            catch
            {
            }
        }

        private void ConfigureEmbeddedShellEditor(RichTextBox output, TerminalSession session)
        {
            output.HandleCreated += delegate
            {
                ApplyTerminalViewportPadding(output);
            };
            output.SizeChanged += delegate
            {
                ApplyTerminalViewportPadding(output);
                ResizeEmbeddedShellPseudoConsole(session);
            };
            output.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && output.SelectionLength == 0)
                {
                    output.Focus();
                    output.BeginInvoke((Action)delegate
                    {
                        EnsureEmbeddedShellCaretAtEditableEnd(session);
                    });
                }
            };
            output.MouseWheel += delegate
            {
                session.WheelScrolledAwayFromBottom = !IsRichTextScrolledToBottom(output);
            };
            output.GotFocus += delegate
            {
                EnsureEmbeddedShellCaretAtEditableEnd(session);
            };
            output.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                OnEmbeddedShellKeyDown(session, e);
            };
            output.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                OnEmbeddedShellKeyPress(session, e);
            };
            output.AllowDrop = true;
            output.DragEnter += OnTerminalFileDragEnter;
            output.DragOver += OnTerminalFileDragEnter;
            output.DragDrop += delegate(object sender, DragEventArgs e)
            {
                OnEmbeddedShellFileDragDrop(session, e);
            };
        }

        private void ConfigureEmbeddedShellFileDropTargets(TerminalSession session, params Control[] controls)
        {
            if (controls == null)
            {
                return;
            }

            for (int i = 0; i < controls.Length; i++)
            {
                Control control = controls[i];
                if (control == null)
                {
                    continue;
                }

                control.AllowDrop = true;
                control.DragEnter += OnTerminalFileDragEnter;
                control.DragOver += OnTerminalFileDragEnter;
                control.DragDrop += delegate(object sender, DragEventArgs e)
                {
                    OnEmbeddedShellFileDragDrop(session, e);
                };
            }
        }

        private void OnTerminalFileDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = HasTerminalFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnEmbeddedShellFileDragDrop(TerminalSession session, DragEventArgs e)
        {
            string text;
            if (!TryFormatDroppedTerminalPaths(e, true, out text))
            {
                return;
            }

            InsertDroppedTextIntoEmbeddedShell(session, text);
        }

        private void OnCmdFileDragDrop(object sender, DragEventArgs e)
        {
            bool shellQuote = IsFixedCmdPassthroughMode() && IsFixedCmdAdbShellLocalEditMode();
            string text;
            if (!TryFormatDroppedTerminalPaths(e, shellQuote, out text))
            {
                return;
            }

            InsertDroppedTextIntoCmd(text);
        }

        private bool HasTerminalFileDrop(DragEventArgs e)
        {
            string[] paths;
            return TryGetDroppedTerminalPaths(e, out paths);
        }

        private bool TryFormatDroppedTerminalPaths(DragEventArgs e, bool shellQuote, out string text)
        {
            text = null;
            string[] paths;
            if (!TryGetDroppedTerminalPaths(e, out paths))
            {
                return false;
            }

            List<string> formatted = new List<string>();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (String.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    path = Path.GetFullPath(path);
                }
                catch
                {
                }

                formatted.Add(shellQuote ? ShellQuote(path) : QuoteDroppedCmdPath(path));
            }

            if (formatted.Count == 0)
            {
                return false;
            }

            text = String.Join(" ", formatted.ToArray());
            return true;
        }

        private bool TryGetDroppedTerminalPaths(DragEventArgs e, out string[] paths)
        {
            paths = null;
            if (e == null || e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            return paths != null && paths.Length > 0;
        }

        private string QuoteDroppedCmdPath(string path)
        {
            if (path == null)
            {
                return "\"\"";
            }

            return CmdDroppedPathNeedsQuotes(path) ? QuoteCmdToken(path) : path;
        }

        private bool CmdDroppedPathNeedsQuotes(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.IndexOfAny(new char[] { ' ', '\t', '&', '(', ')', '[', ']', '{', '}', '^', '=', ';', ',', '\'', '|', '<', '>' }) >= 0;
        }

        private void InsertDroppedTextIntoEmbeddedShell(TerminalSession session, string text)
        {
            if (session == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            if (session.Output != null && !session.Output.IsDisposed)
            {
                session.Output.Focus();
            }

            if (session.AdbShellLocalMode)
            {
                PasteIntoEmbeddedShellLine(session, text);
            }
            else
            {
                SendEmbeddedShellPasteText(session, text);
            }
        }

        private void InsertDroppedTextIntoCmd(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            if (cmdOutputBox != null && !cmdOutputBox.IsDisposed)
            {
                cmdOutputBox.Focus();
            }

            if (IsFixedCmdPassthroughMode())
            {
                if (IsFixedCmdAdbShellLocalEditMode())
                {
                    PasteIntoFixedCmdAdbShellLine(text);
                }
                else
                {
                    SendFixedCmdPasteText(text);
                }
                return;
            }

            ResetCmdHistoryNavigation();
            SyncCmdCurrentInputFromVisibleLine();
            TrackCmdInsertedText(text);
            RenderCmdCurrentInputLine();
        }

        private ContextMenuStrip BuildEmbeddedShellContextMenu(TerminalSession session)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Opening += delegate
            {
                if (menu.Items.Count > 0)
                {
                    menu.Items[0].Enabled = session != null && session.Output != null && session.Output.SelectionLength > 0;
                    menu.Items[1].Enabled = Clipboard.ContainsText();
                }
            };
            menu.Items.Add("复制", null, delegate { CopySelectedEmbeddedShellText(session); });
            menu.Items.Add("粘贴", null, delegate { PasteIntoEmbeddedShell(session); });
            return menu;
        }

        private void StartEmbeddedShellReader(TerminalSession session)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                char[] buffer = new char[512];
                try
                {
                    while (session != null && session.FixedSession != null)
                    {
                        StreamReader reader = session.FixedSession.OutputReader;
                        if (reader == null)
                        {
                            break;
                        }

                        int read = reader.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }

                        string chunk = new string(buffer, 0, read);
                        LogEmbeddedShellDebug(session, "reader-chunk", chunk);
                        AppendEmbeddedShell(session, chunk);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException)
                {
                }
                catch (NullReferenceException)
                {
                    // StreamReader can transiently throw during concurrent disposal when the shell closes.
                }
            });
        }

        private void AppendEmbeddedShell(TerminalSession session, string text)
        {
            if (session == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            lock (session.PendingOutputLock)
            {
                session.PendingOutput.Append(text);
                if (session.FlushScheduled)
                {
                    return;
                }
                session.FlushScheduled = true;
            }

            if (session.Output == null || session.Output.IsDisposed)
            {
                lock (session.PendingOutputLock)
                {
                    session.FlushScheduled = false;
                }
                return;
            }

            try
            {
                session.Output.BeginInvoke((Action)delegate
                {
                    FlushEmbeddedShellPendingOutput(session);
                });
            }
            catch (InvalidOperationException)
            {
                lock (session.PendingOutputLock)
                {
                    session.FlushScheduled = false;
                }
            }
        }

        private void FlushEmbeddedShellPendingOutput(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                if (session != null)
                {
                    lock (session.PendingOutputLock)
                    {
                        session.FlushScheduled = false;
                    }
                }
                return;
            }

            string text;
            lock (session.PendingOutputLock)
            {
                text = session.PendingOutput.ToString();
                session.PendingOutput.Length = 0;
                session.FlushScheduled = false;
            }

            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            if (ShouldSuppressEmbeddedShellViewportRedraw(text))
            {
                LogEmbeddedShellDebug(session, "suppress-viewport-redraw", text);
                return;
            }

            Point scrollPosition = GetRichTextScrollPosition(session.Output);
            bool keepAtBottom = !session.WheelScrolledAwayFromBottom;
            LogEmbeddedShellDebug(session, "flush-pending-raw", text);

            bool clearDisplay;
            text = SanitizeEmbeddedShellOutput(session, text, out clearDisplay);
            LogEmbeddedShellDebug(session, "flush-pending-sanitized", "clearDisplay=" + clearDisplay + " text=" + EscapeTextForDebugLog(text));
            if (clearDisplay)
            {
                session.Output.Clear();
                session.WheelScrolledAwayFromBottom = false;
                session.SuppressLeadingBlankAfterClear = true;
                scrollPosition = Point.Empty;
                keepAtBottom = true;
            }
            if (!String.IsNullOrEmpty(text))
            {
                AppendEmbeddedShellTextIncremental(session, text);
                PruneTrailingDuplicateAdbShellPrompt(session.Output);
                UpdateEmbeddedShellInputStart(session);
                ColorizeEmbeddedShellCurrentLine(session);
            }

            session.LastCursorIndex = session.Output.TextLength;
            RestoreEmbeddedShellCaret(session);
            session.LastCursorIndex = session.Output.SelectionStart;

            if (keepAtBottom)
            {
                ScrollRichTextToBottom(session.Output);
                session.WheelScrolledAwayFromBottom = false;
            }
            else
            {
                SetRichTextScrollPosition(session.Output, scrollPosition);
            }
        }

        private string SanitizeEmbeddedShellOutput(TerminalSession session, string text, out bool clearDisplay)
        {
            clearDisplay = false;
            if (session == null || String.IsNullOrEmpty(text))
            {
                return "";
            }

            StringBuilder output = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (session.OscPending)
                {
                    if (session.OscEscapePending)
                    {
                        session.OscEscapePending = false;
                        if (ch == '\\')
                        {
                            session.OscPending = false;
                            session.ControlBuffer.Length = 0;
                            continue;
                        }
                    }

                    if (ch == '\a')
                    {
                        session.OscPending = false;
                        session.ControlBuffer.Length = 0;
                        continue;
                    }

                    if (ch == 0x1b)
                    {
                        session.OscEscapePending = true;
                    }
                    else
                    {
                        session.ControlBuffer.Append(ch);
                    }
                    continue;
                }

                if (session.CsiPending)
                {
                    session.ControlBuffer.Append(ch);
                    if (ch >= '@' && ch <= '~')
                    {
                        string sequence = session.ControlBuffer.ToString();
                        if (ch == 'J')
                        {
                            if (sequence.IndexOf("2", StringComparison.Ordinal) >= 0 ||
                                sequence.IndexOf("3", StringComparison.Ordinal) >= 0)
                            {
                                clearDisplay = true;
                                output.Length = 0;
                            }
                        }
                        else
                        {
                            AppendEmbeddedShellCsiMarker(output, sequence);
                        }
                        session.CsiPending = false;
                        session.ControlBuffer.Length = 0;
                    }
                    continue;
                }

                if (session.EscapePending)
                {
                    session.EscapePending = false;
                    if (ch == '[')
                    {
                        session.CsiPending = true;
                        session.ControlBuffer.Length = 0;
                    }
                    else if (ch == ']')
                    {
                        session.OscPending = true;
                        session.OscEscapePending = false;
                        session.ControlBuffer.Length = 0;
                    }
                    else if (ch == 'c')
                    {
                        clearDisplay = true;
                        output.Length = 0;
                    }
                    continue;
                }

                if (ch == 0x1b)
                {
                    session.EscapePending = true;
                    continue;
                }

                if (ch == '\a' || ch == '\ufeff')
                {
                    continue;
                }
                if (ch == '\t')
                {
                    output.Append(ExpandEmbeddedShellTabForText(session, output));
                    continue;
                }

                if (!Char.IsControl(ch) || ch == '\r' || ch == '\n' || ch == '\b')
                {
                    output.Append(ch);
                }
            }

            string sanitized = output.ToString();
            string terminalBackspace = new string(new char[] { CmdMarkerLeft, ' ', CmdMarkerLeft });
            if (sanitized.IndexOf(terminalBackspace, StringComparison.Ordinal) >= 0)
            {
                sanitized = sanitized.Replace(terminalBackspace, "\b");
            }
            return sanitized;
        }

        private bool ShouldSuppressEmbeddedShellViewportRedraw(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.IndexOf("\u001b[H", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            int clearLineCount = CountSubstringOccurrences(text, "\u001b[K");
            if (clearLineCount < 3)
            {
                return false;
            }

            bool hasViewportResize = text.IndexOf("\u001b[8;", StringComparison.Ordinal) >= 0;
            bool hasCursorHide = text.IndexOf("\u001b[?25l", StringComparison.Ordinal) >= 0;
            return hasViewportResize || hasCursorHide;
        }

        private int CountSubstringOccurrences(string text, string value)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                index = text.IndexOf(value, index, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                count++;
                index += value.Length;
            }

            return count;
        }

        private string ExpandEmbeddedShellTabForText(TerminalSession session, StringBuilder output)
        {
            int column = 0;
            if (output != null && output.Length > 0)
            {
                int lineStart = -1;
                for (int i = output.Length - 1; i >= 0; i--)
                {
                    char ch = output[i];
                    if (ch == '\n' || ch == '\r')
                    {
                        lineStart = i;
                        break;
                    }
                }
                column = lineStart >= 0
                    ? output.Length - lineStart - 1
                    : GetEmbeddedShellCurrentOutputColumn(session) + output.Length;
            }
            else
            {
                column = GetEmbeddedShellCurrentOutputColumn(session);
            }

            int spaces = 8 - (column % 8);
            if (spaces <= 0)
            {
                spaces = 8;
            }
            return new string(' ', spaces);
        }

        private void AppendEmbeddedShellCsiMarker(StringBuilder output, string sequence)
        {
            if (output == null || String.IsNullOrEmpty(sequence))
            {
                return;
            }

            char command = sequence[sequence.Length - 1];
            string paramText = sequence.Substring(0, sequence.Length - 1);
            string firstParam = paramText;
            int semicolon = firstParam.IndexOf(';');
            if (semicolon >= 0)
            {
                firstParam = firstParam.Substring(0, semicolon);
            }

            int count;
            if (!Int32.TryParse(firstParam, out count) || count <= 0)
            {
                count = 1;
            }
            count = Math.Min(count, 200);

            char marker = '\0';
            if (command == 'D')
            {
                marker = CmdMarkerLeft;
            }
            else if (command == 'C')
            {
                marker = CmdMarkerRight;
            }
            else if (command == 'P')
            {
                marker = CmdMarkerDelete;
            }
            else if (command == 'K')
            {
                marker = CmdMarkerClearLineTail;
                count = 1;
            }

            if (marker == '\0')
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                output.Append(marker);
            }
        }

        private void AppendEmbeddedShellTextIncremental(TerminalSession session, string text)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed || String.IsNullOrEmpty(text))
            {
                return;
            }

            StringBuilder plain = new StringBuilder(text.Length);
            int plainColumn = GetEmbeddedShellCurrentOutputColumn(session);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (session.SuppressLeadingBlankAfterClear)
                {
                    if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                    {
                        continue;
                    }
                    session.SuppressLeadingBlankAfterClear = false;
                }

                if (ch == '\r')
                {
                    continue;
                }
                if (ch == '\n')
                {
                    if (plain.Length > 0)
                    {
                        AppendEmbeddedShellOutputText(session, plain.ToString());
                        plain.Length = 0;
                    }
                    AppendEmbeddedShellOutputText(session, Environment.NewLine);
                    plainColumn = 0;
                    continue;
                }
                if (ch == '\t')
                {
                    int spaces = 8 - (plainColumn % 8);
                    if (spaces <= 0)
                    {
                        spaces = 8;
                    }
                    plain.Append(new string(' ', spaces));
                    plainColumn += spaces;
                    continue;
                }
                if (ch == CmdMarkerLeft || ch == CmdMarkerRight ||
                    ch == CmdMarkerDelete || ch == CmdMarkerClearLineTail)
                {
                    if (plain.Length > 0)
                    {
                        AppendEmbeddedShellOutputText(session, plain.ToString());
                        plain.Length = 0;
                    }

                    if (ch == CmdMarkerLeft)
                    {
                        MoveEmbeddedShellCaretLeft(session);
                    }
                    else if (ch == CmdMarkerRight)
                    {
                        MoveEmbeddedShellCaretRight(session);
                    }
                    else if (ch == CmdMarkerDelete)
                    {
                        ApplyEmbeddedShellDelete(session);
                    }
                    else
                    {
                        ClearEmbeddedShellLineTail(session);
                    }
                    continue;
                }
                if (ch == '\b')
                {
                    if (plain.Length > 0)
                    {
                        plain.Length--;
                        if (plainColumn > 0)
                        {
                            plainColumn--;
                        }
                    }
                    else if (session.Output.TextLength > 0)
                    {
                        session.Output.Select(session.Output.TextLength - 1, 1);
                        ReplaceEmbeddedShellSelectedText(session, "");
                    }
                    continue;
                }

                plain.Append(ch);
                plainColumn++;
            }

            if (plain.Length > 0)
            {
                AppendEmbeddedShellOutputText(session, plain.ToString());
            }
        }

        private int GetEmbeddedShellCurrentOutputColumn(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed || session.Output.TextLength == 0)
            {
                return 0;
            }

            string text = session.Output.Text;
            int lineStart = text.LastIndexOf('\n');
            return lineStart >= 0 ? text.Length - lineStart - 1 : text.Length;
        }

        private void AppendEmbeddedShellOutputText(TerminalSession session, string text)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed || String.IsNullOrEmpty(text))
            {
                return;
            }

            session.Output.SelectionStart = session.Output.TextLength;
            session.Output.SelectionLength = 0;
            session.Output.SelectionColor = cmdOutputColor;
            session.Output.AppendText(text);
            session.Output.SelectionColor = cmdOutputColor;
        }

        private void RestoreEmbeddedShellCaret(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int index = Math.Min(Math.Max(0, session.LastCursorIndex), session.Output.TextLength);
            session.Output.SelectionStart = index;
            session.Output.SelectionLength = 0;
        }

        private void EnsureEmbeddedShellCaretAtEditableEnd(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            if (session.Output.SelectionLength == 0 && session.Output.SelectionStart != session.Output.TextLength)
            {
                session.Output.SelectionStart = session.Output.TextLength;
                session.Output.SelectionLength = 0;
            }
        }

        private void MoveEmbeddedShellCaretLeft(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            if (session.Output.SelectionStart > inputStart)
            {
                session.Output.SelectionStart--;
                session.Output.SelectionLength = 0;
            }
        }

        private void MoveEmbeddedShellCaretRight(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            if (session.Output.SelectionStart < session.Output.TextLength)
            {
                session.Output.SelectionStart++;
                session.Output.SelectionLength = 0;
                return;
            }

            AppendEmbeddedShellOutputText(session, " ");
        }

        private void ApplyEmbeddedShellDelete(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            int start = Math.Max(inputStart, session.Output.SelectionStart);
            if (session.Output.SelectionLength > 0)
            {
                session.Output.Select(start, Math.Min(session.Output.SelectionLength, session.Output.TextLength - start));
                ReplaceEmbeddedShellSelectedText(session, "");
                session.Output.SelectionStart = start;
                session.Output.SelectionLength = 0;
                return;
            }

            if (start < session.Output.TextLength)
            {
                session.Output.Select(start, 1);
                ReplaceEmbeddedShellSelectedText(session, "");
                session.Output.SelectionStart = start;
                session.Output.SelectionLength = 0;
            }
        }

        private void ClearEmbeddedShellLineTail(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int start = Math.Max(GetEmbeddedShellInputStartIndex(session), session.Output.SelectionStart);
            int lineEnd = session.Output.Text.IndexOf('\n', start);
            if (lineEnd < 0)
            {
                lineEnd = session.Output.TextLength;
            }
            if (lineEnd > start)
            {
                session.Output.Select(start, lineEnd - start);
                ReplaceEmbeddedShellSelectedText(session, "");
                session.Output.SelectionStart = start;
                session.Output.SelectionLength = 0;
            }
        }

        private void ReplaceEmbeddedShellSelectedText(TerminalSession session, string text)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            bool wasReadOnly = session.Output.ReadOnly;
            try
            {
                session.Output.ReadOnly = false;
                session.Output.SelectedText = text ?? "";
            }
            finally
            {
                if (wasReadOnly && session.Output != null && !session.Output.IsDisposed)
                {
                    session.Output.ReadOnly = true;
                }
            }
        }

        private bool TryGetEmbeddedShellInputStartIndex(TerminalSession session, out int inputStart)
        {
            inputStart = 0;
            if (session == null || session.Output == null || session.Output.IsDisposed || String.IsNullOrEmpty(session.Output.Text))
            {
                return false;
            }

            if (session.AdbShellLocalMode &&
                TryFindEmbeddedShellInputStartIndex(session.Output.Text, out inputStart))
            {
                session.AdbShellInputStartIndex = inputStart;
                return true;
            }

            if (session.AdbShellLocalMode &&
                session.AdbShellInputStartIndex >= 0 &&
                session.AdbShellInputStartIndex <= session.Output.TextLength)
            {
                inputStart = session.AdbShellInputStartIndex;
                return true;
            }

            return TryFindEmbeddedShellInputStartIndex(session.Output.Text, out inputStart);
        }

        private bool TryFindEmbeddedShellInputStartIndex(string text, out int inputStart)
        {
            inputStart = 0;
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            int promptInputStart = FindLastAdbShellPromptInputStart(text);
            if (promptInputStart < 0)
            {
                return false;
            }

            inputStart = Math.Min(promptInputStart, text.Length);
            return true;
        }

        private int GetEmbeddedShellInputStartIndex(TerminalSession session)
        {
            int inputStart;
            return TryGetEmbeddedShellInputStartIndex(session, out inputStart)
                ? inputStart
                : (session == null || session.Output == null ? 0 : session.Output.TextLength);
        }

        private void UpdateEmbeddedShellInputStart(TerminalSession session)
        {
            if (session == null || !session.AdbShellLocalMode || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart;
            if (TryFindEmbeddedShellInputStartIndex(session.Output.Text, out inputStart))
            {
                session.AdbShellInputStartIndex = inputStart;
            }
        }

        private bool IsEmbeddedShellLocalEditMode(TerminalSession session)
        {
            int inputStart;
            return TryGetEmbeddedShellInputStartIndex(session, out inputStart);
        }

        private void SyncEmbeddedShellCurrentInputFromVisibleLine(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            int lineEnd = session.Output.Text.IndexOf('\n', inputStart);
            if (lineEnd < 0)
            {
                lineEnd = session.Output.TextLength;
            }

            int selectionStart = session.Output.SelectionStart;
            int selectionEnd = selectionStart + session.Output.SelectionLength;
            bool selectionOutsideLatestInput =
                selectionStart < inputStart ||
                selectionStart > lineEnd ||
                selectionEnd > lineEnd;
            if (selectionOutsideLatestInput)
            {
                session.Output.SelectionStart = lineEnd;
                session.Output.SelectionLength = 0;
                session.LastCursorIndex = lineEnd;
            }

            int length = Math.Max(0, lineEnd - inputStart);
            string current = length == 0 ? "" : session.Output.Text.Substring(inputStart, length);
            session.CurrentInput.Length = 0;
            session.CurrentInput.Append(current);
            session.CurrentInputCursor = Math.Max(0, Math.Min(session.Output.SelectionStart - inputStart, session.CurrentInput.Length));
        }

        private void RenderEmbeddedShellCurrentInputLine(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            int lineEnd = session.Output.Text.IndexOf('\n', inputStart);
            if (lineEnd < 0)
            {
                lineEnd = session.Output.TextLength;
            }

            bool redrawSuspended = false;
            try
            {
                if (session.Output.IsHandleCreated)
                {
                    SendMessage(session.Output.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
                    redrawSuspended = true;
                }

                session.Output.Select(inputStart, Math.Max(0, lineEnd - inputStart));
                ReplaceEmbeddedShellSelectedText(session, session.CurrentInput.ToString());
                ApplyEmbeddedShellCurrentLineColors(session, inputStart);
                session.Output.SelectionStart = Math.Min(inputStart + session.CurrentInputCursor, session.Output.TextLength);
                session.Output.SelectionLength = 0;
            }
            finally
            {
                if (redrawSuspended && session.Output != null && !session.Output.IsDisposed && session.Output.IsHandleCreated)
                {
                    SendMessage(session.Output.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                    session.Output.Invalidate();
                }
            }
        }

        private void ColorizeEmbeddedShellCurrentLine(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            ApplyEmbeddedShellCurrentLineColors(session, GetEmbeddedShellInputStartIndex(session));
        }

        private void ApplyEmbeddedShellCurrentLineColors(TerminalSession session, int inputStart)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            inputStart = Math.Max(0, Math.Min(inputStart, session.Output.TextLength));
            int lineStart = session.Output.Text.LastIndexOf('\n', Math.Max(0, inputStart - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            if (inputStart > lineStart)
            {
                session.Output.Select(lineStart, inputStart - lineStart);
                session.Output.SelectionColor = cmdPromptColor;
            }

            int inputLength = Math.Min(session.CurrentInput.Length, Math.Max(0, session.Output.TextLength - inputStart));
            if (inputLength > 0)
            {
                session.Output.Select(inputStart, inputLength);
                session.Output.SelectionColor = cmdInputColor;
            }

            session.Output.SelectionLength = 0;
            session.Output.SelectionColor = cmdOutputColor;
        }

        private void RestoreEmbeddedShellInputCaretFromModel(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed)
            {
                return;
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            int caret = Math.Min(inputStart + Math.Max(0, session.CurrentInputCursor), session.Output.TextLength);
            try
            {
                session.Output.SelectionStart = caret;
                session.Output.SelectionLength = 0;
                session.LastCursorIndex = caret;
            }
            catch
            {
            }
        }

        private void TrackEmbeddedShellInsertedText(TerminalSession session, string text)
        {
            if (session == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\n')
                {
                    session.CurrentInput.Length = 0;
                    session.CurrentInputCursor = 0;
                    continue;
                }

                session.CurrentInput.Insert(session.CurrentInputCursor, ch);
                session.CurrentInputCursor++;
            }
        }

        private void TrackEmbeddedShellDelete(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            if (session.CurrentInputCursor < 0)
            {
                session.CurrentInputCursor = 0;
            }
            if (session.CurrentInputCursor >= session.CurrentInput.Length)
            {
                return;
            }

            session.CurrentInput.Remove(session.CurrentInputCursor, 1);
        }

        private void HandleEmbeddedShellBackspace(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            SyncEmbeddedShellCurrentInputFromVisibleLine(session);
            if (session.CurrentInputCursor > 0 && session.CurrentInput.Length > 0)
            {
                session.CurrentInput.Remove(session.CurrentInputCursor - 1, 1);
                session.CurrentInputCursor--;
            }
            RenderEmbeddedShellCurrentInputLine(session);
            if (session.Output != null && !session.Output.IsDisposed)
            {
                session.LastCursorIndex = session.Output.SelectionStart;
            }
        }

        private void ResetEmbeddedShellHistoryNavigation(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            session.AdbShellHistoryIndex = -1;
            session.AdbShellHistoryDraft = "";
        }

        private void AddEmbeddedShellHistoryEntry(TerminalSession session, string command)
        {
            if (session == null || String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            if (session.AdbShellHistory.Count > 0 &&
                String.Equals(session.AdbShellHistory[session.AdbShellHistory.Count - 1], command, StringComparison.Ordinal))
            {
                return;
            }

            session.AdbShellHistory.Add(command);
            if (session.AdbShellHistory.Count > 200)
            {
                session.AdbShellHistory.RemoveAt(0);
            }
        }

        private void NavigateEmbeddedShellHistory(TerminalSession session, int direction)
        {
            if (session == null || session.AdbShellHistory.Count == 0)
            {
                return;
            }

            if (session.AdbShellHistoryIndex < 0)
            {
                SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                session.AdbShellHistoryDraft = session.CurrentInput.ToString();
                session.AdbShellHistoryIndex = session.AdbShellHistory.Count;
            }

            int next = session.AdbShellHistoryIndex + direction;
            if (next < 0)
            {
                next = 0;
            }
            if (next > session.AdbShellHistory.Count)
            {
                next = session.AdbShellHistory.Count;
            }

            session.AdbShellHistoryIndex = next;
            session.CurrentInput.Length = 0;
            if (session.AdbShellHistoryIndex == session.AdbShellHistory.Count)
            {
                session.CurrentInput.Append(session.AdbShellHistoryDraft);
            }
            else
            {
                session.CurrentInput.Append(session.AdbShellHistory[session.AdbShellHistoryIndex]);
            }
            session.CurrentInputCursor = session.CurrentInput.Length;
            RenderEmbeddedShellCurrentInputLine(session);
        }

        private void SubmitEmbeddedShellLocalInput(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            SyncEmbeddedShellCurrentInputFromVisibleLine(session);
            string command = session.CurrentInput.ToString();
            AddEmbeddedShellHistoryEntry(session, command);
            ResetEmbeddedShellHistoryNavigation(session);
            session.CurrentInput.Length = 0;
            session.CurrentInputCursor = 0;
            RenderEmbeddedShellCurrentInputLine(session);
            ScrollRichTextToBottom(session.Output);
            session.WheelScrolledAwayFromBottom = false;
            LogEmbeddedShellDebug(session, "submit-local-input", command);
            SendEmbeddedShellText(session, command + "\r", "submit-local-input");
        }

        private void PasteIntoEmbeddedShellLine(TerminalSession session, string text)
        {
            if (session == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    ResetEmbeddedShellHistoryNavigation(session);
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    TrackEmbeddedShellInsertedText(session, lines[i]);
                    RenderEmbeddedShellCurrentInputLine(session);
                }

                if (i < lines.Length - 1)
                {
                    SubmitEmbeddedShellLocalInput(session);
                }
            }
        }

        private void SendEmbeddedShellPasteText(TerminalSession session, string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            normalized = normalized.Replace('\n', '\r');
            SendEmbeddedShellText(session, normalized, "paste-raw");
        }

        private void SendEmbeddedShellText(TerminalSession session, string text, string source = null)
        {
            if (session == null || session.FixedSession == null || session.FixedSession.InputWriter == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                LogEmbeddedShellDebug(session, String.IsNullOrWhiteSpace(source) ? "send-text" : source, text);
                session.FixedSession.InputWriter.Write(text);
                session.FixedSession.InputWriter.Flush();
            }
            catch
            {
            }
        }

        private void OnEmbeddedShellKeyDown(TerminalSession session, KeyEventArgs e)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            if (e.Control && e.KeyCode == Keys.C && session.Output.SelectionLength > 0)
            {
                e.SuppressKeyPress = true;
                CopySelectedEmbeddedShellText(session);
                return;
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x3");
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteIntoEmbeddedShell(session);
                return;
            }

            if (session.FixedSession == null || session.FixedSession.InputWriter == null)
            {
                return;
            }

            if (IsEmbeddedShellLocalEditMode(session))
            {
                if (e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    ResetEmbeddedShellHistoryNavigation(session);
                    ApplyEmbeddedShellTabCompletion(session);
                    return;
                }

                if (e.KeyCode == Keys.Left)
                {
                    e.SuppressKeyPress = true;
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    if (session.CurrentInputCursor > 0)
                    {
                        session.CurrentInputCursor--;
                    }
                    RestoreEmbeddedShellInputCaretFromModel(session);
                    return;
                }

                if (e.KeyCode == Keys.Right)
                {
                    e.SuppressKeyPress = true;
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    if (session.CurrentInputCursor < session.CurrentInput.Length)
                    {
                        session.CurrentInputCursor++;
                    }
                    RestoreEmbeddedShellInputCaretFromModel(session);
                    return;
                }

                if (e.KeyCode == Keys.Home)
                {
                    e.SuppressKeyPress = true;
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    session.CurrentInputCursor = 0;
                    RestoreEmbeddedShellInputCaretFromModel(session);
                    return;
                }

                if (e.KeyCode == Keys.End)
                {
                    e.SuppressKeyPress = true;
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    session.CurrentInputCursor = session.CurrentInput.Length;
                    RestoreEmbeddedShellInputCaretFromModel(session);
                    return;
                }

                if (e.KeyCode == Keys.Back)
                {
                    e.SuppressKeyPress = true;
                    ResetEmbeddedShellHistoryNavigation(session);
                    HandleEmbeddedShellBackspace(session);
                    return;
                }

                if (e.KeyCode == Keys.Delete)
                {
                    e.SuppressKeyPress = true;
                    ResetEmbeddedShellHistoryNavigation(session);
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    TrackEmbeddedShellDelete(session);
                    RenderEmbeddedShellCurrentInputLine(session);
                    return;
                }

                if (e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    NavigateEmbeddedShellHistory(session, -1);
                    return;
                }

                if (e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    NavigateEmbeddedShellHistory(session, 1);
                    return;
                }

                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SubmitEmbeddedShellLocalInput(session);
                    return;
                }
            }

            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\t");
                return;
            }
            if (e.KeyCode == Keys.Left)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[D");
                return;
            }
            if (e.KeyCode == Keys.Right)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[C");
                return;
            }
            if (e.KeyCode == Keys.Home)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[H");
                return;
            }
            if (e.KeyCode == Keys.End)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[F");
                return;
            }
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[A");
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[B");
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\r");
                return;
            }
            if (e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x7f");
                return;
            }
            if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                SendEmbeddedShellText(session, "\x1b[3~");
                return;
            }
        }

        private void OnEmbeddedShellKeyPress(TerminalSession session, KeyPressEventArgs e)
        {
            if (session == null || session.FixedSession == null || session.FixedSession.InputWriter == null)
            {
                return;
            }

            if (IsEmbeddedShellLocalEditMode(session))
            {
                e.Handled = true;
                if (!Char.IsControl(e.KeyChar))
                {
                    ResetEmbeddedShellHistoryNavigation(session);
                    SyncEmbeddedShellCurrentInputFromVisibleLine(session);
                    TrackEmbeddedShellInsertedText(session, e.KeyChar.ToString());
                    RenderEmbeddedShellCurrentInputLine(session);
                }
                return;
            }

            if (!Char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                SendEmbeddedShellText(session, e.KeyChar.ToString());
            }
        }

        private void CopySelectedEmbeddedShellText(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.SelectionLength <= 0)
            {
                return;
            }

            Clipboard.SetText(session.Output.SelectedText);
        }

        private void PasteIntoEmbeddedShell(TerminalSession session)
        {
            if (session == null || !Clipboard.ContainsText())
            {
                return;
            }

            string text = Clipboard.GetText();
            if (IsEmbeddedShellLocalEditMode(session))
            {
                PasteIntoEmbeddedShellLine(session, text);
            }
            else
            {
                SendEmbeddedShellPasteText(session, text);
            }
        }

        private void ApplyEmbeddedShellTabCompletion(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            SyncEmbeddedShellCurrentInputFromVisibleLine(session);
            string input = session.CurrentInput.ToString();
            int cursor = Math.Max(0, Math.Min(session.CurrentInputCursor, input.Length));
            string completed;
            int completedCursor;
            List<string> names;
            string displayTrimPrefix;
            if (!TryResolveAdbShellTabCompletion(
                session.Serial,
                GetCurrentAdbShellPromptDirectory(session),
                input,
                cursor,
                out completed,
                out completedCursor,
                out names,
                out displayTrimPrefix))
            {
                return;
            }

            if (completed != null)
            {
                session.CurrentInput.Length = 0;
                session.CurrentInput.Append(completed);
                session.CurrentInputCursor = completedCursor;
                RenderEmbeddedShellCurrentInputLine(session);
                return;
            }

            if (names != null && names.Count > 1)
            {
                AppendEmbeddedShellCompletionCandidates(session, names, input, cursor, displayTrimPrefix);
            }
        }

        private List<string> GetAdbShellCommandCompletionCandidatesForSerial(string serial, string rawToken)
        {
            List<string> names = new List<string>();
            if (String.IsNullOrEmpty(rawToken))
            {
                return names;
            }

            serial = NormalizeAdbShellCompletionSerial(serial);
            List<string> commands;
            if (!TryGetAdbShellCompletionCache(adbShellCommandCompletionCache, serial, AdbShellCommandCompletionCacheLifetime, out commands))
            {
                StartAdbShellCommandCompletionWarmup(serial);
                return names;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].StartsWith(rawToken, StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(commands[i] + " ");
                }
            }

            return names;
        }

        private List<string> GetAdbShellFileCompletionCandidatesForSerial(string serial, string currentDir, string rawToken)
        {
            List<string> names = new List<string>();
            if (String.IsNullOrWhiteSpace(currentDir))
            {
                return names;
            }

            string normalized = rawToken.Replace('\\', '/');
            int separatorIndex = normalized.LastIndexOf('/');
            string typedDirPart = separatorIndex >= 0 ? normalized.Substring(0, separatorIndex + 1) : "";
            string typedNamePart = separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;
            string searchDirectory = ResolveAdbShellCompletionDirectory(currentDir, typedDirPart);
            if (String.IsNullOrWhiteSpace(searchDirectory))
            {
                return names;
            }

            serial = NormalizeAdbShellCompletionSerial(serial);
            string cacheKey = BuildAdbShellDirectoryCompletionCacheKey(serial, searchDirectory);
            List<string> directoryItems;
            if (!TryGetAdbShellCompletionCache(adbShellDirectoryCompletionCache, cacheKey, AdbShellDirectoryCompletionCacheLifetime, out directoryItems))
            {
                StartAdbShellDirectoryCompletionWarmup(serial, searchDirectory, cacheKey);
                return names;
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < directoryItems.Count; i++)
            {
                string item = directoryItems[i];
                if (item.Length == 0)
                {
                    continue;
                }

                string compareName = item.EndsWith("/", StringComparison.Ordinal) ? item.Substring(0, item.Length - 1) : item;
                if (!compareName.StartsWith(typedNamePart, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = typedDirPart + item;
                if (unique.Add(candidate))
                {
                    names.Add(candidate);
                }
            }

            return names;
        }

        private string GetCurrentAdbShellPromptDirectory(TerminalSession session)
        {
            if (session == null || session.Output == null || String.IsNullOrWhiteSpace(session.Output.Text))
            {
                return "/";
            }

            string text = session.Output.Text;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                int lineStart = text.LastIndexOf('\n', i);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                string prefix = text.Substring(lineStart, i - lineStart).Trim();
                string promptHead = GetLastPromptHeadToken(prefix);
                string path = ExtractAdbShellPromptPath(promptHead);
                if (!String.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return "/";
        }

        private string GetCurrentAdbShellPromptPrefix(TerminalSession session)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed || String.IsNullOrEmpty(session.Output.Text))
            {
                return "";
            }

            int inputStart = GetEmbeddedShellInputStartIndex(session);
            inputStart = Math.Max(0, Math.Min(inputStart, session.Output.TextLength));
            int lineStart = session.Output.Text.LastIndexOf('\n', Math.Max(0, inputStart - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            if (inputStart < lineStart)
            {
                return "";
            }
            return session.Output.Text.Substring(lineStart, inputStart - lineStart);
        }

        private void AppendEmbeddedShellCompletionCandidates(TerminalSession session, List<string> names, string input, int cursor, string displayTrimPrefix)
        {
            if (session == null || session.Output == null || session.Output.IsDisposed || names == null || names.Count == 0)
            {
                return;
            }

            string prompt = GetCurrentAdbShellPromptPrefix(session);
            if (String.IsNullOrEmpty(prompt))
            {
                return;
            }

            string formatted = FormatAdbShellCompletionColumnsForOutput(session.Output, names, displayTrimPrefix);
            if (String.IsNullOrWhiteSpace(formatted))
            {
                return;
            }

            session.Output.SelectionStart = session.Output.TextLength;
            session.Output.SelectionLength = 0;
            session.Output.SelectionColor = cmdOutputColor;
            session.Output.AppendText(Environment.NewLine + formatted + Environment.NewLine);

            session.Output.SelectionColor = cmdPromptColor;
            session.Output.AppendText(prompt);

            int inputStart = session.Output.TextLength;
            session.Output.SelectionColor = cmdInputColor;
            session.Output.AppendText(input);
            session.Output.SelectionColor = cmdOutputColor;

            session.AdbShellInputStartIndex = inputStart;
            session.CurrentInput.Length = 0;
            session.CurrentInput.Append(input);
            session.CurrentInputCursor = Math.Max(0, Math.Min(cursor, input.Length));
            session.Output.SelectionStart = inputStart + session.CurrentInputCursor;
            session.Output.SelectionLength = 0;
            session.LastCursorIndex = session.Output.SelectionStart;
            ScrollRichTextToBottom(session.Output);
            session.WheelScrolledAwayFromBottom = false;
        }

        private string FormatAdbShellCompletionColumnsForOutput(RichTextBox output, List<string> names, string displayTrimPrefix)
        {
            List<string> displayNames = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string item = FormatAdbShellCompletionDisplayName(names[i], displayTrimPrefix);
                if (!String.IsNullOrWhiteSpace(item))
                {
                    displayNames.Add(item);
                }
            }
            if (displayNames.Count == 0)
            {
                return "";
            }

            displayNames.Sort(StringComparer.OrdinalIgnoreCase);
            int maxLength = 0;
            for (int i = 0; i < displayNames.Count; i++)
            {
                maxLength = Math.Max(maxLength, displayNames[i].Length);
            }

            int consoleColumns = EstimateTextColumns(output);
            int columnWidth = Math.Max(maxLength + 2, 8);
            int columnCount = Math.Max(1, consoleColumns / columnWidth);
            int rowCount = (displayNames.Count + columnCount - 1) / columnCount;

            StringBuilder builder = new StringBuilder();
            for (int row = 0; row < rowCount; row++)
            {
                if (row > 0)
                {
                    builder.Append(Environment.NewLine);
                }

                for (int column = 0; column < columnCount; column++)
                {
                    int index = column * rowCount + row;
                    if (index >= displayNames.Count)
                    {
                        continue;
                    }

                    string item = displayNames[index];
                    builder.Append(item);
                    if (column < columnCount - 1 && index + rowCount < displayNames.Count)
                    {
                        builder.Append(new string(' ', Math.Max(2, columnWidth - item.Length)));
                    }
                }
            }

            return builder.ToString();
        }

        private int EstimateTextColumns(RichTextBox output)
        {
            return EstimateTerminalColumns(output, 90);
        }

        private void ScrollRichTextToBottom(RichTextBox box)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
            {
                return;
            }

            SendMessage(box.Handle, WmVScroll, new IntPtr(SbBottom), IntPtr.Zero);
        }

        private bool IsRichTextScrolledToBottom(RichTextBox box)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
            {
                return true;
            }

            if (box.TextLength == 0)
            {
                return true;
            }

            try
            {
                int charIndex = Math.Max(0, box.TextLength - 1);
                Point lastCharPosition = box.GetPositionFromCharIndex(charIndex);
                return lastCharPosition.Y <= box.ClientSize.Height - Math.Max(2, box.Font.Height / 2);
            }
            catch
            {
                return true;
            }
        }

        private void StopTerminalProcess(TerminalSession session)
        {
            if (session == null)
            {
                return;
            }

            if (session.FixedSession != null)
            {
                try
                {
                    session.FixedSession.Dispose();
                }
                catch
                {
                }
                session.FixedSession = null;
            }

            if (session.Process == null)
            {
                return;
            }

            try
            {
                if (!session.Process.HasExited)
                {
                    try
                    {
                        session.Process.StandardInput.WriteLine("exit");
                        if (session.Process.WaitForExit(500))
                        {
                            return;
                        }
                    }
                    catch
                    {
                    }

                    session.Process.Kill();
                    session.Process.WaitForExit(500);
                }
            }
            catch
            {
            }
        }

        private void ConfigureTerminalEditor(RichTextBox output, Func<TerminalSession> sessionProvider)
        {
            output.ShortcutsEnabled = true;
            output.HideSelection = false;
            output.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && output.SelectionLength == 0)
                {
                    MoveTerminalCaretToEnd(sessionProvider());
                }
            };
            output.HandleCreated += delegate
            {
                ApplyTerminalViewportPadding(output);
            };
            output.SizeChanged += delegate
            {
                ApplyTerminalViewportPadding(output);
            };
            output.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                HandleTerminalKeyDown(sessionProvider(), e);
            };
            output.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                HandleTerminalKeyPress(sessionProvider(), e);
            };
            output.GotFocus += delegate
            {
                MoveTerminalCaretToEnd(sessionProvider());
            };
        }

        private void ApplyTerminalViewportPadding(RichTextBox output)
        {
            if (output == null || output.IsDisposed || !output.IsHandleCreated)
            {
                return;
            }

            try
            {
                NativeRect rect = new NativeRect();
                SendMessage(output.Handle, EmGetRect, IntPtr.Zero, ref rect);

                int desiredBottom = Math.Max(rect.Top + 1, output.ClientSize.Height - TerminalViewportBottomInset);
                if (rect.Bottom == desiredBottom)
                {
                    return;
                }

                rect.Bottom = desiredBottom;
                SendMessage(output.Handle, EmSetRectNp, IntPtr.Zero, ref rect);
            }
            catch
            {
            }
        }

        private Control BuildTerminalHost(RichTextBox terminalOutput)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = Padding.Empty;
            host.Controls.Add(terminalOutput);
            return host;
        }

        private void HandleTerminalKeyDown(TerminalSession session, KeyEventArgs e)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            if (e.Control && e.KeyCode == Keys.C && session.Output.SelectionLength > 0)
            {
                return;
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                TrySendControlC(session);
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                InsertTerminalText(session, Clipboard.ContainsText() ? Clipboard.GetText() : "");
                return;
            }

            if (!session.IsAdbShell && e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                NavigateTerminalHistory(session, true);
                return;
            }

            if (!session.IsAdbShell && e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                NavigateTerminalHistory(session, false);
                return;
            }

            if (!session.IsAdbShell && e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendTerminalInput(session);
                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                if (session.Output.SelectionLength > 0)
                {
                    if (session.Output.SelectionStart < session.InputStart)
                    {
                        e.SuppressKeyPress = true;
                        MoveTerminalCaretToEnd(session);
                    }
                    return;
                }

                if (session.Output.SelectionStart <= session.InputStart)
                {
                    e.SuppressKeyPress = true;
                    MoveTerminalCaretToEnd(session);
                }
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                if (session.Output.SelectionLength > 0)
                {
                    if (session.Output.SelectionStart < session.InputStart)
                    {
                        e.SuppressKeyPress = true;
                        MoveTerminalCaretToEnd(session);
                    }
                    return;
                }

                if (session.Output.SelectionStart < session.InputStart)
                {
                    e.SuppressKeyPress = true;
                    MoveTerminalCaretToEnd(session);
                }
                return;
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Home)
            {
                if (session.Output.SelectionStart <= session.InputStart && session.Output.SelectionLength == 0)
                {
                    e.SuppressKeyPress = true;
                    MoveTerminalCaretToEnd(session);
                }
                return;
            }

            if (!e.Control && !e.Alt)
            {
                EnsureTerminalEditAtEnd(session);
            }
        }

        private void HandleTerminalKeyPress(TerminalSession session, KeyPressEventArgs e)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                e.Handled = true;
                return;
            }

            if (!Char.IsControl(e.KeyChar))
            {
                EnsureTerminalEditAtEnd(session);
            }
        }

        private void EnsureTerminalEditAtEnd(TerminalSession session)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            if (session.Output.SelectionLength == 0 && session.Output.SelectionStart != GetTerminalEditableEnd(session))
            {
                MoveTerminalCaretToEnd(session);
                return;
            }

            if (session.Output.SelectionStart < session.InputStart)
            {
                MoveTerminalCaretToEnd(session);
            }
        }

        private void MoveTerminalCaretToEnd(TerminalSession session)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            session.Output.SelectionStart = GetTerminalEditableEnd(session);
            session.Output.SelectionLength = 0;
            SafeScrollTerminalToCaret(session.Output);
        }

        private void SafeScrollTerminalToCaret(RichTextBox output)
        {
            if (output == null || output.IsDisposed || !output.IsHandleCreated)
            {
                return;
            }

            try
            {
                output.BeginInvoke((Action)delegate
                {
                    if (output.IsDisposed || !output.IsHandleCreated)
                    {
                        return;
                    }

                    try
                    {
                        output.ScrollToCaret();
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void InsertTerminalText(TerminalSession session, string text)
        {
            if (session == null || session.Output == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            EnsureTerminalEditAtEnd(session);
            session.Output.SelectedText = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            session.Output.SelectionStart = GetTerminalEditableEnd(session);
            session.Output.SelectionLength = 0;
            SafeScrollTerminalToCaret(session.Output);
        }

        private void ReplacePendingTerminalInput(TerminalSession session, string text)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            EnsureTerminalEditAtEnd(session);
            int editableEnd = GetTerminalEditableEnd(session);
            session.Output.Select(session.InputStart, Math.Max(0, editableEnd - session.InputStart));
            session.Output.SelectedText = text ?? "";
            session.Output.SelectionStart = GetTerminalEditableEnd(session);
            session.Output.SelectionLength = 0;
            SafeScrollTerminalToCaret(session.Output);
        }

        private string GetPendingTerminalInput(TerminalSession session)
        {
            if (session == null || session.Output == null)
            {
                return "";
            }

            if (session.InputStart < 0 || session.InputStart > session.Output.TextLength)
            {
                session.InputStart = GetTerminalEditableEnd(session);
            }

            int editableEnd = GetTerminalEditableEnd(session);
            return editableEnd > session.InputStart
                ? session.Output.Text.Substring(session.InputStart, editableEnd - session.InputStart)
                : "";
        }

        private int GetTerminalEditableEnd(TerminalSession session)
        {
            if (session == null || session.Output == null)
            {
                return 0;
            }

            return Math.Max(0, session.Output.TextLength - session.TailPaddingLength);
        }

        private void RemoveTerminalTailPadding(TerminalSession session)
        {
            if (session == null || session.Output == null || session.TailPaddingLength <= 0)
            {
                return;
            }

            int editableEnd = GetTerminalEditableEnd(session);
            if (editableEnd < session.Output.TextLength)
            {
                session.Output.Select(editableEnd, session.Output.TextLength - editableEnd);
                session.Output.SelectedText = "";
            }

            session.TailPaddingLength = 0;
        }

        private void EnsureTerminalTailPadding(TerminalSession session)
        {
            if (session == null || session.Output == null)
            {
                return;
            }

            RemoveTerminalTailPadding(session);
            int before = session.Output.TextLength;
            session.Output.AppendText(TerminalTailPaddingText);
            session.TailPaddingLength = Math.Max(0, session.Output.TextLength - before);
        }

        private void PushTerminalHistory(TerminalSession session, string command)
        {
            if (session == null || session.IsAdbShell || String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string trimmed = command.Trim();
            if (session.CommandHistory.Count == 0 || !String.Equals(session.CommandHistory[session.CommandHistory.Count - 1], trimmed, StringComparison.Ordinal))
            {
                session.CommandHistory.Add(trimmed);
            }
            session.HistoryDraft = "";
            session.HistoryIndex = session.CommandHistory.Count;
        }

        private void NavigateTerminalHistory(TerminalSession session, bool previous)
        {
            if (session == null || session.IsAdbShell || session.CommandHistory.Count == 0)
            {
                return;
            }

            if (session.HistoryIndex < 0 || session.HistoryIndex > session.CommandHistory.Count)
            {
                session.HistoryIndex = session.CommandHistory.Count;
            }

            if (previous)
            {
                if (session.HistoryIndex == session.CommandHistory.Count)
                {
                    session.HistoryDraft = GetPendingTerminalInput(session);
                }
                if (session.HistoryIndex > 0)
                {
                    session.HistoryIndex--;
                }
            }
            else
            {
                if (session.HistoryIndex < session.CommandHistory.Count)
                {
                    session.HistoryIndex++;
                }
            }

            string value = session.HistoryIndex >= session.CommandHistory.Count
                ? session.HistoryDraft
                : session.CommandHistory[session.HistoryIndex];
            ReplacePendingTerminalInput(session, value);
        }

        private int FindCompletionTokenStart(string text)
        {
            bool inQuotes = false;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    if (!inQuotes)
                    {
                        return i;
                    }
                    continue;
                }

                if (!inQuotes && Char.IsWhiteSpace(c))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private ContextMenuStrip BuildCmdContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Opening += OnCmdContextMenuOpening;
            menu.Items.Add("复制", null, delegate { CopySelectedCmdText(); });
            menu.Items.Add("粘贴", null, delegate { PasteIntoCmd(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("清空显示", null, delegate { ClearFixedCmdDisplay(); });
            return menu;
        }

        private void OnCmdContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (cmdContextMenu == null)
            {
                return;
            }

            if (cmdContextMenu.Items.Count > 0)
            {
                cmdContextMenu.Items[0].Enabled = cmdOutputBox != null && cmdOutputBox.SelectionLength > 0;
                cmdContextMenu.Items[1].Enabled = Clipboard.ContainsText();
            }
        }

        private void CopySelectedCmdText()
        {
            if (cmdOutputBox == null || cmdOutputBox.SelectionLength <= 0)
            {
                return;
            }

            Clipboard.SetText(cmdOutputBox.SelectedText);
        }

        private ContextMenuStrip BuildOutputContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Opening += OnOutputContextMenuOpening;
            menu.Items.Add("复制", null, delegate { CopySelectedOutputText(); });
            return menu;
        }

        private void OnOutputContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (outputContextMenu == null || outputContextMenu.Items.Count == 0)
            {
                return;
            }

            outputContextMenu.Items[0].Enabled = outputBox != null && outputBox.SelectionLength > 0;
        }

        private void OnOutputBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedOutputText();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void CopySelectedOutputText()
        {
            if (outputBox == null || outputBox.SelectionLength <= 0)
            {
                return;
            }

            Clipboard.SetText(outputBox.SelectedText);
        }

        private void PasteIntoCmd()
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            string text = Clipboard.GetText();
            if (IsFixedCmdPassthroughMode())
            {
                if (IsFixedCmdAdbShellLocalEditMode())
                {
                    PasteIntoFixedCmdAdbShellLine(text);
                }
                else
                {
                    SendFixedCmdPasteText(text);
                }
                return;
            }
            if (text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0)
            {
                SubmitPastedCmdLines(text);
                return;
            }

            ResetCmdHistoryNavigation();
            TrackCmdInsertedText(text);
            RenderCmdCurrentInputLine();
        }

        private void ClearFixedCmdDisplay()
        {
            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null)
            {
                return;
            }

            SendFixedCmdText("cls\r");
        }

        private void TrySendControlC(TerminalSession session)
        {
            if (session == null || session.Process == null || session.Process.HasExited)
            {
                return;
            }

            try
            {
                session.Process.StandardInput.Write("\x3");
                session.Process.StandardInput.Flush();
            }
            catch
            {
            }
        }

        private void ToggleQuickToolEditor()
        {
            quickToolEditorPanel.Visible = !quickToolEditorPanel.Visible;
            if (quickFilterPanel != null)
            {
                quickFilterPanel.Visible = !quickToolEditorPanel.Visible;
            }
        }

        private void ResetQuickToolEditorTypeState()
        {
            quickTypeSelectionTouched = false;
            SetQuickToolEditorTypeSelection("cmd");
            if (quickTypeComboBox != null)
            {
                quickTypeComboBox.Enabled = false;
            }
        }

        private void SetQuickToolEditorTypeSelection(string type)
        {
            if (quickTypeComboBox == null || quickTypeComboBox.Items.Count == 0)
            {
                return;
            }

            int index = quickTypeComboBox.Items.IndexOf(type);
            if (index < 0)
            {
                index = quickTypeComboBox.Items.IndexOf("cmd");
            }
            if (index < 0)
            {
                index = 0;
            }

            quickTypeSelectionUpdating = true;
            try
            {
                quickTypeComboBox.SelectedIndex = index;
            }
            finally
            {
                quickTypeSelectionUpdating = false;
            }
        }

        private void AutoDetectQuickToolTypeFromEditorCommand()
        {
            if (quickCommandTextBox == null || quickTypeComboBox == null)
            {
                return;
            }

            string command = quickCommandTextBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(command))
            {
                ResetQuickToolEditorTypeState();
                return;
            }

            if (!quickTypeSelectionTouched)
            {
                SetQuickToolEditorTypeSelection(InferQuickToolType(command));
            }
            quickTypeComboBox.Enabled = true;
        }

        private string InferQuickToolType(string command)
        {
            string target = ExtractQuickToolCommandTarget(command).Trim();
            if (String.IsNullOrWhiteSpace(target))
            {
                return "cmd";
            }

            string extension = Path.GetExtension(target).ToLowerInvariant();
            if (extension == ".py")
            {
                return "py";
            }
            if (extension == ".ps1")
            {
                return "ps1";
            }
            if (extension == ".cmd" || extension == ".bat" || extension == ".exe" || extension == ".com")
            {
                return "cmd";
            }
            return "shell";
        }

        private string ExtractQuickToolCommandTarget(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                return "";
            }

            command = command.Trim();
            if (command.StartsWith("\"", StringComparison.Ordinal))
            {
                int closing = command.IndexOf('"', 1);
                if (closing > 1)
                {
                    return command.Substring(1, closing - 1);
                }
                return command.Trim('"');
            }

            int whitespace = command.IndexOfAny(new[] { ' ', '\t' });
            return whitespace >= 0 ? command.Substring(0, whitespace) : command;
        }

        private string ResolveQuickToolType(QuickTool tool)
        {
            if (tool == null)
            {
                return "cmd";
            }

            string type = (tool.Type ?? "").Trim().ToLowerInvariant();
            if (type == "cmd" || type == "shell" || type == "py" || type == "ps1")
            {
                return type;
            }
            return InferQuickToolType(tool.Command);
        }

        private void AddQuickTool()
        {
            string name = quickNameTextBox.Text.Trim();
            string command = quickCommandTextBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(command))
            {
                MessageBox.Show(this, "请填写按钮名和命令/脚本路径。", "缺少内容", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string type = quickTypeComboBox == null || !quickTypeComboBox.Enabled
                ? InferQuickToolType(command)
                : Convert.ToString(quickTypeComboBox.SelectedItem);
            if (String.IsNullOrWhiteSpace(type))
            {
                type = InferQuickToolType(command);
            }

            QuickTool existing = null;
            for (int i = 0; i < config.QuickTools.Count; i++)
            {
                if (String.Equals(config.QuickTools[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = config.QuickTools[i];
                    break;
                }
            }

            if (existing == null)
            {
                config.QuickTools.Add(new QuickTool { Name = name, Type = type, Command = command });
            }
            else
            {
                existing.Type = type;
                existing.Command = command;
            }

            quickNameTextBox.Clear();
            quickCommandTextBox.Clear();
            ResetQuickToolEditorTypeState();
            SaveConfig();
            BindQuickTools();
        }

        private void BindQuickTools()
        {
            if (quickToolsPanel == null)
            {
                return;
            }
            quickToolsPanel.Controls.Clear();
            string filter = quickFilterComboBox == null ? "全部" : Convert.ToString(quickFilterComboBox.SelectedItem);
            HashSet<QuickTool> renderedSwitchTools = new HashSet<QuickTool>();
            for (int i = 0; i < config.QuickTools.Count; i++)
            {
                QuickTool tool = config.QuickTools[i];
                if (renderedSwitchTools.Contains(tool))
                {
                    continue;
                }

                QuickToolSwitchPair switchPair;
                if (TryGetQuickToolSwitchPair(tool, out switchPair))
                {
                    renderedSwitchTools.Add(switchPair.OnTool);
                    renderedSwitchTools.Add(switchPair.OffTool);
                    if (IsQuickToolTypeVisible(switchPair.Type, filter))
                    {
                        quickToolsPanel.Controls.Add(CreateQuickToolSwitchControl(switchPair));
                    }
                    continue;
                }

                string displayType = ResolveQuickToolType(tool);
                if (!IsQuickToolTypeVisible(displayType, filter))
                {
                    continue;
                }

                quickToolsPanel.Controls.Add(CreateQuickToolButton(tool, displayType));
            }
        }

        private bool IsQuickToolTypeVisible(string displayType, string filter)
        {
            return String.IsNullOrWhiteSpace(filter) || filter == "全部" || String.Equals(displayType, filter, StringComparison.OrdinalIgnoreCase);
        }

        private Button CreateQuickToolButton(QuickTool tool, string displayType)
        {
            Button run = new Button();
            run.Text = tool.Name + " [" + displayType + "]";
            run.Width = 150;
            run.Height = 30;
            run.Margin = new Padding(3, 4, 8, 4);
            run.Tag = tool;
            EnableQuickToolDropTarget(run);
            run.Click += delegate(object sender, EventArgs e)
            {
                if (ShouldSuppressQuickToolClick())
                {
                    return;
                }
                RunQuickTool((QuickTool)((Button)sender).Tag);
            };
            run.MouseDown += OnQuickToolMouseDown;
            run.MouseMove += OnQuickToolMouseMove;
            run.ContextMenuStrip = BuildQuickToolContextMenu(tool);
            return run;
        }

        private Control CreateQuickToolSwitchControl(QuickToolSwitchPair pair)
        {
            Panel panel = new Panel();
            panel.Height = 30;
            panel.Margin = new Padding(3, 4, 8, 4);
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.BackColor = SystemColors.Control;
            panel.Tag = pair.OnTool;
            EnableQuickToolDropTarget(panel);
            panel.MouseDown += OnQuickToolMouseDown;
            panel.MouseMove += OnQuickToolMouseMove;

            Font font = quickToolsPanel == null ? Font : quickToolsPanel.Font;
            string typeText = "[" + pair.Type + "]";
            int nameWidth = Math.Max(70, Math.Min(180, TextRenderer.MeasureText(pair.DisplayName, font).Width + 10));
            int typeWidth = TextRenderer.MeasureText(typeText, font).Width + 4;
            int buttonWidth = 34;
            int left = 7;
            panel.Width = left + nameWidth + buttonWidth * 2 + typeWidth + 22;

            Label nameLabel = new Label();
            nameLabel.Text = pair.DisplayName;
            nameLabel.AutoEllipsis = true;
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            nameLabel.Width = nameWidth;
            nameLabel.Height = 24;
            nameLabel.Location = new Point(left, 2);
            nameLabel.MouseDown += OnQuickToolMouseDown;
            nameLabel.MouseMove += OnQuickToolMouseMove;
            nameLabel.Tag = pair.OnTool;
            EnableQuickToolDropTarget(nameLabel);
            panel.Controls.Add(nameLabel);
            left += nameWidth;

            Button onButton = CreateQuickToolSwitchChoiceButton("开", pair.OnTool);
            onButton.Location = new Point(left, 2);
            EnableQuickToolDropTarget(onButton);
            panel.Controls.Add(onButton);
            left += buttonWidth;

            Button offButton = CreateQuickToolSwitchChoiceButton("关", pair.OffTool);
            offButton.Location = new Point(left, 2);
            EnableQuickToolDropTarget(offButton);
            panel.Controls.Add(offButton);
            left += buttonWidth + 4;

            Label typeLabel = new Label();
            typeLabel.Text = typeText;
            typeLabel.TextAlign = ContentAlignment.MiddleLeft;
            typeLabel.Width = typeWidth;
            typeLabel.Height = 24;
            typeLabel.Location = new Point(left, 2);
            typeLabel.MouseDown += OnQuickToolMouseDown;
            typeLabel.MouseMove += OnQuickToolMouseMove;
            typeLabel.Tag = pair.OnTool;
            EnableQuickToolDropTarget(typeLabel);
            panel.Controls.Add(typeLabel);

            ContextMenuStrip menu = BuildQuickToolSwitchContextMenu(pair);
            panel.ContextMenuStrip = menu;
            nameLabel.ContextMenuStrip = menu;
            typeLabel.ContextMenuStrip = menu;
            onButton.ContextMenuStrip = menu;
            offButton.ContextMenuStrip = menu;
            return panel;
        }

        private Button CreateQuickToolSwitchChoiceButton(string text, QuickTool tool)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 34;
            button.Height = 24;
            button.Margin = new Padding(0);
            button.Tag = tool;
            button.MouseDown += OnQuickToolMouseDown;
            button.MouseMove += OnQuickToolMouseMove;
            button.Click += delegate(object sender, EventArgs e)
            {
                if (ShouldSuppressQuickToolClick())
                {
                    return;
                }
                RunQuickTool((QuickTool)((Button)sender).Tag);
            };
            return button;
        }

        private void EnableQuickToolDropTarget(Control control)
        {
            if (control == null)
            {
                return;
            }
            control.AllowDrop = true;
            control.DragEnter += OnQuickToolDragEnter;
            control.DragOver += OnQuickToolDragEnter;
            control.DragDrop += OnQuickToolDragDrop;
        }

        private ContextMenuStrip BuildQuickToolContextMenu(QuickTool tool)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem copyItem = new ToolStripMenuItem("复制命令");
            copyItem.Tag = tool;
            copyItem.Click += delegate(object sender, EventArgs e)
            {
                CopyQuickToolCommand((QuickTool)((ToolStripMenuItem)sender).Tag, "快捷命令");
            };
            ToolStripMenuItem editItem = new ToolStripMenuItem("编辑");
            editItem.Tag = tool;
            editItem.Click += delegate(object sender, EventArgs e)
            {
                EditQuickTool((QuickTool)((ToolStripMenuItem)sender).Tag);
            };
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除");
            deleteItem.Tag = tool;
            deleteItem.Click += delegate(object sender, EventArgs e)
            {
                QuickTool t = (QuickTool)((ToolStripMenuItem)sender).Tag;
                DeleteQuickTool(t);
            };
            menu.Items.Add(copyItem);
            menu.Items.Add(editItem);
            menu.Items.Add(deleteItem);
            return menu;
        }

        private ContextMenuStrip BuildQuickToolSwitchContextMenu(QuickToolSwitchPair pair)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem copyOnItem = new ToolStripMenuItem("复制开命令");
            copyOnItem.Tag = pair.OnTool;
            copyOnItem.Click += delegate(object sender, EventArgs e)
            {
                CopyQuickToolCommand((QuickTool)((ToolStripMenuItem)sender).Tag, "开命令");
            };
            ToolStripMenuItem copyOffItem = new ToolStripMenuItem("复制关命令");
            copyOffItem.Tag = pair.OffTool;
            copyOffItem.Click += delegate(object sender, EventArgs e)
            {
                CopyQuickToolCommand((QuickTool)((ToolStripMenuItem)sender).Tag, "关命令");
            };
            ToolStripMenuItem editOnItem = new ToolStripMenuItem("编辑开命令");
            editOnItem.Tag = pair.OnTool;
            editOnItem.Click += delegate(object sender, EventArgs e)
            {
                EditQuickTool((QuickTool)((ToolStripMenuItem)sender).Tag);
            };
            ToolStripMenuItem editOffItem = new ToolStripMenuItem("编辑关命令");
            editOffItem.Tag = pair.OffTool;
            editOffItem.Click += delegate(object sender, EventArgs e)
            {
                EditQuickTool((QuickTool)((ToolStripMenuItem)sender).Tag);
            };
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除开关组");
            deleteItem.Click += delegate
            {
                DeleteQuickToolSwitchPair(pair);
            };
            menu.Items.Add(copyOnItem);
            menu.Items.Add(copyOffItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(editOnItem);
            menu.Items.Add(editOffItem);
            menu.Items.Add(deleteItem);
            return menu;
        }

        private void CopyQuickToolCommand(QuickTool tool, string label)
        {
            if (tool == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(tool.Command))
            {
                AppendOutput(label + "为空，无法复制: " + tool.Name);
                return;
            }

            Clipboard.SetText(tool.Command);
            AppendOutput("已复制" + label + ": " + tool.Name);
        }

        private void DeleteQuickTool(QuickTool tool)
        {
            if (tool == null)
            {
                return;
            }
            if (MessageBox.Show(this, "删除快捷工具「" + tool.Name + "」？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                config.QuickTools.Remove(tool);
                SaveConfig();
                BindQuickTools();
            }
        }

        private void DeleteQuickToolSwitchPair(QuickToolSwitchPair pair)
        {
            if (pair == null)
            {
                return;
            }
            if (MessageBox.Show(this, "删除快捷工具「" + pair.DisplayName + "」开关组？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                config.QuickTools.Remove(pair.OnTool);
                config.QuickTools.Remove(pair.OffTool);
                SaveConfig();
                BindQuickTools();
            }
        }

        private bool TryGetQuickToolSwitchPair(QuickTool tool, out QuickToolSwitchPair pair)
        {
            pair = null;
            string displayName;
            bool isOn;
            if (tool == null || !TryParseQuickToolSwitchName(tool.Name, out displayName, out isOn))
            {
                return false;
            }

            string type = ResolveQuickToolType(tool);
            for (int i = 0; i < config.QuickTools.Count; i++)
            {
                QuickTool candidate = config.QuickTools[i];
                if (candidate == null || candidate == tool)
                {
                    continue;
                }

                string candidateDisplayName;
                bool candidateIsOn;
                if (!TryParseQuickToolSwitchName(candidate.Name, out candidateDisplayName, out candidateIsOn))
                {
                    continue;
                }
                if (candidateIsOn == isOn || !String.Equals(displayName, candidateDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!String.Equals(type, ResolveQuickToolType(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pair = new QuickToolSwitchPair
                {
                    DisplayName = displayName,
                    Type = type,
                    OnTool = isOn ? tool : candidate,
                    OffTool = isOn ? candidate : tool
                };
                return true;
            }

            return false;
        }

        private bool TryParseQuickToolSwitchName(string name, out string displayName, out bool isOn)
        {
            displayName = "";
            isOn = false;
            if (String.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string trimmed = name.Trim();
            int stateIndex = trimmed.Length - 1;
            while (stateIndex > 0 && Char.IsDigit(trimmed[stateIndex]))
            {
                stateIndex--;
            }

            if (stateIndex <= 0)
            {
                return false;
            }

            char state = trimmed[stateIndex];
            if (state != '开' && state != '关')
            {
                return false;
            }

            string prefix = trimmed.Substring(0, stateIndex).Trim();
            string suffix = trimmed.Substring(stateIndex + 1).Trim();
            if (String.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            displayName = prefix + suffix;
            isOn = state == '开';
            return true;
        }

        private void EditQuickTool(QuickTool tool)
        {
            if (tool == null)
            {
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = "编辑快捷按钮";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 820;
                dialog.Height = 190;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                TableLayoutPanel layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 3;
                layout.RowCount = 4;
                layout.Padding = new Padding(12);
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

                TextBox name = new TextBox();
                name.Dock = DockStyle.Fill;
                name.Text = tool.Name;

                ComboBox type = new ComboBox();
                type.Dock = DockStyle.Fill;
                type.DropDownStyle = ComboBoxStyle.DropDownList;
                type.Items.AddRange(new object[] { "shell", "py", "ps1", "cmd" });
                int selected = type.Items.IndexOf(tool.Type);
                type.SelectedIndex = selected >= 0 ? selected : 0;

                TextBox command = new TextBox();
                command.Dock = DockStyle.Fill;
                command.Text = tool.Command;

                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = FlowDirection.RightToLeft;

                Button save = MakeButton("保存", 80, delegate
                {
                    string newName = name.Text.Trim();
                    string newType = Convert.ToString(type.SelectedItem);
                    string newCommand = command.Text.Trim();
                    if (String.IsNullOrWhiteSpace(newName) || String.IsNullOrWhiteSpace(newCommand))
                    {
                        MessageBox.Show(this, "请填写按钮名和命令/脚本路径。", "缺少内容", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (FindQuickToolByName(newName, tool) != null)
                    {
                        MessageBox.Show(this, "已存在同名快捷按钮。", "名称重复", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    tool.Name = newName;
                    tool.Type = newType;
                    tool.Command = newCommand;
                    SaveConfig();
                    BindQuickTools();
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                });
                Button cancel = MakeButton("取消", 80, delegate
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                });

                buttons.Controls.Add(save);
                buttons.Controls.Add(cancel);
                layout.Controls.Add(MakeLabel("按钮名：", 64), 0, 0);
                layout.Controls.Add(name, 1, 0);
                layout.Controls.Add(MakeLabel("类型：", 52), 0, 1);
                layout.Controls.Add(type, 1, 1);
                layout.Controls.Add(MakeLabel("命令：", 52), 0, 2);
                layout.Controls.Add(command, 1, 2);
                layout.Controls.Add(MakeQuickToolCommandBrowseButton(command), 2, 2);
                layout.Controls.Add(buttons, 1, 3);
                dialog.Controls.Add(layout);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                dialog.ShowDialog(this);
            }
        }

        private void RunQuickToolInFixedCmd(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            if (fixedCmdSession == null || fixedCmdSession.Process == null || fixedCmdSession.Process.HasExited)
            {
                StartFixedCmdSession();
            }

            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null)
            {
                AppendOutput("PC CMD 尚未就绪，无法执行快捷命令。");
                return;
            }

            if (IsFixedCmdPassthroughMode())
            {
                MessageBox.Show(this, "底部 PC CMD 当前处于 adb shell 或其他交互程序模式，请先退出该模式后再执行此快捷命令。", "PC CMD 当前不可用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendOutput("快捷执行已取消：PC CMD 当前处于透传/交互模式。");
                return;
            }

            string normalized = command.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                cmdCurrentInput.Length = 0;
                cmdCurrentInput.Append(line);
                cmdCurrentInputCursor = cmdCurrentInput.Length;
                RenderCmdCurrentInputLine();
                SubmitCmdCurrentInput();
            }

            if (cmdOutputBox != null && !cmdOutputBox.IsDisposed)
            {
                cmdOutputBox.Focus();
            }
        }

        private void RunQuickToolInEmbeddedShell(QuickTool tool)
        {
            if (tool == null || String.IsNullOrWhiteSpace(tool.Command))
            {
                return;
            }

            TerminalSession session = GetSelectedEmbeddedShellSession();
            if (session == null)
            {
                string serial = SelectedSerial();
                if (serial == null)
                {
                    return;
                }

                EnsureDeviceOnlineBeforeActionAsync(serial, "快捷执行 shell", delegate(bool online)
                {
                    if (!online)
                    {
                        return;
                    }
                    TerminalSession newSession = StartShellTab(serial);
                    if (newSession != null)
                    {
                        ExecuteQuickToolInEmbeddedShell(newSession, tool.Command);
                    }
                });
                return;
            }

            ExecuteQuickToolInEmbeddedShell(session, tool.Command);
        }

        private void ExecuteQuickToolInEmbeddedShell(TerminalSession session, string command)
        {
            if (session == null || String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string normalized = command.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsEmbeddedShellLocalEditMode(session))
                {
                    ResetEmbeddedShellHistoryNavigation(session);
                    session.CurrentInput.Length = 0;
                    session.CurrentInput.Append(line);
                    session.CurrentInputCursor = session.CurrentInput.Length;
                    RenderEmbeddedShellCurrentInputLine(session);
                    SubmitEmbeddedShellLocalInput(session);
                }
                else
                {
                    SendEmbeddedShellText(session, line + "\r");
                }
            }

            if (terminalTabs != null && session.Page != null)
            {
                terminalTabs.SelectedTab = session.Page;
            }
            if (session.Output != null && !session.Output.IsDisposed)
            {
                session.Output.Focus();
            }
        }

        private QuickTool FindQuickToolByName(string name, QuickTool except)
        {
            for (int i = 0; i < config.QuickTools.Count; i++)
            {
                QuickTool tool = config.QuickTools[i];
                if (tool != except && String.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return tool;
                }
            }
            return null;
        }

        private void OnQuickToolMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                quickDragStartPoint = e.Location;
            }
        }

        private void OnQuickToolMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            Control control = sender as Control;
            if (control == null || control.Tag == null)
            {
                return;
            }

            Size dragSize = SystemInformation.DragSize;
            Rectangle dragRect = new Rectangle(
                quickDragStartPoint.X - dragSize.Width / 2,
                quickDragStartPoint.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);
            if (!dragRect.Contains(e.Location))
            {
                quickToolSuppressClicksUntilUtc = DateTime.UtcNow.AddMilliseconds(600);
                control.DoDragDrop(control.Tag, DragDropEffects.Move);
                quickToolSuppressClicksUntilUtc = DateTime.UtcNow.AddMilliseconds(600);
            }
        }

        private bool ShouldSuppressQuickToolClick()
        {
            return DateTime.UtcNow < quickToolSuppressClicksUntilUtc;
        }

        private void OnQuickToolDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(QuickTool)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void OnQuickToolDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(QuickTool)))
            {
                return;
            }

            QuickTool source = e.Data.GetData(typeof(QuickTool)) as QuickTool;
            QuickTool target = null;
            Control targetControl = sender as Control;
            if (targetControl != null)
            {
                target = targetControl.Tag as QuickTool;
            }
            ReorderQuickTool(source, target);
        }

        private void ReorderQuickTool(QuickTool source, QuickTool target)
        {
            if (source == null || source == target)
            {
                return;
            }

            List<QuickTool> sourceGroup = GetQuickToolDragGroup(source);
            if (sourceGroup.Count == 0)
            {
                return;
            }
            if (target != null && sourceGroup.Contains(target))
            {
                return;
            }

            List<QuickTool> targetGroup = target == null ? null : GetQuickToolDragGroup(target);
            for (int i = 0; i < sourceGroup.Count; i++)
            {
                config.QuickTools.Remove(sourceGroup[i]);
            }

            int targetIndex = config.QuickTools.Count;
            if (targetGroup != null && targetGroup.Count > 0)
            {
                targetIndex = Int32.MaxValue;
                for (int i = 0; i < targetGroup.Count; i++)
                {
                    int index = config.QuickTools.IndexOf(targetGroup[i]);
                    if (index >= 0 && index < targetIndex)
                    {
                        targetIndex = index;
                    }
                }
                if (targetIndex == Int32.MaxValue)
                {
                    targetIndex = config.QuickTools.Count;
                }
            }
            if (targetIndex < 0)
            {
                targetIndex = config.QuickTools.Count;
            }
            config.QuickTools.InsertRange(targetIndex, sourceGroup);
            SaveConfig();
            BindQuickTools();
        }

        private List<QuickTool> GetQuickToolDragGroup(QuickTool tool)
        {
            List<QuickTool> result = new List<QuickTool>();
            if (tool == null || config == null || config.QuickTools == null)
            {
                return result;
            }

            QuickToolSwitchPair pair;
            if (TryGetQuickToolSwitchPair(tool, out pair))
            {
                for (int i = 0; i < config.QuickTools.Count; i++)
                {
                    QuickTool current = config.QuickTools[i];
                    if (current == pair.OnTool || current == pair.OffTool)
                    {
                        result.Add(current);
                    }
                }
                return result;
            }

            if (config.QuickTools.Contains(tool))
            {
                result.Add(tool);
            }
            return result;
        }

        private void RunQuickTool(QuickTool tool)
        {
            string type = ResolveQuickToolType(tool);
            if (type == "shell")
            {
                RunQuickToolInEmbeddedShell(tool);
            }
            else if (type == "py")
            {
                RunQuickToolInFixedCmd(QuoteCmdToken(GetPythonPath()) + " " + QuoteCmdToken(tool.Command));
            }
            else if (type == "ps1")
            {
                RunQuickToolInFixedCmd("powershell.exe -ExecutionPolicy Bypass -File " + QuoteCmdToken(tool.Command));
            }
            else if (type == "cmd")
            {
                RunQuickToolInFixedCmd(tool.Command);
            }
            else
            {
                AppendOutput("未知快捷工具类型: " + type);
            }
        }

        private void AppendCmd(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            lock (cmdPendingOutputLock)
            {
                cmdPendingOutput.Append(text);
                if (cmdFlushScheduled)
                {
                    return;
                }
                cmdFlushScheduled = true;
            }

            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                lock (cmdPendingOutputLock)
                {
                    cmdFlushScheduled = false;
                }
                return;
            }

            try
            {
                cmdOutputBox.BeginInvoke(new Action(FlushPendingCmdOutput));
            }
            catch (InvalidOperationException)
            {
                lock (cmdPendingOutputLock)
                {
                    cmdFlushScheduled = false;
                }
            }
        }

        private void FlushPendingCmdOutput()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                lock (cmdPendingOutputLock)
                {
                    cmdFlushScheduled = false;
                }
                return;
            }

            string text;
            lock (cmdPendingOutputLock)
            {
                text = cmdPendingOutput.ToString();
                cmdPendingOutput.Length = 0;
                cmdFlushScheduled = false;
            }

            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            Point scrollPosition = GetRichTextScrollPosition(cmdOutputBox);
            bool keepAtBottom = !cmdWheelScrolledAwayFromBottom;

            bool clearDisplay;
            text = SanitizeCmdOutput(text, out clearDisplay);
            if (clearDisplay)
            {
                cmdOutputBox.Clear();
                cmdWheelScrolledAwayFromBottom = false;
                cmdSuppressLeadingBlankAfterClear = true;
                scrollPosition = Point.Empty;
                keepAtBottom = true;
            }
            if (!String.IsNullOrEmpty(text))
            {
                AppendCmdTextIncremental(text);
                if (fixedCmdAdbShellLocalMode)
                {
                    PruneTrailingDuplicateAdbShellPrompt(cmdOutputBox);
                }
                UpdateFixedCmdAdbShellInputStart();
                ColorizeCmdCurrentLine();
            }

            if (fixedCmdForcePassthrough && IsFixedCmdReadyForLocalInput())
            {
                fixedCmdForcePassthrough = false;
                fixedCmdAdbShellLocalMode = false;
                fixedCmdAdbShellInputStartIndex = -1;
            }

            cmdLastCursorIndex = cmdOutputBox.TextLength;
            RestoreCmdCaret();
            cmdLastCursorIndex = cmdOutputBox.SelectionStart;

            if (keepAtBottom)
            {
                ScrollCmdToBottom();
            }
            else
            {
                SetRichTextScrollPosition(cmdOutputBox, scrollPosition);
            }
        }

        private void AppendCmdTextIncremental(string text)
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(text))
            {
                return;
            }

            StringBuilder plain = new StringBuilder(text.Length);
            int plainColumn = GetCmdCurrentOutputColumn();
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (cmdSuppressLeadingBlankAfterClear)
                {
                    if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                    {
                        continue;
                    }
                    cmdSuppressLeadingBlankAfterClear = false;
                }

                if (ch == '\r')
                {
                    continue;
                }
                if (ch == '\n')
                {
                    if (plain.Length > 0)
                    {
                        AppendCmdOutputText(plain.ToString());
                        plain.Length = 0;
                    }
                    AppendCmdOutputText(Environment.NewLine);
                    plainColumn = 0;
                    continue;
                }
                if (ch == '\t')
                {
                    int spaces = 8 - (plainColumn % 8);
                    if (spaces <= 0)
                    {
                        spaces = 8;
                    }
                    plain.Append(new string(' ', spaces));
                    plainColumn += spaces;
                    continue;
                }
                if (ch == CmdMarkerLeft || ch == CmdMarkerRight ||
                    ch == CmdMarkerDelete || ch == CmdMarkerClearLineTail)
                {
                    if (plain.Length > 0)
                    {
                        AppendCmdOutputText(plain.ToString());
                        plain.Length = 0;
                    }

                    if (ch == CmdMarkerLeft)
                    {
                        MoveLocalCmdCaretLeft();
                    }
                    else if (ch == CmdMarkerRight)
                    {
                        MoveLocalCmdCaretRight();
                    }
                    else if (ch == CmdMarkerDelete)
                    {
                        ApplyLocalCmdDelete();
                    }
                    else
                    {
                        ClearLocalCmdLineTail();
                    }
                    continue;
                }
                if (ch == '\b')
                {
                    if (plain.Length > 0)
                    {
                        plain.Length--;
                        if (plainColumn > 0)
                        {
                            plainColumn--;
                        }
                    }
                    else if (cmdOutputBox.TextLength > 0)
                    {
                        cmdOutputBox.Select(cmdOutputBox.TextLength - 1, 1);
                        cmdOutputBox.SelectedText = "";
                    }
                    continue;
                }

                plain.Append(ch);
                plainColumn++;
            }

            if (plain.Length > 0)
            {
                AppendCmdOutputText(plain.ToString());
            }
        }

        private int GetCmdCurrentOutputColumn()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || cmdOutputBox.TextLength == 0)
            {
                return 0;
            }

            string text = cmdOutputBox.Text;
            int lineStart = text.LastIndexOf('\n');
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            return Math.Max(0, text.Length - lineStart);
        }

        private bool IsFixedCmdReadyForLocalInput()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(cmdOutputBox.Text))
            {
                return true;
            }

            string text = cmdOutputBox.Text;
            int lineStart = text.LastIndexOf('\n');
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int promptIndex = text.IndexOf('>', lineStart);
            if (promptIndex < lineStart)
            {
                return false;
            }

            string promptPath = text.Substring(lineStart, promptIndex - lineStart).Trim();
            if (promptPath.Length == 0 || !Directory.Exists(promptPath))
            {
                return false;
            }

            for (int i = promptIndex + 1; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch != '\r' && ch != '\n' && ch != '\t' && ch != ' ')
                {
                    return false;
                }
            }

            return true;
        }

        private void AppendCmdOutputText(string text)
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(text))
            {
                return;
            }

            cmdOutputBox.SelectionStart = cmdOutputBox.TextLength;
            cmdOutputBox.SelectionLength = 0;
            cmdOutputBox.SelectionColor = cmdOutputColor;
            cmdOutputBox.AppendText(text);
            cmdOutputBox.SelectionColor = cmdOutputColor;
        }

        private void ResetCmdOutputFilter()
        {
            cmdEscapePending = false;
            cmdCsiPending = false;
            cmdOscPending = false;
            cmdOscEscapePending = false;
            cmdSuppressLeadingBlankAfterClear = false;
            cmdControlBuffer.Length = 0;
        }

        private void RestoreCmdCaret()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int index = Math.Min(Math.Max(0, cmdLastCursorIndex), cmdOutputBox.TextLength);
            cmdOutputBox.SelectionStart = index;
            cmdOutputBox.SelectionLength = 0;
        }

        private void RestoreCmdCaretLater()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || !cmdOutputBox.IsHandleCreated)
            {
                return;
            }

            try
            {
                cmdOutputBox.BeginInvoke((Action)delegate
                {
                    RestoreCmdCaret();
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private string SanitizeCmdOutput(string text, out bool clearDisplay)
        {
            clearDisplay = false;
            if (String.IsNullOrEmpty(text))
            {
                return "";
            }

            StringBuilder output = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (cmdOscPending)
                {
                    if (cmdOscEscapePending)
                    {
                        cmdOscEscapePending = false;
                        if (ch == '\\')
                        {
                            cmdOscPending = false;
                            cmdControlBuffer.Length = 0;
                            continue;
                        }
                    }

                    if (ch == '\a')
                    {
                        cmdOscPending = false;
                        cmdControlBuffer.Length = 0;
                        continue;
                    }

                    if (ch == 0x1b)
                    {
                        cmdOscEscapePending = true;
                    }
                    else
                    {
                        cmdControlBuffer.Append(ch);
                    }
                    continue;
                }

                if (cmdCsiPending)
                {
                    cmdControlBuffer.Append(ch);
                    if (ch >= '@' && ch <= '~')
                    {
                        string sequence = cmdControlBuffer.ToString();
                        if (ch == 'J')
                        {
                            if (sequence.IndexOf("2", StringComparison.Ordinal) >= 0 ||
                                sequence.IndexOf("3", StringComparison.Ordinal) >= 0)
                            {
                                clearDisplay = true;
                                output.Length = 0;
                            }
                        }
                        else
                        {
                            AppendCmdCsiMarker(output, sequence);
                        }
                        cmdCsiPending = false;
                        cmdControlBuffer.Length = 0;
                    }
                    continue;
                }

                if (cmdEscapePending)
                {
                    cmdEscapePending = false;
                    if (ch == '[')
                    {
                        cmdCsiPending = true;
                        cmdControlBuffer.Length = 0;
                    }
                    else if (ch == ']')
                    {
                        cmdOscPending = true;
                        cmdOscEscapePending = false;
                        cmdControlBuffer.Length = 0;
                    }
                    else if (ch == 'c')
                    {
                        clearDisplay = true;
                        output.Length = 0;
                    }
                    continue;
                }

                if (ch == 0x1b)
                {
                    cmdEscapePending = true;
                    continue;
                }

                if (ch == '\a')
                {
                    continue;
                }
                if (ch == '\ufeff')
                {
                    continue;
                }
                if (ch == '\t')
                {
                    output.Append(ExpandCmdTabForText(output));
                    continue;
                }

                if (!Char.IsControl(ch) || ch == '\r' || ch == '\n' || ch == '\b')
                {
                    output.Append(ch);
                }
            }

            string sanitized = output.ToString();
            string terminalBackspace = new string(new char[] { CmdMarkerLeft, ' ', CmdMarkerLeft });
            if (sanitized.IndexOf(terminalBackspace, StringComparison.Ordinal) >= 0)
            {
                sanitized = sanitized.Replace(terminalBackspace, "\b");
            }
            return sanitized;
        }

        private string ExpandCmdTabForText(StringBuilder output)
        {
            int column = 0;
            if (output != null && output.Length > 0)
            {
                int lineStart = -1;
                for (int i = output.Length - 1; i >= 0; i--)
                {
                    char ch = output[i];
                    if (ch == '\n' || ch == '\r')
                    {
                        lineStart = i;
                        break;
                    }
                }
                column = lineStart >= 0
                    ? output.Length - lineStart - 1
                    : GetCmdCurrentOutputColumn() + output.Length;
            }
            else
            {
                column = GetCmdCurrentOutputColumn();
            }

            int spaces = 8 - (column % 8);
            if (spaces <= 0)
            {
                spaces = 8;
            }
            return new string(' ', spaces);
        }

        private void AppendCmdCsiMarker(StringBuilder output, string sequence)
        {
            if (output == null || String.IsNullOrEmpty(sequence))
            {
                return;
            }

            char command = sequence[sequence.Length - 1];
            string paramText = sequence.Substring(0, sequence.Length - 1);
            string firstParam = paramText;
            int semicolon = firstParam.IndexOf(';');
            if (semicolon >= 0)
            {
                firstParam = firstParam.Substring(0, semicolon);
            }
            int count;
            if (!Int32.TryParse(firstParam, out count) || count <= 0)
            {
                count = 1;
            }
            count = Math.Min(count, 200);

            char marker = '\0';
            if (command == 'D')
            {
                marker = CmdMarkerLeft;
            }
            else if (command == 'C')
            {
                marker = CmdMarkerRight;
            }
            else if (command == 'P')
            {
                marker = CmdMarkerDelete;
            }
            else if (command == 'K')
            {
                marker = CmdMarkerClearLineTail;
                count = 1;
            }

            if (marker == '\0')
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                output.Append(marker);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            TerminalSession embeddedSession = GetFocusedEmbeddedShellSession();
            if (embeddedSession != null)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                if (keyData == (Keys.Control | Keys.C))
                {
                    if (embeddedSession.Output.SelectionLength > 0)
                    {
                        CopySelectedEmbeddedShellText(embeddedSession);
                    }
                    else
                    {
                        SendEmbeddedShellText(embeddedSession, "\x3");
                    }
                    return true;
                }

                if (keyData == (Keys.Control | Keys.V))
                {
                    PasteIntoEmbeddedShell(embeddedSession);
                    return true;
                }

                if (keyCode == Keys.Tab)
                {
                    if (IsEmbeddedShellLocalEditMode(embeddedSession))
                    {
                        ResetEmbeddedShellHistoryNavigation(embeddedSession);
                        ApplyEmbeddedShellTabCompletion(embeddedSession);
                    }
                    else
                    {
                        SendEmbeddedShellText(embeddedSession, "\t");
                    }
                    return true;
                }
            }

            if (IsCmdInputFocused())
            {
                Keys keyCode = keyData & Keys.KeyCode;
                if (keyData == (Keys.Control | Keys.C))
                {
                    if (cmdOutputBox.SelectionLength > 0)
                    {
                        CopySelectedCmdText();
                    }
                    else
                    {
                        SendFixedCmdText("\x3");
                    }
                    return true;
                }

                if (keyData == (Keys.Control | Keys.V))
                {
                    PasteIntoCmd();
                    return true;
                }

                if (keyData == (Keys.Control | Keys.L))
                {
                    ClearFixedCmdDisplay();
                    return true;
                }

                if (keyCode == Keys.Tab)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (IsFixedCmdAdbShellLocalEditMode())
                        {
                            ResetAdbShellHistoryNavigation();
                            ApplyFixedCmdAdbShellTabCompletion();
                        }
                        else
                        {
                            SendFixedCmdText("\t");
                        }
                    }
                    else
                    {
                        ResetCmdHistoryNavigation();
                        ApplyFixedCmdTabCompletion();
                    }
                    return true;
                }

                if (keyCode == Keys.Back)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            return true;
                        }
                        SendFixedCmdText("\x7f");
                    }
                    else
                    {
                        ResetCmdHistoryNavigation();
                        HandleCmdBackspace();
                    }
                    return true;
                }

                if (keyCode == Keys.Delete)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            return true;
                        }
                        SendFixedCmdText("\x1b[3~");
                    }
                    else
                    {
                        ResetCmdHistoryNavigation();
                        SyncCmdCurrentInputFromVisibleLine();
                        TrackCmdDelete();
                        RenderCmdCurrentInputLine();
                    }
                    return true;
                }

                if (keyCode == Keys.Left)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            return true;
                        }
                        SendFixedCmdText("\x1b[D");
                        return true;
                    }
                    SyncCmdCurrentInputFromVisibleLine();
                    if (cmdCurrentInputCursor > 0)
                    {
                        cmdCurrentInputCursor--;
                    }
                    RestoreCmdInputCaretFromModel();
                    return true;
                }

                if (keyCode == Keys.Right)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            return true;
                        }
                        SendFixedCmdText("\x1b[C");
                        return true;
                    }
                    SyncCmdCurrentInputFromVisibleLine();
                    if (cmdCurrentInputCursor < cmdCurrentInput.Length)
                    {
                        cmdCurrentInputCursor++;
                    }
                    RestoreCmdInputCaretFromModel();
                    return true;
                }

                if (keyCode == Keys.Home)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (!HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            SendFixedCmdText("\x1b[H");
                        }
                    }
                    else
                    {
                        SyncCmdCurrentInputFromVisibleLine();
                        cmdCurrentInputCursor = 0;
                        RestoreCmdInputCaretFromModel();
                    }
                    return true;
                }

                if (keyCode == Keys.End)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (!HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            SendFixedCmdText("\x1b[F");
                        }
                    }
                    else
                    {
                        SyncCmdCurrentInputFromVisibleLine();
                        cmdCurrentInputCursor = cmdCurrentInput.Length;
                        RestoreCmdInputCaretFromModel();
                    }
                    return true;
                }

                if (keyCode == Keys.Up)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (IsFixedCmdAdbShellLocalEditMode())
                        {
                            NavigateAdbShellHistory(-1);
                        }
                        else
                        {
                            SendFixedCmdText("\x1b[A");
                        }
                    }
                    else
                    {
                        NavigateCmdHistory(-1);
                    }
                    return true;
                }

                if (keyCode == Keys.Down)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (IsFixedCmdAdbShellLocalEditMode())
                        {
                            NavigateAdbShellHistory(1);
                        }
                        else
                        {
                            SendFixedCmdText("\x1b[B");
                        }
                    }
                    else
                    {
                        NavigateCmdHistory(1);
                    }
                    return true;
                }

                if (keyCode == Keys.Enter)
                {
                    if (IsFixedCmdPassthroughMode())
                    {
                        if (!HandleFixedCmdAdbShellLocalEditKey(keyCode))
                        {
                            SendFixedCmdText("\r");
                        }
                    }
                    else
                    {
                        SubmitCmdCurrentInput();
                    }
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private TerminalSession GetFocusedEmbeddedShellSession()
        {
            if (terminalSessions == null || terminalSessions.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < terminalSessions.Count; i++)
            {
                TerminalSession session = terminalSessions[i];
                if (session == null || !session.UsesFixedCore || session.Output == null || session.Output.IsDisposed)
                {
                    continue;
                }

                if (terminalTabs != null && session.Page != terminalTabs.SelectedTab)
                {
                    continue;
                }

                if (session.Output.Focused || session.Output.ContainsFocus || ActiveControl == session.Output)
                {
                    return session;
                }
            }

            return null;
        }

        private TerminalSession GetSelectedEmbeddedShellSession()
        {
            if (terminalSessions == null || terminalSessions.Count == 0 || terminalTabs == null)
            {
                return null;
            }

            for (int i = 0; i < terminalSessions.Count; i++)
            {
                TerminalSession session = terminalSessions[i];
                if (session == null || !session.UsesFixedCore || session.Output == null || session.Output.IsDisposed)
                {
                    continue;
                }

                if (session.FixedSession == null || session.FixedSession.InputWriter == null)
                {
                    continue;
                }

                if (session.Page == terminalTabs.SelectedTab)
                {
                    return session;
                }
            }

            return null;
        }

        private bool IsCmdInputFocused()
        {
            return cmdOutputBox != null &&
                !cmdOutputBox.IsDisposed &&
                (cmdOutputBox.Focused || cmdOutputBox.ContainsFocus || ActiveControl == cmdOutputBox);
        }

        private int GetCmdInputStartIndex()
        {
            if (cmdOutputBox == null || String.IsNullOrEmpty(cmdOutputBox.Text))
            {
                return 0;
            }

            string text = cmdOutputBox.Text;
            int lineStart = text.LastIndexOf('\n');
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int prompt = text.IndexOf('>', lineStart);
            if (prompt >= lineStart)
            {
                string promptPath = text.Substring(lineStart, prompt - lineStart).Trim();
                if (promptPath.Length > 0 && Directory.Exists(promptPath))
                {
                    return Math.Min(prompt + 1, cmdOutputBox.TextLength);
                }
            }
            int shellInputStart;
            if (TryGetFixedCmdAdbShellInputStartIndex(out shellInputStart))
            {
                return shellInputStart;
            }
            return cmdOutputBox.TextLength;
        }

        private bool IsFixedCmdPromptLine()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(cmdOutputBox.Text))
            {
                return true;
            }

            string text = cmdOutputBox.Text;
            int lineStart = text.LastIndexOf('\n');
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int promptIndex = text.IndexOf('>', lineStart);
            if (promptIndex < lineStart)
            {
                return false;
            }

            string promptPath = text.Substring(lineStart, promptIndex - lineStart).Trim();
            return promptPath.Length > 0 && Directory.Exists(promptPath);
        }

        private bool IsFixedCmdAdbShellLocalEditMode()
        {
            int inputStart;
            return TryGetFixedCmdAdbShellInputStartIndex(out inputStart);
        }

        private bool TryGetFixedCmdAdbShellInputStartIndex(out int inputStart)
        {
            inputStart = 0;
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(cmdOutputBox.Text))
            {
                return false;
            }

            if (fixedCmdAdbShellLocalMode &&
                TryFindFixedCmdAdbShellInputStartIndex(cmdOutputBox.Text, out inputStart))
            {
                fixedCmdAdbShellInputStartIndex = inputStart;
                return true;
            }

            if (fixedCmdAdbShellLocalMode &&
                fixedCmdAdbShellInputStartIndex >= 0 &&
                fixedCmdAdbShellInputStartIndex <= cmdOutputBox.TextLength)
            {
                inputStart = fixedCmdAdbShellInputStartIndex;
                return true;
            }

            return TryFindFixedCmdAdbShellInputStartIndex(cmdOutputBox.Text, out inputStart);
        }

        private bool TryFindFixedCmdAdbShellInputStartIndex(string text, out int inputStart)
        {
            inputStart = 0;
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            int promptInputStart = FindLastAdbShellPromptInputStart(text);
            if (promptInputStart < 0)
            {
                return false;
            }

            inputStart = Math.Min(promptInputStart, text.Length);
            return true;
        }

        private void UpdateFixedCmdAdbShellInputStart()
        {
            if (!fixedCmdAdbShellLocalMode || cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                fixedCmdAdbShellInputStartIndex = -1;
                return;
            }

            int inputStart;
            if (TryFindFixedCmdAdbShellInputStartIndex(cmdOutputBox.Text, out inputStart))
            {
                fixedCmdAdbShellInputStartIndex = inputStart;
            }
        }

        private int FindAdbShellPromptMarker(string line)
        {
            if (String.IsNullOrEmpty(line))
            {
                return -1;
            }

            for (int i = line.Length - 2; i >= 0; i--)
            {
                char marker = line[i];
                if ((marker != '$' && marker != '#') || line[i + 1] != ' ')
                {
                    continue;
                }

                string prefix = line.Substring(0, i).Trim();
                if (prefix.Length == 0)
                {
                    return i;
                }

                string promptHead = GetLastPromptHeadToken(prefix);
                if (promptHead.IndexOf(':') >= 0 ||
                    promptHead.EndsWith("/", StringComparison.Ordinal) ||
                    promptHead.EndsWith("\\", StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindLastAdbShellPromptMarker(string text)
        {
            int inputStart = FindLastAdbShellPromptInputStart(text);
            return inputStart < 0 ? -1 : Math.Max(0, inputStart - 1);
        }

        private int FindLastAdbShellPromptInputStart(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (int i = text.Length - 1; i >= 0; i--)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                int inputStart = i + 1;
                if (inputStart < text.Length)
                {
                    char next = text[inputStart];
                    if (next == ' ')
                    {
                        inputStart++;
                    }
                    else if (next == '\t')
                    {
                        inputStart++;
                    }
                }

                int lineStart = text.LastIndexOf('\n', i);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                string prefix = text.Substring(lineStart, i - lineStart).Trim();
                if (prefix.Length == 0)
                {
                    return ResolveDuplicateAdbShellPromptInputStart(text, lineStart, inputStart);
                }

                string promptHead = GetLastPromptHeadToken(prefix);
                if (IsValidAdbShellPromptHead(promptHead))
                {
                    return ResolveDuplicateAdbShellPromptInputStart(text, lineStart, inputStart);
                }
            }

            return -1;
        }

        private int ResolveDuplicateAdbShellPromptInputStart(string text, int lineStart, int latestInputStart)
        {
            if (String.IsNullOrEmpty(text))
            {
                return latestInputStart;
            }

            int lineEnd = text.IndexOf('\n', latestInputStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            for (int i = lineStart; i < latestInputStart; i++)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                int candidateInputStart = GetAdbShellPromptInputStartAfterMarker(text, i);
                if (candidateInputStart <= lineStart || candidateInputStart >= latestInputStart)
                {
                    continue;
                }

                string candidatePrefix = text.Substring(lineStart, i - lineStart).Trim();
                if (candidatePrefix.Length > 0)
                {
                    string candidatePromptHead = GetLastPromptHeadToken(candidatePrefix);
                    if (!IsValidAdbShellPromptHead(candidatePromptHead))
                    {
                        continue;
                    }
                }

                int duplicatePromptLength;
                string suffix = text.Substring(candidateInputStart, lineEnd - candidateInputStart);
                if (TryGetLeadingAdbShellPromptTextLength(suffix, out duplicatePromptLength))
                {
                    return candidateInputStart;
                }
            }

            return latestInputStart;
        }

        private int GetAdbShellPromptInputStartAfterMarker(string text, int markerIndex)
        {
            int inputStart = markerIndex + 1;
            if (inputStart < text.Length)
            {
                char next = text[inputStart];
                if (next == ' ' || next == '\t')
                {
                    inputStart++;
                }
            }
            return inputStart;
        }

        private bool TryGetLeadingAdbShellPromptTextLength(string text, out int length)
        {
            length = 0;
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                string prefix = text.Substring(0, i).Trim();
                string promptHead = GetLastPromptHeadToken(prefix);
                if (!IsValidAdbShellPromptHead(promptHead))
                {
                    continue;
                }

                length = i + 1;
                if (length < text.Length && (text[length] == ' ' || text[length] == '\t'))
                {
                    length++;
                }
                return true;
            }

            return false;
        }

        private bool PruneTrailingDuplicateAdbShellPrompt(RichTextBox output)
        {
            if (output == null || output.IsDisposed || output.TextLength == 0)
            {
                return false;
            }

            int duplicateStart;
            int duplicateLength;
            if (!TryFindTrailingDuplicateAdbShellPrompt(output.Text, out duplicateStart, out duplicateLength))
            {
                return false;
            }

            int selectionStart = output.SelectionStart;
            bool wasReadOnly = output.ReadOnly;
            try
            {
                output.ReadOnly = false;
                output.Select(duplicateStart, duplicateLength);
                output.SelectedText = "";

                if (selectionStart > duplicateStart)
                {
                    selectionStart = Math.Max(duplicateStart, selectionStart - duplicateLength);
                }
                output.SelectionStart = Math.Min(selectionStart, output.TextLength);
                output.SelectionLength = 0;
                output.SelectionColor = cmdOutputColor;
                return true;
            }
            finally
            {
                if (wasReadOnly && output != null && !output.IsDisposed)
                {
                    output.ReadOnly = true;
                }
            }
        }

        private bool TryFindTrailingDuplicateAdbShellPrompt(string text, out int duplicateStart, out int duplicateLength)
        {
            duplicateStart = -1;
            duplicateLength = 0;
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            int lineEnd = text.Length;
            while (lineEnd > 0 && (text[lineEnd - 1] == '\r' || text[lineEnd - 1] == '\n'))
            {
                lineEnd--;
            }
            if (lineEnd <= 0)
            {
                return false;
            }

            int lineStart = text.LastIndexOf('\n', lineEnd - 1);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            for (int i = lineStart; i < lineEnd; i++)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                int firstInputStart = GetAdbShellPromptInputStartAfterMarker(text, i);
                if (firstInputStart <= lineStart || firstInputStart >= lineEnd)
                {
                    continue;
                }

                string prefix = text.Substring(lineStart, i - lineStart).Trim();
                if (prefix.Length > 0)
                {
                    string promptHead = GetLastPromptHeadToken(prefix);
                    if (!IsValidAdbShellPromptHead(promptHead))
                    {
                        continue;
                    }
                }

                string suffix = text.Substring(firstInputStart, lineEnd - firstInputStart);
                int duplicatePromptLength;
                if (!TryGetLeadingAdbShellPromptTextLength(suffix, out duplicatePromptLength))
                {
                    continue;
                }

                string afterDuplicatePrompt = suffix.Substring(duplicatePromptLength);
                if (afterDuplicatePrompt.Trim().Length != 0)
                {
                    continue;
                }

                duplicateStart = firstInputStart;
                duplicateLength = lineEnd - firstInputStart;
                return duplicateLength > 0;
            }

            return false;
        }

        private bool IsValidAdbShellPromptHead(string promptHead)
        {
            if (String.IsNullOrWhiteSpace(promptHead))
            {
                return false;
            }

            return promptHead.IndexOf(':') >= 0 ||
                promptHead.EndsWith("/", StringComparison.Ordinal) ||
                promptHead.EndsWith("\\", StringComparison.Ordinal);
        }

        private string GetLastPromptHeadToken(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text.Trim();
            int start = text.Length - 1;
            while (start >= 0 && !Char.IsWhiteSpace(text[start]))
            {
                start--;
            }

            return text.Substring(start + 1);
        }

        private bool IsFixedCmdPassthroughMode()
        {
            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null)
            {
                return false;
            }

            if (fixedCmdForcePassthrough)
            {
                return true;
            }

            return !IsFixedCmdPromptLine();
        }

        private bool HandleFixedCmdAdbShellLocalEditKey(Keys keyCode)
        {
            if (!IsFixedCmdAdbShellLocalEditMode())
            {
                return false;
            }

            if (keyCode == Keys.Tab)
            {
                ResetAdbShellHistoryNavigation();
                ApplyFixedCmdAdbShellTabCompletion();
                return true;
            }

            if (keyCode == Keys.Left)
            {
                SyncCmdCurrentInputFromVisibleLine();
                if (cmdCurrentInputCursor > 0)
                {
                    cmdCurrentInputCursor--;
                }
                RestoreCmdInputCaretFromModel();
                return true;
            }

            if (keyCode == Keys.Right)
            {
                SyncCmdCurrentInputFromVisibleLine();
                if (cmdCurrentInputCursor < cmdCurrentInput.Length)
                {
                    cmdCurrentInputCursor++;
                }
                RestoreCmdInputCaretFromModel();
                return true;
            }

            if (keyCode == Keys.Home)
            {
                SyncCmdCurrentInputFromVisibleLine();
                cmdCurrentInputCursor = 0;
                RestoreCmdInputCaretFromModel();
                return true;
            }

            if (keyCode == Keys.End)
            {
                SyncCmdCurrentInputFromVisibleLine();
                cmdCurrentInputCursor = cmdCurrentInput.Length;
                RestoreCmdInputCaretFromModel();
                return true;
            }

            if (keyCode == Keys.Back)
            {
                ResetAdbShellHistoryNavigation();
                HandleCmdBackspace();
                return true;
            }

            if (keyCode == Keys.Delete)
            {
                ResetAdbShellHistoryNavigation();
                SyncCmdCurrentInputFromVisibleLine();
                TrackCmdDelete();
                RenderCmdCurrentInputLine();
                return true;
            }

            if (keyCode == Keys.Up)
            {
                NavigateAdbShellHistory(-1);
                return true;
            }

            if (keyCode == Keys.Down)
            {
                NavigateAdbShellHistory(1);
                return true;
            }

            if (keyCode == Keys.Enter)
            {
                SubmitFixedCmdAdbShellLocalInput();
                return true;
            }

            return false;
        }

        private void SubmitFixedCmdAdbShellLocalInput()
        {
            SyncCmdCurrentInputFromVisibleLine();
            string command = cmdCurrentInput.ToString();
            AddAdbShellHistoryEntry(command);
            ResetAdbShellHistoryNavigation();
            cmdCurrentInput.Length = 0;
            cmdCurrentInputCursor = 0;
            RenderCmdCurrentInputLine();
            ScrollCmdToBottom();
            SendFixedCmdText(command + "\r");
        }

        private void PasteIntoFixedCmdAdbShellLine(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    ResetAdbShellHistoryNavigation();
                    SyncCmdCurrentInputFromVisibleLine();
                    TrackCmdInsertedText(lines[i]);
                    RenderCmdCurrentInputLine();
                }

                if (i < lines.Length - 1)
                {
                    SubmitFixedCmdAdbShellLocalInput();
                }
            }
        }

        private void SendFixedCmdPasteText(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            normalized = normalized.Replace('\n', '\r');
            SendFixedCmdText(normalized);
        }

        private void MoveLocalCmdCaretLeft()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int inputStart = GetCmdInputStartIndex();
            if (cmdOutputBox.SelectionStart > inputStart)
            {
                cmdOutputBox.SelectionStart--;
                cmdOutputBox.SelectionLength = 0;
            }
        }

        private void MoveLocalCmdCaretRight()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            if (cmdOutputBox.SelectionStart < cmdOutputBox.TextLength)
            {
                cmdOutputBox.SelectionStart++;
                cmdOutputBox.SelectionLength = 0;
                return;
            }

            AppendCmdOutputText(" ");
        }

        private void RestoreCmdInputCaretFromModel()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int inputStart = GetCmdInputStartIndex();
            int caret = Math.Min(inputStart + Math.Max(0, cmdCurrentInputCursor), cmdOutputBox.TextLength);
            try
            {
                cmdOutputBox.SelectionStart = caret;
                cmdOutputBox.SelectionLength = 0;
                cmdLastCursorIndex = caret;
            }
            catch
            {
            }
        }

        private void ApplyLocalCmdDelete()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int inputStart = GetCmdInputStartIndex();
            int start = Math.Max(inputStart, cmdOutputBox.SelectionStart);
            if (cmdOutputBox.SelectionLength > 0)
            {
                cmdOutputBox.Select(start, Math.Min(cmdOutputBox.SelectionLength, cmdOutputBox.TextLength - start));
                ReplaceCmdSelectedText("");
                cmdOutputBox.SelectionStart = start;
                cmdOutputBox.SelectionLength = 0;
                return;
            }

            if (start < cmdOutputBox.TextLength)
            {
                cmdOutputBox.Select(start, 1);
                ReplaceCmdSelectedText("");
                cmdOutputBox.SelectionStart = start;
                cmdOutputBox.SelectionLength = 0;
            }
        }

        private void ClearLocalCmdLineTail()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int start = Math.Max(GetCmdInputStartIndex(), cmdOutputBox.SelectionStart);
            int lineEnd = cmdOutputBox.Text.IndexOf('\n', start);
            if (lineEnd < 0)
            {
                lineEnd = cmdOutputBox.TextLength;
            }
            if (lineEnd > start)
            {
                cmdOutputBox.Select(start, lineEnd - start);
                ReplaceCmdSelectedText("");
                cmdOutputBox.SelectionStart = start;
                cmdOutputBox.SelectionLength = 0;
            }
        }

        private void TrackCmdInsertedText(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\n')
                {
                    cmdCurrentInput.Length = 0;
                    cmdCurrentInputCursor = 0;
                    continue;
                }

                cmdCurrentInput.Insert(cmdCurrentInputCursor, ch);
                cmdCurrentInputCursor++;
            }
        }

        private void HandleCmdBackspace()
        {
            SyncCmdCurrentInputFromVisibleLine();
            if (cmdCurrentInputCursor > 0 && cmdCurrentInput.Length > 0)
            {
                cmdCurrentInput.Remove(cmdCurrentInputCursor - 1, 1);
                cmdCurrentInputCursor--;
            }
            RenderCmdCurrentInputLine();
            if (cmdOutputBox != null && !cmdOutputBox.IsDisposed)
            {
                cmdLastCursorIndex = cmdOutputBox.SelectionStart;
            }
        }

        private void ResetCmdHistoryNavigation()
        {
            cmdHistoryIndex = -1;
            cmdHistoryDraft = "";
        }

        private void AddCmdHistoryEntry(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            if (cmdInputHistory.Count > 0 &&
                String.Equals(cmdInputHistory[cmdInputHistory.Count - 1], command, StringComparison.Ordinal))
            {
                return;
            }

            cmdInputHistory.Add(command);
            if (cmdInputHistory.Count > 200)
            {
                cmdInputHistory.RemoveAt(0);
            }
        }

        private void NavigateCmdHistory(int direction)
        {
            if (cmdInputHistory.Count == 0)
            {
                return;
            }

            if (cmdHistoryIndex < 0)
            {
                SyncCmdCurrentInputFromVisibleLine();
                cmdHistoryDraft = cmdCurrentInput.ToString();
                cmdHistoryIndex = cmdInputHistory.Count;
            }

            int next = cmdHistoryIndex + direction;
            if (next < 0)
            {
                next = 0;
            }
            if (next > cmdInputHistory.Count)
            {
                next = cmdInputHistory.Count;
            }

            cmdHistoryIndex = next;
            cmdCurrentInput.Length = 0;
            if (cmdHistoryIndex == cmdInputHistory.Count)
            {
                cmdCurrentInput.Append(cmdHistoryDraft);
            }
            else
            {
                cmdCurrentInput.Append(cmdInputHistory[cmdHistoryIndex]);
            }
            cmdCurrentInputCursor = cmdCurrentInput.Length;
            RenderCmdCurrentInputLine();
        }

        private void ResetAdbShellHistoryNavigation()
        {
            cmdAdbShellHistoryIndex = -1;
            cmdAdbShellHistoryDraft = "";
        }

        private void AddAdbShellHistoryEntry(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                return;
            }

            if (cmdAdbShellInputHistory.Count > 0 &&
                String.Equals(cmdAdbShellInputHistory[cmdAdbShellInputHistory.Count - 1], command, StringComparison.Ordinal))
            {
                return;
            }

            cmdAdbShellInputHistory.Add(command);
            if (cmdAdbShellInputHistory.Count > 200)
            {
                cmdAdbShellInputHistory.RemoveAt(0);
            }
        }

        private void NavigateAdbShellHistory(int direction)
        {
            if (cmdAdbShellInputHistory.Count == 0)
            {
                return;
            }

            if (cmdAdbShellHistoryIndex < 0)
            {
                SyncCmdCurrentInputFromVisibleLine();
                cmdAdbShellHistoryDraft = cmdCurrentInput.ToString();
                cmdAdbShellHistoryIndex = cmdAdbShellInputHistory.Count;
            }

            int next = cmdAdbShellHistoryIndex + direction;
            if (next < 0)
            {
                next = 0;
            }
            if (next > cmdAdbShellInputHistory.Count)
            {
                next = cmdAdbShellInputHistory.Count;
            }

            cmdAdbShellHistoryIndex = next;
            cmdCurrentInput.Length = 0;
            if (cmdAdbShellHistoryIndex == cmdAdbShellInputHistory.Count)
            {
                cmdCurrentInput.Append(cmdAdbShellHistoryDraft);
            }
            else
            {
                cmdCurrentInput.Append(cmdAdbShellInputHistory[cmdAdbShellHistoryIndex]);
            }
            cmdCurrentInputCursor = cmdCurrentInput.Length;
            RenderCmdCurrentInputLine();
        }

        private void ApplyFixedCmdTabCompletion()
        {
            SyncCmdCurrentInputFromVisibleLine();
            string input = cmdCurrentInput.ToString();
            int cursor = Math.Max(0, Math.Min(cmdCurrentInputCursor, input.Length));
            string beforeCaret = input.Substring(0, cursor);
            int tokenStart = FindCompletionTokenStart(beforeCaret);
            string token = beforeCaret.Substring(tokenStart);
            if (token.Length == 0)
            {
                return;
            }

            bool quoted = token.StartsWith("\"", StringComparison.Ordinal);
            string rawToken = quoted ? token.Substring(1) : token;
            if (rawToken.Length == 0)
            {
                return;
            }

            string currentDir = GetCurrentCmdPromptDirectory();
            if (String.IsNullOrWhiteSpace(currentDir) || !Directory.Exists(currentDir))
            {
                return;
            }

            string normalized = rawToken.Replace('/', '\\');
            int separatorIndex = normalized.LastIndexOf('\\');
            string typedDirPart = separatorIndex >= 0 ? normalized.Substring(0, separatorIndex + 1) : "";
            string typedNamePart = separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;

            string searchDirectory;
            try
            {
                if (typedDirPart.Length == 0)
                {
                    searchDirectory = currentDir;
                }
                else if (Path.IsPathRooted(typedDirPart))
                {
                    searchDirectory = typedDirPart.TrimEnd('\\');
                    if (searchDirectory.Length == 2 && searchDirectory[1] == ':')
                    {
                        searchDirectory += "\\";
                    }
                }
                else
                {
                    searchDirectory = Path.GetFullPath(Path.Combine(currentDir, typedDirPart));
                }
            }
            catch
            {
                return;
            }

            List<string> names = new List<string>();
            if (typedDirPart.Length == 0 && IsCmdCommandCompletionPosition(input, tokenStart))
            {
                for (int i = 0; i < CmdBasicCompletionCommands.Length; i++)
                {
                    if (CmdBasicCompletionCommands[i].StartsWith(typedNamePart, StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(CmdBasicCompletionCommands[i] + " ");
                    }
                }
            }

            if (Directory.Exists(searchDirectory))
            {
                string[] candidates;
                try
                {
                    candidates = Directory.GetFileSystemEntries(searchDirectory, typedNamePart + "*");
                }
                catch
                {
                    candidates = new string[0];
                }

                if (candidates != null)
                {
                    Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        names.Add(BuildCmdCompletionName(typedDirPart, candidates[i]));
                    }
                }
            }

            if (names.Count == 0)
            {
                return;
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            string replacementName;
            bool completeCandidate = names.Count == 1;
            if (completeCandidate)
            {
                replacementName = names[0];
            }
            else
            {
                replacementName = FindLongestCommonPrefix(names);
                if (replacementName.Length <= normalized.Length)
                {
                    return;
                }
            }

            string replacement = FormatCmdCompletionReplacement(replacementName, quoted, completeCandidate);
            string completed = input.Substring(0, tokenStart) + replacement + input.Substring(cursor);
            cmdCurrentInput.Length = 0;
            cmdCurrentInput.Append(completed);
            cmdCurrentInputCursor = tokenStart + replacement.Length;
            RenderCmdCurrentInputLine();
        }

        private void ApplyFixedCmdAdbShellTabCompletion()
        {
            SyncCmdCurrentInputFromVisibleLine();
            string input = cmdCurrentInput.ToString();
            int cursor = Math.Max(0, Math.Min(cmdCurrentInputCursor, input.Length));
            string completed;
            int completedCursor;
            List<string> names;
            string displayTrimPrefix;
            if (!TryResolveAdbShellTabCompletion(
                fixedCmdAdbShellSerial,
                GetCurrentAdbShellPromptDirectory(),
                input,
                cursor,
                out completed,
                out completedCursor,
                out names,
                out displayTrimPrefix))
            {
                return;
            }

            if (completed != null)
            {
                cmdCurrentInput.Length = 0;
                cmdCurrentInput.Append(completed);
                cmdCurrentInputCursor = completedCursor;
                RenderCmdCurrentInputLine();
                return;
            }

            if (names != null && names.Count > 1)
            {
                AppendAdbShellCompletionCandidates(names, input, cursor, displayTrimPrefix);
                return;
            }
        }

        private bool TryResolveAdbShellTabCompletion(
            string serial,
            string currentDir,
            string input,
            int cursor,
            out string completed,
            out int completedCursor,
            out List<string> names,
            out string displayTrimPrefix)
        {
            completed = null;
            completedCursor = cursor;
            names = null;
            displayTrimPrefix = "";

            if (input == null)
            {
                input = "";
            }

            cursor = Math.Max(0, Math.Min(cursor, input.Length));
            string beforeCaret = input.Substring(0, cursor);
            if (String.IsNullOrEmpty(beforeCaret))
            {
                return false;
            }

            bool commandCompletion = beforeCaret.IndexOfAny(new char[] { ' ', '\t' }) < 0;
            int tokenStart = commandCompletion ? 0 : FindCompletionTokenStart(beforeCaret);
            string token = beforeCaret.Substring(tokenStart);
            if (commandCompletion && token.Length == 0)
            {
                return false;
            }
            if (!commandCompletion && tokenStart <= 0)
            {
                return false;
            }

            char quote = '\0';
            string rawToken = token;
            if (!commandCompletion &&
                (rawToken.StartsWith("\"", StringComparison.Ordinal) || rawToken.StartsWith("'", StringComparison.Ordinal)))
            {
                quote = rawToken[0];
                rawToken = rawToken.Substring(1);
            }

            names = commandCompletion
                ? GetAdbShellCommandCompletionCandidatesForSerial(serial, rawToken)
                : GetAdbShellFileCompletionCandidatesForSerial(serial, currentDir, rawToken);
            if (names.Count == 0)
            {
                names = null;
                return false;
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            string replacementName = names.Count == 1 ? names[0] : FindLongestCommonPrefix(names);
            if (replacementName.Length > rawToken.Length)
            {
                string replacement = commandCompletion
                    ? replacementName
                    : FormatAdbShellCompletionReplacement(replacementName, quote);
                completed = input.Substring(0, tokenStart) + replacement + input.Substring(cursor);
                completedCursor = tokenStart + replacement.Length;
                displayTrimPrefix = commandCompletion ? "" : GetAdbShellFileCompletionDisplayTrimPrefix(rawToken);
                return true;
            }

            if (names.Count > 1)
            {
                displayTrimPrefix = commandCompletion ? "" : GetAdbShellFileCompletionDisplayTrimPrefix(rawToken);
                return true;
            }

            names = null;
            return false;
        }

        private void StartAdbShellCommandCompletionWarmup(string serial)
        {
            string cacheKey = NormalizeAdbShellCompletionSerial(serial);
            List<string> cached;
            if (TryGetAdbShellCompletionCache(adbShellCommandCompletionCache, cacheKey, AdbShellCommandCompletionCacheLifetime, out cached))
            {
                return;
            }

            lock (adbShellCompletionCacheLock)
            {
                if (adbShellCommandCompletionWarmups.Contains(cacheKey))
                {
                    return;
                }
                adbShellCommandCompletionWarmups.Add(cacheKey);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    List<string> commands = QueryAdbShellCommandCompletionList(cacheKey);
                    SetAdbShellCompletionCache(adbShellCommandCompletionCache, cacheKey, commands);
                }
                finally
                {
                    lock (adbShellCompletionCacheLock)
                    {
                        adbShellCommandCompletionWarmups.Remove(cacheKey);
                    }
                }
            });
        }

        private void StartAdbShellDirectoryCompletionWarmup(string serial, string searchDirectory, string cacheKey)
        {
            lock (adbShellCompletionCacheLock)
            {
                if (adbShellDirectoryCompletionWarmups.Contains(cacheKey))
                {
                    return;
                }
                adbShellDirectoryCompletionWarmups.Add(cacheKey);
            }

            AppendOutput("正在后台加载 Shell 补全候选: " + searchDirectory);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    List<string> items = QueryAdbShellDirectoryCompletionList(serial, searchDirectory);
                    SetAdbShellCompletionCache(adbShellDirectoryCompletionCache, cacheKey, items);
                }
                finally
                {
                    lock (adbShellCompletionCacheLock)
                    {
                        adbShellDirectoryCompletionWarmups.Remove(cacheKey);
                    }
                }
            });
        }

        private List<string> QueryAdbShellCommandCompletionList(string serial)
        {
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> commands = new List<string>();
            string command = "for d in $(echo \"$PATH\" | tr ':' ' '); do " +
                "[ -d \"$d\" ] || continue; " +
                "for f in \"$d\"/*; do " +
                "[ -e \"$f\" ] || continue; [ -d \"$f\" ] && continue; [ -x \"$f\" ] || continue; " +
                "echo \"${f##*/}\"; " +
                "done; " +
                "done";
            ProcessResult result = RunAdbShellCaptureForSerial(serial, command);
            if (result != null && result.ExitCode == 0 && !String.IsNullOrWhiteSpace(result.Output))
            {
                AddMatchingAdbShellCompletionLines(commands, unique, result.Output, "", false);
            }

            for (int i = 0; i < AdbShellBuiltinCompletionCommands.Length; i++)
            {
                string item = AdbShellBuiltinCompletionCommands[i];
                if (unique.Add(item))
                {
                    commands.Add(item);
                }
            }

            commands.Sort(StringComparer.OrdinalIgnoreCase);
            return commands;
        }

        private List<string> QueryAdbShellDirectoryCompletionList(string serial, string searchDirectory)
        {
            List<string> items = new List<string>();
            if (String.IsNullOrWhiteSpace(searchDirectory))
            {
                return items;
            }

            string command = "cd " + ShellQuote(searchDirectory) +
                " 2>/dev/null && for f in .* *; do " +
                "[ \"$f\" = \".\" ] && continue; [ \"$f\" = \"..\" ] && continue; " +
                "[ -e \"$f\" ] || continue; " +
                "[ -d \"$f\" ] && printf '%s/\\n' \"$f\" || printf '%s\\n' \"$f\"; " +
                "done 2>/dev/null";
            ProcessResult result = RunAdbShellCaptureForSerial(serial, command);
            if (result == null || result.ExitCode != 0 || String.IsNullOrWhiteSpace(result.Output))
            {
                return items;
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = NormalizeLineEndings(result.Output).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string item = lines[i].Trim();
                if (item.Length == 0)
                {
                    continue;
                }
                item = item.TrimEnd('\r');
                if (unique.Add(item))
                {
                    items.Add(item);
                }
            }

            items.Sort(StringComparer.OrdinalIgnoreCase);
            return items;
        }

        private bool TryGetAdbShellCompletionCache(Dictionary<string, AdbShellCompletionCacheEntry> cache, string key, TimeSpan lifetime, out List<string> items)
        {
            items = null;
            key = key ?? "";
            lock (adbShellCompletionCacheLock)
            {
                AdbShellCompletionCacheEntry entry;
                if (!cache.TryGetValue(key, out entry) || entry == null || entry.Items == null)
                {
                    return false;
                }

                if (DateTime.UtcNow - entry.Timestamp > lifetime)
                {
                    cache.Remove(key);
                    return false;
                }

                items = new List<string>(entry.Items);
                return true;
            }
        }

        private void SetAdbShellCompletionCache(Dictionary<string, AdbShellCompletionCacheEntry> cache, string key, List<string> items)
        {
            key = key ?? "";
            lock (adbShellCompletionCacheLock)
            {
                cache[key] = new AdbShellCompletionCacheEntry
                {
                    Items = items == null ? new List<string>() : new List<string>(items),
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private string GetFixedAdbShellCompletionCacheSerial()
        {
            return NormalizeAdbShellCompletionSerial(fixedCmdAdbShellSerial);
        }

        private string NormalizeAdbShellCompletionSerial(string serial)
        {
            return String.IsNullOrWhiteSpace(serial) ? "" : serial.Trim();
        }

        private string BuildAdbShellDirectoryCompletionCacheKey(string serial, string searchDirectory)
        {
            return NormalizeAdbShellCompletionSerial(serial) + "\n" + (searchDirectory ?? "");
        }

        private void AddMatchingAdbShellCompletionLines(List<string> names, HashSet<string> unique, string output, string prefix, bool appendCommandSpace)
        {
            if (names == null || unique == null || String.IsNullOrEmpty(output))
            {
                return;
            }

            string[] lines = NormalizeLineEndings(output).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string item = lines[i].Trim();
                if (item.Length == 0 || !item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (unique.Add(item))
                {
                    names.Add(appendCommandSpace ? item + " " : item);
                }
            }
        }

        private string GetAdbShellFileCompletionDisplayTrimPrefix(string rawToken)
        {
            if (String.IsNullOrEmpty(rawToken))
            {
                return "";
            }

            string normalized = rawToken.Replace('\\', '/');
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex >= 0 ? normalized.Substring(0, separatorIndex + 1) : "";
        }

        private void AppendAdbShellCompletionCandidates(List<string> names, string input, int cursor, string displayTrimPrefix)
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || names == null || names.Count == 0)
            {
                return;
            }

            string prompt = GetCurrentAdbShellPromptPrefix();
            if (String.IsNullOrEmpty(prompt))
            {
                return;
            }

            string formatted = FormatAdbShellCompletionColumns(names, displayTrimPrefix);
            if (String.IsNullOrWhiteSpace(formatted))
            {
                return;
            }

            cmdOutputBox.SelectionStart = cmdOutputBox.TextLength;
            cmdOutputBox.SelectionLength = 0;
            cmdOutputBox.SelectionColor = cmdOutputColor;
            cmdOutputBox.AppendText(Environment.NewLine + formatted + Environment.NewLine);

            cmdOutputBox.SelectionColor = cmdPromptColor;
            cmdOutputBox.AppendText(prompt);

            int inputStart = cmdOutputBox.TextLength;
            cmdOutputBox.SelectionColor = cmdInputColor;
            cmdOutputBox.AppendText(input);
            cmdOutputBox.SelectionColor = cmdOutputColor;

            fixedCmdAdbShellInputStartIndex = inputStart;
            cmdCurrentInput.Length = 0;
            cmdCurrentInput.Append(input);
            cmdCurrentInputCursor = Math.Max(0, Math.Min(cursor, input.Length));
            cmdOutputBox.SelectionStart = inputStart + cmdCurrentInputCursor;
            cmdOutputBox.SelectionLength = 0;
            cmdLastCursorIndex = cmdOutputBox.SelectionStart;
            ScrollCmdToBottom();
        }

        private string GetCurrentAdbShellPromptPrefix()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || String.IsNullOrEmpty(cmdOutputBox.Text))
            {
                return "";
            }

            int inputStart = GetCmdInputStartIndex();
            inputStart = Math.Max(0, Math.Min(inputStart, cmdOutputBox.TextLength));
            int lineStart = cmdOutputBox.Text.LastIndexOf('\n', Math.Max(0, inputStart - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            if (inputStart < lineStart)
            {
                return "";
            }
            return cmdOutputBox.Text.Substring(lineStart, inputStart - lineStart);
        }

        private string FormatAdbShellCompletionColumns(List<string> names, string displayTrimPrefix)
        {
            List<string> displayNames = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string item = FormatAdbShellCompletionDisplayName(names[i], displayTrimPrefix);
                if (!String.IsNullOrWhiteSpace(item))
                {
                    displayNames.Add(item);
                }
            }
            if (displayNames.Count == 0)
            {
                return "";
            }

            displayNames.Sort(StringComparer.OrdinalIgnoreCase);
            int maxLength = 0;
            for (int i = 0; i < displayNames.Count; i++)
            {
                maxLength = Math.Max(maxLength, displayNames[i].Length);
            }

            int consoleColumns = EstimateCmdTextColumns();
            int columnWidth = Math.Max(maxLength + 2, 8);
            int columnCount = Math.Max(1, consoleColumns / columnWidth);
            int rowCount = (displayNames.Count + columnCount - 1) / columnCount;

            StringBuilder builder = new StringBuilder();
            for (int row = 0; row < rowCount; row++)
            {
                if (row > 0)
                {
                    builder.Append(Environment.NewLine);
                }

                for (int column = 0; column < columnCount; column++)
                {
                    int index = column * rowCount + row;
                    if (index >= displayNames.Count)
                    {
                        continue;
                    }

                    string item = displayNames[index];
                    builder.Append(item);
                    if (column < columnCount - 1 && index + rowCount < displayNames.Count)
                    {
                        builder.Append(new string(' ', Math.Max(2, columnWidth - item.Length)));
                    }
                }
            }

            return builder.ToString();
        }

        private string FormatAdbShellCompletionDisplayName(string value, string displayTrimPrefix)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string display = value.TrimEnd(' ');
            if (!String.IsNullOrEmpty(displayTrimPrefix) &&
                display.StartsWith(displayTrimPrefix, StringComparison.OrdinalIgnoreCase))
            {
                display = display.Substring(displayTrimPrefix.Length);
            }

            return display;
        }

        private int EstimateCmdTextColumns()
        {
            return EstimateTerminalColumns(cmdOutputBox, 90);
        }

        private string NormalizeLineEndings(string text)
        {
            return String.IsNullOrEmpty(text) ? "" : text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private string ResolveAdbShellCompletionDirectory(string currentDir, string typedDirPart)
        {
            if (String.IsNullOrWhiteSpace(currentDir))
            {
                return null;
            }

            if (String.IsNullOrEmpty(typedDirPart))
            {
                return currentDir;
            }

            string value = typedDirPart.Replace('\\', '/').TrimEnd('/');
            if (value.Length == 0)
            {
                return "/";
            }
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                return value;
            }
            if (currentDir == "/")
            {
                return "/" + value;
            }
            return currentDir.TrimEnd('/') + "/" + value;
        }

        private string FormatAdbShellCompletionReplacement(string replacementName, char quote)
        {
            if (quote == '\0')
            {
                return replacementName;
            }
            return quote + replacementName;
        }

        private string BuildCmdCompletionName(string typedDirPart, string candidate)
        {
            string value = typedDirPart + Path.GetFileName(candidate);
            if (Directory.Exists(candidate))
            {
                value += "\\";
            }
            return value;
        }

        private bool IsCmdCommandCompletionPosition(string input, int tokenStart)
        {
            if (String.IsNullOrEmpty(input))
            {
                return true;
            }

            string beforeToken = input.Substring(0, Math.Max(0, Math.Min(tokenStart, input.Length))).Trim();
            if (beforeToken.Length == 0)
            {
                return true;
            }

            char last = beforeToken[beforeToken.Length - 1];
            return last == '&' || last == '|' || last == '(';
        }

        private bool IsCmdBasicCommandCompletion(string replacementName)
        {
            if (String.IsNullOrWhiteSpace(replacementName))
            {
                return false;
            }

            string command = replacementName.Trim();
            for (int i = 0; i < CmdBasicCompletionCommands.Length; i++)
            {
                if (String.Equals(command, CmdBasicCompletionCommands[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string FindLongestCommonPrefix(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "";
            }

            string prefix = values[0] ?? "";
            for (int i = 1; i < values.Count && prefix.Length > 0; i++)
            {
                string value = values[i] ?? "";
                while (prefix.Length > 0 && !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                }
            }
            return prefix;
        }

        private string FormatCmdCompletionReplacement(string replacementName, bool quoted, bool completeCandidate)
        {
            if (!quoted && IsCmdBasicCommandCompletion(replacementName))
            {
                return replacementName;
            }

            bool needsQuote = quoted || CmdCompletionNeedsQuotes(replacementName);
            if (!needsQuote)
            {
                return replacementName;
            }

            string replacement = "\"" + replacementName;
            if (completeCandidate)
            {
                replacement += "\"";
            }
            return replacement;
        }

        private bool CmdCompletionNeedsQuotes(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.IndexOfAny(new char[] { ' ', '&', '(', ')', '[', ']', '{', '}', '^', '=', ';', ',', '\'' }) >= 0;
        }

        private void SubmitCmdCurrentInput()
        {
            SyncCmdCurrentInputFromVisibleLine();
            string command = cmdCurrentInput.ToString();
            AddCmdHistoryEntry(command);
            ResetCmdHistoryNavigation();
            cmdCurrentInput.Length = 0;
            cmdCurrentInputCursor = 0;
            RenderCmdCurrentInputLine();
            ScrollCmdToBottom();
            string adbShellSerial;
            if (TryParseAdbShellStartCommand(command, out adbShellSerial))
            {
                fixedCmdForcePassthrough = true;
                fixedCmdAdbShellLocalMode = true;
                fixedCmdAdbShellInputStartIndex = -1;
                fixedCmdAdbShellSerial = adbShellSerial;
                ResetAdbShellHistoryNavigation();
                StartAdbShellCommandCompletionWarmup(adbShellSerial);
            }
            ScheduleDeviceRefreshAfterCmdAdbTopologyCommand(command);
            SendFixedCmdText(command + "\r");
        }

        private void ScheduleDeviceRefreshAfterCmdAdbTopologyCommand(string command)
        {
            string debounceKey;
            if (!TryGetCmdAdbTopologyDebounceKey(command, out debounceKey))
            {
                return;
            }

            System.Windows.Forms.Timer timer;
            if (!cmdAdbDeviceRefreshTimers.TryGetValue(debounceKey, out timer))
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 3000;
                timer.Tick += delegate
                {
                    timer.Stop();
                    RefreshDevicesIncrementalFromCmd();
                };
                cmdAdbDeviceRefreshTimers[debounceKey] = timer;
            }

            timer.Stop();
            timer.Start();
        }

        private bool IsCmdAdbTopologyCommand(string command)
        {
            string debounceKey;
            return TryGetCmdAdbTopologyDebounceKey(command, out debounceKey);
        }

        private bool TryGetCmdAdbTopologyDebounceKey(string command, out string debounceKey)
        {
            debounceKey = null;
            if (String.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            string[] parts = SplitWhitespace(command.Trim());
            if (parts.Length < 2 || !IsAdbExecutableToken(parts[0]))
            {
                return false;
            }

            string action = parts[1].Trim().ToLowerInvariant();
            if (action != "connect" && action != "disconnect")
            {
                return false;
            }

            string target = parts.Length >= 3 ? NormalizeAdbTopologyTarget(parts[2]) : "";
            debounceKey = action + "|" + target;
            return true;
        }

        private string NormalizeAdbTopologyTarget(string target)
        {
            if (String.IsNullOrWhiteSpace(target))
            {
                return "";
            }
            return target.Trim().Trim('"').ToLowerInvariant();
        }

        private bool IsAdbExecutableToken(string token)
        {
            if (String.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string value = token.Trim().Trim('"');
            value = value.Replace('/', '\\');
            int slash = value.LastIndexOf('\\');
            if (slash >= 0 && slash < value.Length - 1)
            {
                value = value.Substring(slash + 1);
            }

            return String.Equals(value, "adb", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "adb.exe", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAdbShellStartCommand(string command)
        {
            string ignored;
            return TryParseAdbShellStartCommand(command, out ignored);
        }

        private bool TryParseAdbShellStartCommand(string command, out string serial)
        {
            serial = null;
            if (String.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            string trimmed = command.Trim();
            if (!trimmed.StartsWith("adb", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = trimmed.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < parts.Length; i++)
            {
                if (String.Equals(parts[i], "-s", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                {
                    serial = parts[i + 1];
                    i++;
                    continue;
                }

                if (String.Equals(parts[i], "shell", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrWhiteSpace(serial))
                    {
                        serial = SelectedSerialOrEmpty();
                    }
                    return true;
                }
            }

            return false;
        }

        private void SubmitPastedCmdLines(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            ResetCmdHistoryNavigation();
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
            {
                count--;
            }

            if (count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                TrackCmdInsertedText(lines[i]);
                SubmitCmdCurrentInput();
            }
        }

        private void TrackCmdDelete()
        {
            if (cmdCurrentInputCursor < 0)
            {
                cmdCurrentInputCursor = 0;
            }
            if (cmdCurrentInputCursor >= cmdCurrentInput.Length)
            {
                return;
            }

            cmdCurrentInput.Remove(cmdCurrentInputCursor, 1);
        }

        private void SyncCmdCurrentInputFromVisibleLine()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int inputStart = GetCmdInputStartIndex();
            int lineEnd = cmdOutputBox.Text.IndexOf('\n', inputStart);
            if (lineEnd < 0)
            {
                lineEnd = cmdOutputBox.TextLength;
            }

            if (fixedCmdAdbShellLocalMode)
            {
                int selectionStart = cmdOutputBox.SelectionStart;
                int selectionEnd = selectionStart + cmdOutputBox.SelectionLength;
                bool selectionOutsideLatestInput =
                    selectionStart < inputStart ||
                    selectionStart > lineEnd ||
                    selectionEnd > lineEnd;
                if (selectionOutsideLatestInput)
                {
                    cmdOutputBox.SelectionStart = lineEnd;
                    cmdOutputBox.SelectionLength = 0;
                    cmdLastCursorIndex = lineEnd;
                }
            }

            int length = Math.Max(0, lineEnd - inputStart);
            string current = length == 0 ? "" : cmdOutputBox.Text.Substring(inputStart, length);
            cmdCurrentInput.Length = 0;
            cmdCurrentInput.Append(current);
            cmdCurrentInputCursor = Math.Max(0, Math.Min(cmdOutputBox.SelectionStart - inputStart, cmdCurrentInput.Length));
        }

        private void RenderCmdCurrentInputLine()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            int inputStart = GetCmdInputStartIndex();
            int lineEnd = cmdOutputBox.Text.IndexOf('\n', inputStart);
            if (lineEnd < 0)
            {
                lineEnd = cmdOutputBox.TextLength;
            }

            bool redrawSuspended = false;
            try
            {
                if (cmdOutputBox.IsHandleCreated)
                {
                    SendMessage(cmdOutputBox.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
                    redrawSuspended = true;
                }

                cmdOutputBox.Select(inputStart, Math.Max(0, lineEnd - inputStart));
                ReplaceCmdSelectedText(cmdCurrentInput.ToString());
                ApplyCmdCurrentLineColors(inputStart);
                cmdOutputBox.SelectionStart = Math.Min(inputStart + cmdCurrentInputCursor, cmdOutputBox.TextLength);
                cmdOutputBox.SelectionLength = 0;
            }
            finally
            {
                if (redrawSuspended && cmdOutputBox != null && !cmdOutputBox.IsDisposed && cmdOutputBox.IsHandleCreated)
                {
                    SendMessage(cmdOutputBox.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                    cmdOutputBox.Invalidate();
                }
            }
        }

        private void ColorizeCmdCurrentLine()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            ApplyCmdCurrentLineColors(GetCmdInputStartIndex());
        }

        private void ApplyCmdCurrentLineColors(int inputStart)
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            inputStart = Math.Max(0, Math.Min(inputStart, cmdOutputBox.TextLength));
            int lineStart = cmdOutputBox.Text.LastIndexOf('\n', Math.Max(0, inputStart - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int promptIndex = cmdOutputBox.Text.IndexOf('>', lineStart);
            if (promptIndex < lineStart || promptIndex + 1 != inputStart)
            {
                return;
            }

            int promptLength = Math.Max(0, inputStart - lineStart);
            if (promptLength > 0)
            {
                cmdOutputBox.Select(lineStart, promptLength);
                cmdOutputBox.SelectionColor = cmdPromptColor;
            }

            int inputLength = Math.Min(cmdCurrentInput.Length, Math.Max(0, cmdOutputBox.TextLength - inputStart));
            if (inputLength > 0)
            {
                cmdOutputBox.Select(inputStart, inputLength);
                cmdOutputBox.SelectionColor = cmdInputColor;
            }

            cmdOutputBox.SelectionLength = 0;
            cmdOutputBox.SelectionColor = cmdOutputColor;
        }

        private void ReplaceCmdSelectedText(string text)
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            bool wasReadOnly = cmdOutputBox.ReadOnly;
            if (wasReadOnly)
            {
                cmdOutputBox.ReadOnly = false;
            }
            try
            {
                cmdOutputBox.SelectedText = text ?? "";
            }
            finally
            {
                if (wasReadOnly && cmdOutputBox != null && !cmdOutputBox.IsDisposed)
                {
                    cmdOutputBox.ReadOnly = true;
                }
            }
        }

        private void OnCmdOutputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C && cmdOutputBox.SelectionLength > 0)
            {
                e.SuppressKeyPress = true;
                CopySelectedCmdText();
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteIntoCmd();
                return;
            }

            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null)
            {
                return;
            }

            if (IsFixedCmdPassthroughMode())
            {
                if (HandleFixedCmdAdbShellLocalEditKey(e.KeyCode))
                {
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\t");
                    return;
                }
                if (e.KeyCode == Keys.Left)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[D");
                    return;
                }
                if (e.KeyCode == Keys.Right)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[C");
                    return;
                }
                if (e.KeyCode == Keys.Home)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[H");
                    return;
                }
                if (e.KeyCode == Keys.End)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[F");
                    return;
                }
                if (e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[A");
                    return;
                }
                if (e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[B");
                    return;
                }
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\r");
                    return;
                }
                if (e.KeyCode == Keys.Back)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x7f");
                    return;
                }
                if (e.KeyCode == Keys.Delete)
                {
                    e.SuppressKeyPress = true;
                    SendFixedCmdText("\x1b[3~");
                    return;
                }
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                SendFixedCmdText("\x3");
                return;
            }

            if (e.Control && e.KeyCode == Keys.L)
            {
                e.SuppressKeyPress = true;
                ClearFixedCmdDisplay();
                return;
            }

            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                ResetCmdHistoryNavigation();
                ApplyFixedCmdTabCompletion();
                return;
            }

            if (e.KeyCode == Keys.Left)
            {
                e.SuppressKeyPress = true;
                SyncCmdCurrentInputFromVisibleLine();
                if (cmdCurrentInputCursor > 0)
                {
                    cmdCurrentInputCursor--;
                }
                RestoreCmdInputCaretFromModel();
                return;
            }

            if (e.KeyCode == Keys.Right)
            {
                e.SuppressKeyPress = true;
                SyncCmdCurrentInputFromVisibleLine();
                if (cmdCurrentInputCursor < cmdCurrentInput.Length)
                {
                    cmdCurrentInputCursor++;
                }
                RestoreCmdInputCaretFromModel();
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                e.SuppressKeyPress = true;
                SyncCmdCurrentInputFromVisibleLine();
                cmdCurrentInputCursor = 0;
                RestoreCmdInputCaretFromModel();
                return;
            }

            if (e.KeyCode == Keys.End)
            {
                e.SuppressKeyPress = true;
                SyncCmdCurrentInputFromVisibleLine();
                cmdCurrentInputCursor = cmdCurrentInput.Length;
                RestoreCmdInputCaretFromModel();
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                NavigateCmdHistory(-1);
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                NavigateCmdHistory(1);
                return;
            }

            string sequence = null;
            switch (e.KeyCode)
            {
                case Keys.Enter: sequence = "\r"; break;
            }

            if (sequence != null)
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.Enter)
                {
                    SubmitCmdCurrentInput();
                }
                else
                {
                    SendFixedCmdText(sequence);
                }
                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                ResetCmdHistoryNavigation();
                HandleCmdBackspace();
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                ResetCmdHistoryNavigation();
                SyncCmdCurrentInputFromVisibleLine();
                TrackCmdDelete();
                RenderCmdCurrentInputLine();
                return;
            }
        }

        private void OnCmdOutputKeyPress(object sender, KeyPressEventArgs e)
        {
            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null)
            {
                return;
            }

            if (IsFixedCmdPassthroughMode())
            {
                e.Handled = true;
                if (IsFixedCmdAdbShellLocalEditMode())
                {
                    if (!Char.IsControl(e.KeyChar))
                    {
                        ResetAdbShellHistoryNavigation();
                        SyncCmdCurrentInputFromVisibleLine();
                        TrackCmdInsertedText(e.KeyChar.ToString());
                        RenderCmdCurrentInputLine();
                    }
                    return;
                }
                if (!Char.IsControl(e.KeyChar))
                {
                    SendFixedCmdText(e.KeyChar.ToString());
                }
                return;
            }

            if (!Char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                ResetCmdHistoryNavigation();
                TrackCmdInsertedText(e.KeyChar.ToString());
                RenderCmdCurrentInputLine();
            }
        }

        private string GetCurrentCmdPromptDirectory()
        {
            if (cmdOutputBox == null || String.IsNullOrWhiteSpace(cmdOutputBox.Text))
            {
                return cmdWorkingDirectory;
            }

            string[] lines = cmdOutputBox.Text.Replace("\r\n", "\n").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].TrimEnd();
                int promptIndex = line.LastIndexOf('>');
                if (promptIndex > 0)
                {
                    string promptPath = line.Substring(0, promptIndex).Trim();
                    if (Directory.Exists(promptPath))
                    {
                        return promptPath;
                    }
                }
            }

            return cmdWorkingDirectory;
        }

        private string GetCurrentAdbShellPromptDirectory()
        {
            if (cmdOutputBox == null || String.IsNullOrWhiteSpace(cmdOutputBox.Text))
            {
                return "/";
            }

            string text = cmdOutputBox.Text;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                char marker = text[i];
                if (marker != '$' && marker != '#')
                {
                    continue;
                }

                int lineStart = text.LastIndexOf('\n', i);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                string prefix = text.Substring(lineStart, i - lineStart).Trim();
                string promptHead = GetLastPromptHeadToken(prefix);
                string path = ExtractAdbShellPromptPath(promptHead);
                if (!String.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return "/";
        }

        private string ExtractAdbShellPromptPath(string promptHead)
        {
            if (String.IsNullOrWhiteSpace(promptHead))
            {
                return null;
            }

            string value = promptHead.Trim();
            int colon = value.LastIndexOf(':');
            if (colon >= 0 && colon + 1 < value.Length)
            {
                value = value.Substring(colon + 1);
            }
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return value.StartsWith("/", StringComparison.Ordinal) ? value : "/";
        }

        private ProcessResult RunFixedAdbShellCapture(string command)
        {
            return RunAdbShellCaptureForSerial(fixedCmdAdbShellSerial, command);
        }

        private ProcessResult RunAdbShellCaptureForSerial(string serial, string command)
        {
            string adb = GetAdbPath();
            if (String.IsNullOrWhiteSpace(adb))
            {
                return null;
            }

            string args = String.IsNullOrWhiteSpace(serial)
                ? "shell " + command
                : "-s " + QuoteArg(serial) + " shell " + command;
            return RunProcess(adb, args, null, false);
        }

        private void SendFixedCmdText(string text)
        {
            if (fixedCmdSession == null || fixedCmdSession.InputWriter == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                fixedCmdSession.InputWriter.Write(text);
                fixedCmdSession.InputWriter.Flush();
            }
            catch
            {
            }
        }

        private void ScrollCmdToBottom()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed)
            {
                return;
            }

            if (cmdOutputBox.InvokeRequired)
            {
                cmdOutputBox.BeginInvoke(new Action(ScrollCmdToBottom));
                return;
            }

            SendCmdScrollToBottom();
            cmdWheelScrolledAwayFromBottom = false;
        }

        private void SendCmdScrollToBottom()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || !cmdOutputBox.IsHandleCreated)
            {
                return;
            }

            SendMessage(cmdOutputBox.Handle, WmVScroll, new IntPtr(SbBottom), IntPtr.Zero);
        }

        private Point GetRichTextScrollPosition(RichTextBox box)
        {
            Point point = Point.Empty;
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
            {
                return point;
            }

            try
            {
                SendMessage(box.Handle, EmGetScrollPos, IntPtr.Zero, ref point);
            }
            catch
            {
            }
            return point;
        }

        private void SetRichTextScrollPosition(RichTextBox box, Point point)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
            {
                return;
            }

            try
            {
                SendMessage(box.Handle, EmSetScrollPos, IntPtr.Zero, ref point);
            }
            catch
            {
            }
        }

        private bool IsCmdScrolledToBottom()
        {
            if (cmdOutputBox == null || cmdOutputBox.IsDisposed || !cmdOutputBox.IsHandleCreated)
            {
                return true;
            }

            if (cmdOutputBox.TextLength == 0)
            {
                return true;
            }

            try
            {
                int charIndex = Math.Max(0, cmdOutputBox.TextLength - 1);
                Point lastCharPosition = cmdOutputBox.GetPositionFromCharIndex(charIndex);
                return lastCharPosition.Y <= cmdOutputBox.ClientSize.Height - Math.Max(2, cmdOutputBox.Font.Height / 2);
            }
            catch
            {
                return true;
            }
        }

        private void OnFilesGridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                filesGrid.ClearSelection();
                filesGrid.Rows[e.RowIndex].Selected = true;
            }
            else
            {
                filesGrid.ClearSelection();
            }
        }

        private void OnFilesGridCellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                return;
            }

            FileEntry entry = filesGrid.Rows[e.RowIndex].Tag as FileEntry;
            if (entry == null)
            {
                return;
            }

            if (entry.Type == "目录")
            {
                remotePathTextBox.Text = entry.FullPath;
                RefreshFiles();
                return;
            }

            if (IsEditableTextFile(entry.Name))
            {
                EditSelectedTextFile();
                return;
            }

            OpenSelectedFile();
        }

        private void OnFilesGridMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (filesRightClickTimer != null && filesRightClickTimer.Enabled)
                {
                    filesRightClickTimer.Stop();
                    GoParentDirectory();
                }
            }
        }

        private void OnFilesGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            if (filesRightClickTimer != null && filesRightClickTimer.Enabled && IsWithinDoubleClickArea(pendingFilesMenuLocation, e.Location))
            {
                filesRightClickTimer.Stop();
                GoParentDirectory();
                return;
            }

            pendingFilesMenuLocation = e.Location;
            if (filesRightClickTimer != null)
            {
                filesRightClickTimer.Stop();
                filesRightClickTimer.Start();
            }
        }

        private void OnFilesRightClickTimerTick(object sender, EventArgs e)
        {
            filesRightClickTimer.Stop();
            if (filesMenu != null)
            {
                filesMenu.Show(filesGrid, pendingFilesMenuLocation);
            }
        }

        private bool IsWithinDoubleClickArea(Point first, Point second)
        {
            Size size = SystemInformation.DoubleClickSize;
            Rectangle area = new Rectangle(
                first.X - size.Width / 2,
                first.Y - size.Height / 2,
                size.Width,
                size.Height);
            return area.Contains(second);
        }

        private void OnFilesGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0)
            {
                return;
            }

            string columnName = filesGrid.Columns[e.ColumnIndex].Name;
            if (columnName != "Name" && columnName != "Type" && columnName != "Size" && columnName != "Modified")
            {
                return;
            }

            if (fileSortColumn == columnName)
            {
                fileSortAscending = !fileSortAscending;
            }
            else
            {
                fileSortColumn = columnName;
                fileSortAscending = true;
            }
            RenderFiles();
        }

        private void OnFilesGridDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnFilesGridDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            UploadLocalPaths(serial, paths, remotePathTextBox.Text.Trim());
        }

        private void OnAppsGridDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = HasApkFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnAppsGridDragDrop(object sender, DragEventArgs e)
        {
            if (!HasApkFileDrop(e))
            {
                return;
            }

            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            InstallApkFiles(paths);
        }

        private bool HasApkFileDrop(DragEventArgs e)
        {
            if (e == null || e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path) && String.Equals(Path.GetExtension(path), ".apk", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnFilesMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            FileEntry entry = SelectedFileEntry(false);
            bool hasSelection = entry != null;
            bool hasClipboard = fileClipboardEntry != null && !String.IsNullOrWhiteSpace(fileClipboardOperation);
            bool hasDownloadedFile = hasSelection && IsDownloadedFileComplete(entry);
            string[] selectionOnly = new string[] { "复制", "剪切", "删除", "重命名", "下载", "属性" };
            string[] withUpload = new string[] { "上传" };

            for (int i = 0; i < filesMenu.Items.Count; i++)
            {
                ToolStripItem item = filesMenu.Items[i];
                if (item is ToolStripSeparator)
                {
                    continue;
                }
                item.Visible = true;

                if (Array.IndexOf(selectionOnly, item.Text) >= 0)
                {
                    item.Enabled = hasSelection;
                }
                else if (item.Text == "打开目录")
                {
                    item.Visible = hasDownloadedFile;
                    item.Enabled = hasDownloadedFile;
                }
                else if (item.Text == "编辑文本")
                {
                    item.Enabled = hasSelection && entry.Type == "文件";
                }
                else if (item.Text == "粘贴")
                {
                    item.Enabled = hasClipboard;
                }
                else if (item.Text == "新建文件" || item.Text == "新建文件夹")
                {
                    item.Enabled = true;
                }
                else if (Array.IndexOf(withUpload, item.Text) >= 0)
                {
                    item.Enabled = true;
                }
            }
        }

        private void RefreshFiles()
        {
            int requestId = unchecked(++fileRefreshRequestId);
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }
            UpdateSelectedDeviceHeader();

            string path = remotePathTextBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(path))
            {
                path = "/sdcard/";
                remotePathTextBox.Text = path;
            }

            AppendOutput("正在读取文件列表: " + path);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult result = RunAdbShellCapture(serial, "ls -la " + ShellQuote(path));
                List<FileEntry> entries = ParseFileEntries(path, result.Output);
                BeginInvokeIfAvailable(delegate
                {
                    if (requestId != fileRefreshRequestId ||
                        !String.Equals(serial, SelectedSerialOrEmpty(), StringComparison.OrdinalIgnoreCase) ||
                        !String.Equals(path, remotePathTextBox.Text.Trim(), StringComparison.Ordinal))
                    {
                        return;
                    }

                    filesGrid.Rows.Clear();
                    if (!String.IsNullOrWhiteSpace(result.Error))
                    {
                        AppendOutput(result.Error.Trim());
                    }
                    currentFileEntries = entries;
                    RenderFiles();
                    AppendOutput("文件列表已刷新: " + path);
                });
            });
        }

        private List<FileEntry> ParseFileEntries(string basePath, string output)
        {
            List<FileEntry> entries = new List<FileEntry>();
            if (String.IsNullOrWhiteSpace(output))
            {
                return entries;
            }

            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("total "))
                {
                    continue;
                }

                string[] parts = SplitWhitespace(line);
                if (parts.Length < 7)
                {
                    continue;
                }

                string perm = parts[0];
                string size = parts.Length > 4 ? parts[4] : "";
                int nameStart = ResolveLsNameStart(parts);
                string modified = ResolveLsModified(parts, nameStart);
                string name = JoinParts(parts, nameStart);
                int linkMarker = name.IndexOf(" -> ", StringComparison.Ordinal);
                if (linkMarker > 0)
                {
                    name = name.Substring(0, linkMarker);
                }
                if (name == "." || name == "..")
                {
                    continue;
                }

                FileEntry entry = new FileEntry();
                entry.Name = name;
                entry.Permission = perm;
                entry.Type = perm.StartsWith("d") ? "目录" : "文件";
                entry.SizeBytes = ParseSizeBytes(size);
                entry.Size = FormatSize(entry.SizeBytes);
                entry.Modified = modified;
                entry.FullPath = CombineRemotePath(basePath, name);
                if (entry.Type == "目录" && !entry.FullPath.EndsWith("/"))
                {
                    entry.FullPath = entry.FullPath + "/";
                }
                entries.Add(entry);
            }
            return entries;
        }

        private int ResolveLsNameStart(string[] parts)
        {
            if (parts.Length >= 8 && IsIsoDate(parts[5]))
            {
                return 7;
            }
            if (parts.Length >= 9 && IsMonthName(parts[5]))
            {
                return 8;
            }
            return Math.Min(parts.Length - 1, 7);
        }

        private string ResolveLsModified(string[] parts, int nameStart)
        {
            if (nameStart == 7 && parts.Length > 6)
            {
                return parts[5] + " " + parts[6];
            }
            if (nameStart == 8 && parts.Length > 7)
            {
                return parts[5] + " " + parts[6] + " " + parts[7];
            }
            return "";
        }

        private bool IsIsoDate(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length < 8)
            {
                return false;
            }
            return value.IndexOf('-') > 0;
        }

        private bool IsMonthName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string[] months = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            for (int i = 0; i < months.Length; i++)
            {
                if (String.Equals(value, months[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string JoinParts(string[] parts, int start)
        {
            if (parts == null || parts.Length == 0)
            {
                return "";
            }
            if (start < 0)
            {
                start = 0;
            }
            if (start >= parts.Length)
            {
                start = parts.Length - 1;
            }
            StringBuilder builder = new StringBuilder(parts[start]);
            for (int i = start + 1; i < parts.Length; i++)
            {
                builder.Append(' ');
                builder.Append(parts[i]);
            }
            return builder.ToString();
        }

        private void GoParentDirectory()
        {
            string path = remotePathTextBox.Text.TrimEnd('/');
            if (String.IsNullOrEmpty(path) || path == "/")
            {
                remotePathTextBox.Text = "/";
            }
            else
            {
                int idx = path.LastIndexOf('/');
                string parent = idx <= 0 ? "/" : path.Substring(0, idx);
                if (parent != "/" && !parent.EndsWith("/"))
                {
                    parent = parent + "/";
                }
                remotePathTextBox.Text = parent;
            }
            RefreshFiles();
        }

        private FileEntry SelectedFileEntry()
        {
            return SelectedFileEntry(true);
        }

        private FileEntry SelectedFileEntry(bool showMessage)
        {
            if (filesGrid.SelectedRows.Count == 0)
            {
                if (showMessage)
                {
                    MessageBox.Show(this, "请先选择一个文件或目录。", "未选择文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return null;
            }
            return filesGrid.SelectedRows[0].Tag as FileEntry;
        }

        private void RenderFiles()
        {
            filesGrid.Rows.Clear();
            List<FileEntry> view = new List<FileEntry>(currentFileEntries);
            view.Sort(CompareFileEntries);
            for (int i = 0; i < view.Count; i++)
            {
                FileEntry entry = view[i];
                int row = filesGrid.Rows.Add(entry.Name, entry.Type, entry.Size, entry.Modified);
                filesGrid.Rows[row].Tag = entry;
            }
            MarkCurrentSortGlyph();
            AdjustFilesGridColumns();
            ApplyFileTailEllipsis();
        }

        private void AdjustFilesGridColumns()
        {
            if (filesGrid == null || filesGrid.Columns.Count == 0 || filesGrid.ClientSize.Width <= 0)
            {
                return;
            }

            int typeWidth = MeasureGridColumnWidth(filesGrid, "Type", GetLongestFileValue("Type"), 18);
            int sizeWidth = MeasureGridColumnWidth(filesGrid, "Size", GetLongestFileValue("Size"), 18);
            int available = Math.Max(120, filesGrid.ClientSize.Width - typeWidth - sizeWidth - GetGridScrollbarAllowance(filesGrid) - 4);
            int nameWidth = Math.Max(140, (int)(available * 0.6));
            int modifiedWidth = Math.Max(100, available - nameWidth);

            filesGrid.Columns["Name"].Width = nameWidth;
            filesGrid.Columns["Type"].Width = typeWidth;
            filesGrid.Columns["Size"].Width = sizeWidth;
            filesGrid.Columns["Modified"].Width = modifiedWidth;
        }

        private string GetLongestFileValue(string columnName)
        {
            string longest = GetFileColumnHeader(columnName);
            for (int i = 0; i < currentFileEntries.Count; i++)
            {
                string value = GetFileCellFullText(currentFileEntries[i], columnName);
                if (!String.IsNullOrEmpty(value) && value.Length > longest.Length)
                {
                    longest = value;
                }
            }
            return longest;
        }

        private void ApplyFileTailEllipsis()
        {
            if (filesGrid == null || filesGrid.Columns.Count == 0)
            {
                return;
            }

            for (int rowIndex = 0; rowIndex < filesGrid.Rows.Count; rowIndex++)
            {
                DataGridViewRow row = filesGrid.Rows[rowIndex];
                FileEntry entry = row.Tag as FileEntry;
                if (entry == null)
                {
                    continue;
                }

                for (int columnIndex = 0; columnIndex < filesGrid.Columns.Count; columnIndex++)
                {
                    DataGridViewColumn column = filesGrid.Columns[columnIndex];
                    string fullText = GetFileCellFullText(entry, column.Name);
                    DataGridViewCell cell = row.Cells[columnIndex];
                    Font font = cell.InheritedStyle.Font == null ? filesGrid.Font : cell.InheritedStyle.Font;
                    cell.Value = TailEllipsisByPixels(fullText, font, Math.Max(16, column.Width - 12));
                    cell.ToolTipText = fullText;
                }
            }
        }

        private string GetFileCellFullText(FileEntry entry, string columnName)
        {
            if (columnName == "Name")
            {
                return entry.Name;
            }
            if (columnName == "Type")
            {
                return entry.Type;
            }
            if (columnName == "Size")
            {
                return entry.Size;
            }
            if (columnName == "Modified")
            {
                return entry.Modified;
            }
            return "";
        }

        private int CompareFileEntries(FileEntry left, FileEntry right)
        {
            int result;
            if (fileSortColumn == "Size")
            {
                result = left.SizeBytes.CompareTo(right.SizeBytes);
            }
            else if (fileSortColumn == "Type")
            {
                result = String.Compare(left.Type, right.Type, StringComparison.OrdinalIgnoreCase);
            }
            else if (fileSortColumn == "Modified")
            {
                result = String.Compare(left.Modified, right.Modified, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                result = String.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (left.Type != right.Type && fileSortColumn == "Name")
            {
                result = left.Type == "目录" ? -1 : 1;
            }
            return fileSortAscending ? result : -result;
        }

        private void MarkCurrentSortGlyph()
        {
            foreach (DataGridViewColumn column in filesGrid.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
                column.HeaderText = GetFileColumnHeader(column.Name);
            }
            if (filesGrid.Columns.Contains(fileSortColumn))
            {
                filesGrid.Columns[fileSortColumn].HeaderText = GetFileColumnHeader(fileSortColumn) + (fileSortAscending ? " ↓" : " ↑");
            }
        }

        private string GetFileColumnHeader(string columnName)
        {
            if (columnName == "Name")
            {
                return "名称";
            }
            if (columnName == "Type")
            {
                return "类型";
            }
            if (columnName == "Size")
            {
                return "大小";
            }
            if (columnName == "Modified")
            {
                return "修改时间";
            }
            return columnName;
        }

        private long ParseSizeBytes(string value)
        {
            long size;
            return Int64.TryParse(value, out size) ? size : 0L;
        }

        private string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return "-";
            }

            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1)
            {
                size /= 1024.0;
                idx++;
            }
            return idx == 0 ? size.ToString("0") + units[idx] : size.ToString("0.0") + units[idx];
        }

        private string TailEllipsis(string text, int maxChars)
        {
            if (String.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text;
            }
            if (maxChars <= 1)
            {
                return "…";
            }
            return "…" + text.Substring(text.Length - (maxChars - 1));
        }

        private string TailEllipsisByPixels(string text, Font font, int maxWidth)
        {
            if (String.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                return text;
            }
            if (TextRenderer.MeasureText(text, font).Width <= maxWidth)
            {
                return text;
            }

            string marker = "…";
            if (TextRenderer.MeasureText(marker, font).Width > maxWidth)
            {
                return marker;
            }

            int low = 0;
            int high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                string candidate = marker + text.Substring(text.Length - mid);
                if (TextRenderer.MeasureText(candidate, font).Width <= maxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return marker + text.Substring(text.Length - best);
        }

        private string PrettyPrintJson(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
            {
                return "{}";
            }

            StringBuilder builder = new StringBuilder();
            bool inString = false;
            int indent = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                    builder.Append(c);
                    continue;
                }

                if (inString)
                {
                    builder.Append(c);
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    builder.Append(c);
                    builder.AppendLine();
                    indent++;
                    builder.Append(new string(' ', indent * 2));
                }
                else if (c == '}' || c == ']')
                {
                    builder.AppendLine();
                    indent = Math.Max(0, indent - 1);
                    builder.Append(new string(' ', indent * 2));
                    builder.Append(c);
                }
                else if (c == ',')
                {
                    builder.Append(c);
                    builder.AppendLine();
                    builder.Append(new string(' ', indent * 2));
                }
                else if (c == ':')
                {
                    builder.Append(": ");
                }
                else if (!Char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        private bool IsEditableTextFile(string name)
        {
            string ext = Path.GetExtension(name);
            if (String.IsNullOrWhiteSpace(ext))
            {
                return false;
            }
            string lower = ext.ToLowerInvariant();
            return lower == ".txt"
                || lower == ".log"
                || lower == ".xml"
                || lower == ".json"
                || lower == ".properties"
                || lower == ".ini"
                || lower == ".csv"
                || lower == ".md";
        }

        private void OpenSelectedFile()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null || entry.Type != "文件")
            {
                return;
            }

            Directory.CreateDirectory(config.DownloadDir);
            string tempDir = Path.Combine(config.DownloadDir, "_open", SanitizeFileName(serial));
            Directory.CreateDirectory(tempDir);
            string local = Path.Combine(tempDir, SanitizeFileName(entry.Name));
            AdbFileTransferItem item = CreateDownloadTransfer(serial, entry.FullPath, local, entry.Name, entry.SizeBytes);
            StartAdbFileTransferBatchAsync(new List<AdbFileTransferItem> { item }, delegate(bool success)
            {
                if (!success || !File.Exists(local))
                {
                    AppendOutput("打开失败，未能下载文件: " + entry.FullPath);
                    return;
                }

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(local);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    AppendOutput("打开本地文件失败: " + ex.Message);
                }
            });
        }

        private void CopySelectedFileEntry()
        {
            FileEntry entry = SelectedFileEntry();
            if (entry == null)
            {
                return;
            }
            fileClipboardEntry = entry;
            fileClipboardOperation = "copy";
            AppendOutput("已复制: " + entry.FullPath);
        }

        private void CutSelectedFileEntry()
        {
            FileEntry entry = SelectedFileEntry();
            if (entry == null)
            {
                return;
            }
            fileClipboardEntry = entry;
            fileClipboardOperation = "cut";
            AppendOutput("已剪切: " + entry.FullPath);
        }

        private void PasteFileEntry()
        {
            string serial = SelectedSerial();
            if (serial == null || fileClipboardEntry == null || String.IsNullOrWhiteSpace(fileClipboardOperation))
            {
                return;
            }

            FileEntry targetEntry = SelectedFileEntry(false);
            string targetDir = targetEntry != null && targetEntry.Type == "目录" ? targetEntry.FullPath : remotePathTextBox.Text.Trim();
            if (!targetDir.EndsWith("/"))
            {
                targetDir += "/";
            }

            string source = fileClipboardEntry.FullPath.TrimEnd('/');
            string dest = CombineRemotePath(targetDir, fileClipboardEntry.Name);
            string command = fileClipboardOperation == "copy"
                ? (fileClipboardEntry.Type == "目录" ? "cp -r " : "cp ") + ShellQuote(source) + " " + ShellQuote(dest)
                : "mv " + ShellQuote(source) + " " + ShellQuote(dest);

            string clipboardOperation = fileClipboardOperation;
            RunAdbShell(serial, command, delegate(bool success)
            {
                if (success && clipboardOperation == "cut")
                {
                    fileClipboardEntry = null;
                    fileClipboardOperation = null;
                }
                RefreshFiles();
            });
        }

        private void DownloadSelectedFile()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null)
            {
                return;
            }

            Directory.CreateDirectory(config.DownloadDir);
            string target = ResolveDownloadedFilePath(serial, entry);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            AdbFileTransferItem item = CreateDownloadTransfer(serial, entry.FullPath, target, entry.Name, entry.SizeBytes);
            StartAdbFileTransferBatchAsync(new List<AdbFileTransferItem> { item }, delegate(bool success)
            {
                if (success)
                {
                    AppendOutput("下载目标: " + target);
                }
            });
        }

        private void OpenDownloadedFileDirectory()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null)
            {
                return;
            }

            string target = ResolveDownloadedFilePath(serial, entry);
            if (!IsDownloadedFileComplete(entry, target))
            {
                AppendOutput("本地文件尚未下载完成: " + entry.FullPath);
                return;
            }

            string dir = Path.GetDirectoryName(target);
            if (String.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                AppendOutput("本地目录不存在: " + dir);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(dir);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppendOutput("打开本地目录失败: " + ex.Message);
            }
        }

        private bool IsDownloadedFileComplete(FileEntry entry)
        {
            string serial = SelectedSerialOrEmpty();
            if (String.IsNullOrWhiteSpace(serial) || entry == null)
            {
                return false;
            }

            return IsDownloadedFileComplete(entry, ResolveDownloadedFilePath(serial, entry));
        }

        private bool IsDownloadedFileComplete(FileEntry entry, string localPath)
        {
            if (entry == null || entry.Type != "文件" || String.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return false;
            }

            if (entry.SizeBytes <= 0)
            {
                return true;
            }

            try
            {
                return new FileInfo(localPath).Length >= entry.SizeBytes;
            }
            catch
            {
                return false;
            }
        }

        private string ResolveDownloadedFilePath(string serial, FileEntry entry)
        {
            string fileName = entry == null ? "" : (entry.Name ?? "");
            return Path.Combine(config.DownloadDir, SanitizeFileName(serial), fileName);
        }

        private void UploadFile()
        {
            UploadToSelectedFileEntry();
        }

        private void UploadToSelectedFileEntry()
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string[] paths = PickUploadPaths();
            if (paths != null && paths.Length > 0)
            {
                FileEntry targetEntry = SelectedFileEntry(false);
                string targetDir = ResolveUploadTargetDirectory(targetEntry);
                UploadLocalPaths(serial, paths, targetDir);
            }
        }

        private string[] PickUploadPaths()
        {
            DialogResult choice = MessageBox.Show(this, "选择“是”上传文件，选择“否”上传文件夹。", "上传类型", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要上传到设备的文件";
                    dialog.Multiselect = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        return dialog.FileNames;
                    }
                }
            }
            else if (choice == DialogResult.No)
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择要上传到设备的文件夹";
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        return new string[] { dialog.SelectedPath };
                    }
                }
            }

            return null;
        }

        private void UploadLocalPaths(string serial, string[] localPaths, string targetDir)
        {
            if (localPaths == null || localPaths.Length == 0)
            {
                return;
            }

            if (!targetDir.EndsWith("/"))
            {
                targetDir += "/";
            }

            List<AdbFileTransferItem> transfers = new List<AdbFileTransferItem>();
            for (int i = 0; i < localPaths.Length; i++)
            {
                string target = CombineRemotePath(targetDir, Path.GetFileName(localPaths[i]));
                transfers.Add(CreateUploadTransfer(serial, localPaths[i], target));
            }
            StartAdbFileTransferBatchAsync(transfers, delegate(bool success)
            {
                if (success)
                {
                    RefreshFiles();
                }
            });
        }

        private string ResolveUploadTargetDirectory(FileEntry targetEntry)
        {
            if (targetEntry == null)
            {
                return remotePathTextBox.Text.Trim();
            }
            if (targetEntry.Type == "目录")
            {
                return targetEntry.FullPath;
            }
            string filePath = targetEntry.FullPath.TrimEnd('/');
            int idx = filePath.LastIndexOf('/');
            return idx <= 0 ? "/" : filePath.Substring(0, idx + 1);
        }

        private string ResolveFileActionTargetDirectory(FileEntry targetEntry)
        {
            if (targetEntry != null && targetEntry.Type == "目录")
            {
                return targetEntry.FullPath;
            }
            return remotePathTextBox.Text.Trim();
        }

        private void CreateRemoteFileEntry(bool directory)
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string label = directory ? "新建文件夹" : "新建文件";
            string defaultName = directory ? "new_folder" : "new_file.txt";
            string name = PromptForText(label, "名称", defaultName);
            if (String.IsNullOrWhiteSpace(name))
            {
                return;
            }
            if (name.IndexOf('/') >= 0)
            {
                MessageBox.Show(this, "名称不能包含 / 。", "名称无效", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            FileEntry targetEntry = SelectedFileEntry(false);
            string targetDir = ResolveFileActionTargetDirectory(targetEntry);
            if (!targetDir.EndsWith("/"))
            {
                targetDir += "/";
            }

            string target = CombineRemotePath(targetDir, name);
            string command = directory ? "mkdir -p " + ShellQuote(target) : "touch " + ShellQuote(target);
            RunAdbShell(serial, command, delegate(bool success) { RefreshFiles(); });
        }

        private void DeleteSelectedFileEntry()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null)
            {
                return;
            }

            if (MessageBox.Show(this, "确认删除「" + entry.Name + "」？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            string command = entry.Type == "目录" ? "rm -rf " : "rm -f ";
            RunAdbShell(serial, command + ShellQuote(entry.FullPath.TrimEnd('/')), delegate(bool success) { RefreshFiles(); });
        }

        private void RenameSelectedFileEntry()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null)
            {
                return;
            }

            string newName = PromptForText("重命名", "新名称", entry.Name);
            if (String.IsNullOrWhiteSpace(newName) || String.Equals(newName, entry.Name, StringComparison.Ordinal))
            {
                return;
            }

            string filePath = entry.FullPath.TrimEnd('/');
            int idx = filePath.LastIndexOf('/');
            string parent = idx <= 0 ? "/" : filePath.Substring(0, idx + 1);
            string target = CombineRemotePath(parent, newName);
            RunAdbShell(serial, "mv " + ShellQuote(filePath) + " " + ShellQuote(target), delegate(bool success) { RefreshFiles(); });
        }

        private void ShowSelectedFileProperties()
        {
            FileEntry entry = SelectedFileEntry();
            if (entry == null)
            {
                return;
            }

            MessageBox.Show(
                this,
                "名称: " + entry.Name + Environment.NewLine +
                "类型: " + entry.Type + Environment.NewLine +
                "大小: " + entry.Size + Environment.NewLine +
                "修改时间: " + entry.Modified + Environment.NewLine +
                "权限: " + entry.Permission + Environment.NewLine +
                "路径: " + entry.FullPath,
                "属性",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void EditSelectedTextFile()
        {
            string serial = SelectedSerial();
            FileEntry entry = SelectedFileEntry();
            if (serial == null || entry == null || entry.Type != "文件")
            {
                return;
            }

            Directory.CreateDirectory(config.DownloadDir);
            string tempDir = Path.Combine(config.DownloadDir, "_edit", SanitizeFileName(serial));
            Directory.CreateDirectory(tempDir);
            string local = Path.Combine(tempDir, SanitizeFileName(entry.Name));
            AdbFileTransferItem download = CreateDownloadTransfer(serial, entry.FullPath, local, entry.Name, entry.SizeBytes);
            StartAdbFileTransferBatchAsync(new List<AdbFileTransferItem> { download }, delegate(bool success)
            {
                if (!success || !File.Exists(local))
                {
                    AppendOutput("编辑失败，未能下载文件: " + entry.FullPath);
                    return;
                }

                try
                {
                    using (TextEditForm editor = new TextEditForm(local))
                    {
                        if (editor.ShowDialog(this) == DialogResult.OK)
                        {
                            AdbFileTransferItem upload = CreateUploadTransfer(serial, local, entry.FullPath);
                            StartAdbFileTransferBatchAsync(new List<AdbFileTransferItem> { upload }, delegate(bool uploadSuccess)
                            {
                                if (uploadSuccess)
                                {
                                    RefreshFiles();
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendOutput("编辑文件失败: " + ex.Message);
                }
            });
        }

        private void CaptureAndViewScreenshot()
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            if (!Directory.Exists(config.ScreenshotDir))
            {
                Directory.CreateDirectory(config.ScreenshotDir);
            }

            string safeSerial = SanitizeFileName(serial);
            string file = Path.Combine(config.ScreenshotDir, safeSerial + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            AppendOutput("正在截取设备屏幕: " + serial);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool success = TakeScreenshot(serial, adb, file);
                BeginInvokeIfAvailable(delegate
                {
                    if (success && File.Exists(file))
                    {
                        RefreshScreenshots();
                        SelectScreenshot(file);
                        mainTabs.SelectedTab = FindTabByText(mainTabs, "截图查看");
                    }
                });
            });
        }

        private bool TakeScreenshot(string serial, string adb, string file)
        {
            string args = "-s " + QuoteArg(serial) + " exec-out screencap -p";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(adb, args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process process = Process.Start(psi))
                {
                    using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                    {
                        process.StandardOutput.BaseStream.CopyTo(fileStream);
                    }
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode == 0 && File.Exists(file) && new FileInfo(file).Length > 0)
                    {
                        AppendOutput("截图已保存: " + file);
                        return true;
                    }
                    AppendOutput("截图失败: " + error);
                }
            }
            catch (Exception ex)
            {
                AppendOutput("截图异常: " + ex.Message);
            }
            return false;
        }

        private void RefreshScreenshots()
        {
            if (screenshotListBox == null)
            {
                return;
            }

            Directory.CreateDirectory(config.ScreenshotDir);
            screenshotListBox.Items.Clear();
            string[] files = Directory.GetFiles(config.ScreenshotDir, "*.png");
            Array.Sort(files);
            Array.Reverse(files);
            int maxNameWidth = 120;
            using (Graphics graphics = screenshotListBox.CreateGraphics())
            {
                for (int i = 0; i < files.Length; i++)
                {
                    int width = (int)graphics.MeasureString(Path.GetFileName(files[i]), screenshotListBox.Font).Width;
                    if (width > maxNameWidth)
                    {
                        maxNameWidth = width;
                    }
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                screenshotListBox.Items.Add(new ScreenshotItem(files[i]));
            }
            screenshotListBox.Invalidate();
            SplitContainer split = screenshotListBox.FindForm() == null ? null : screenshotListBox.Parent.Parent.Parent as SplitContainer;
            if (split != null)
            {
                int desired = maxNameWidth + 30;
                int max = Math.Max(120, split.Width - 180);
                if (desired > max)
                {
                    desired = max;
                }
                if (desired < 120)
                {
                    desired = 120;
                }
                ApplySplitterDistance(split, desired);
            }
        }

        private void OnScreenshotListBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= screenshotListBox.Items.Count)
            {
                return;
            }

            ScreenshotItem item = screenshotListBox.Items[e.Index] as ScreenshotItem;
            string text = item == null ? Convert.ToString(screenshotListBox.Items[e.Index]) : Path.GetFileName(item.FullPath);
            string drawText = TailEllipsisByPixels(text, e.Font, Math.Max(16, e.Bounds.Width - 8));
            TextRenderer.DrawText(e.Graphics, drawText, e.Font, e.Bounds, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private void OnScreenshotListBoxMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int index = screenshotListBox.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                screenshotListBox.SelectedIndex = index;
            }
            else
            {
                screenshotListBox.ClearSelected();
            }
        }

        private void OnScreenshotsMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasSelection = screenshotListBox != null && screenshotListBox.SelectedItem != null;
            for (int i = 0; i < screenshotsMenu.Items.Count; i++)
            {
                screenshotsMenu.Items[i].Enabled = hasSelection;
            }
        }

        private void SelectScreenshot(string file)
        {
            for (int i = 0; i < screenshotListBox.Items.Count; i++)
            {
                ScreenshotItem item = screenshotListBox.Items[i] as ScreenshotItem;
                if (item != null && String.Equals(item.FullPath, file, StringComparison.OrdinalIgnoreCase))
                {
                    screenshotListBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private void ShowSelectedScreenshot()
        {
            if (screenshotListBox.SelectedItem == null)
            {
                return;
            }

            ScreenshotItem selected = screenshotListBox.SelectedItem as ScreenshotItem;
            string file = selected == null ? Convert.ToString(screenshotListBox.SelectedItem) : selected.FullPath;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Image image = Image.FromStream(stream);
                    Image old = screenshotPictureBox.Image;
                    screenshotPictureBox.Image = new Bitmap(image);
                    image.Dispose();
                    if (old != null)
                    {
                        old.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                AppendOutput("打开截图失败: " + ex.Message);
            }
        }

        private void OpenScreenshotFolder()
        {
            Directory.CreateDirectory(config.ScreenshotDir);
            Process.Start("explorer.exe", config.ScreenshotDir);
        }

        private ScreenshotItem SelectedScreenshot()
        {
            return screenshotListBox == null ? null : screenshotListBox.SelectedItem as ScreenshotItem;
        }

        private void CopySelectedScreenshot()
        {
            ScreenshotItem item = SelectedScreenshot();
            if (item == null)
            {
                return;
            }

            try
            {
                using (Image image = Image.FromFile(item.FullPath))
                {
                    Clipboard.SetImage(new Bitmap(image));
                }
                AppendOutput("已复制截图: " + item.FullPath);
            }
            catch (Exception ex)
            {
                AppendOutput("复制截图失败: " + ex.Message);
            }
        }

        private void DeleteSelectedScreenshot()
        {
            ScreenshotItem item = SelectedScreenshot();
            if (item == null)
            {
                return;
            }
            if (MessageBox.Show(this, "确认删除截图？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                File.Delete(item.FullPath);
                RefreshScreenshots();
                if (screenshotPictureBox.Image != null)
                {
                    screenshotPictureBox.Image.Dispose();
                    screenshotPictureBox.Image = null;
                }
            }
            catch (Exception ex)
            {
                AppendOutput("删除截图失败: " + ex.Message);
            }
        }

        private void RenameSelectedScreenshot()
        {
            ScreenshotItem item = SelectedScreenshot();
            if (item == null)
            {
                return;
            }
            string oldName = Path.GetFileName(item.FullPath);
            string newName = PromptForText("重命名截图", "新名称", oldName);
            if (String.IsNullOrWhiteSpace(newName) || String.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string target = Path.Combine(Path.GetDirectoryName(item.FullPath), newName);
            try
            {
                File.Move(item.FullPath, target);
                RefreshScreenshots();
                SelectScreenshot(target);
            }
            catch (Exception ex)
            {
                AppendOutput("重命名截图失败: " + ex.Message);
            }
        }

        private void CopySelectedScreenshotPath()
        {
            ScreenshotItem item = SelectedScreenshot();
            if (item == null)
            {
                return;
            }
            Clipboard.SetText(item.FullPath);
            AppendOutput("已复制绝对路径: " + item.FullPath);
        }

        private void ShowSelectedScreenshotProperties()
        {
            ScreenshotItem item = SelectedScreenshot();
            if (item == null)
            {
                return;
            }
            FileInfo info = new FileInfo(item.FullPath);
            MessageBox.Show(
                this,
                "名称: " + info.Name + Environment.NewLine +
                "大小: " + FormatSize(info.Length) + Environment.NewLine +
                "修改时间: " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "路径: " + info.FullName,
                "截图属性",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RefreshApps()
        {
            int requestId = unchecked(++appRefreshRequestId);
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }
            UpdateSelectedDeviceHeader();

            string filter = Convert.ToString(appFilterComboBox.SelectedItem);
            string flag = "";
            if (filter == "用户应用")
            {
                flag = "-3";
            }
            else if (filter == "系统应用")
            {
                flag = "-s";
            }

            AppendOutput("正在读取应用列表: " + filter);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult listResult = RunAdbShellCapture(serial, "pm list packages -f " + flag);
                if (requestId != appRefreshRequestId)
                {
                    return;
                }

                ProcessResult disabledListResult = RunAdbShellCapture(serial, "pm list packages -d");
                if (requestId != appRefreshRequestId)
                {
                    return;
                }

                HashSet<string> disabledPackages = ParsePackageNameSet(disabledListResult.Output);
                List<AppEntry> apps = ParseApps(listResult.Output, filter, disabledPackages);
                for (int i = 0; i < apps.Count; i++)
                {
                    if (requestId != appRefreshRequestId)
                    {
                        return;
                    }
                    FillAppVersion(serial, apps[i]);
                }

                BeginInvokeIfAvailable(delegate
                {
                    if (requestId != appRefreshRequestId ||
                        !String.Equals(serial, SelectedSerialOrEmpty(), StringComparison.OrdinalIgnoreCase) ||
                        !String.Equals(filter, Convert.ToString(appFilterComboBox.SelectedItem), StringComparison.Ordinal))
                    {
                        return;
                    }

                    appsGrid.Rows.Clear();
                    for (int i = 0; i < apps.Count; i++)
                    {
                        int row = appsGrid.Rows.Add(apps[i].Name, apps[i].PackageName, apps[i].VersionName, apps[i].Type);
                        appsGrid.Rows[row].Tag = apps[i];
                    }
                    AdjustAppsGridColumns();
                    ApplyAppTailEllipsis();
                    AppendOutput("应用列表已刷新: " + apps.Count);
                });
            });
        }

        private List<AppEntry> ParseApps(string output, string filter, HashSet<string> disabledPackages)
        {
            List<AppEntry> apps = new List<AppEntry>();
            if (String.IsNullOrWhiteSpace(output))
            {
                return apps;
            }

            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("package:"))
                {
                    continue;
                }

                string value = line.Substring("package:".Length);
                int eq = value.LastIndexOf('=');
                string apk = "";
                string pkg = value;
                if (eq > 0)
                {
                    apk = value.Substring(0, eq);
                    pkg = value.Substring(eq + 1);
                }

                string type = filter == "系统应用" ? "系统应用" : (filter == "用户应用" ? "用户应用" : GuessAppType(apk));

                apps.Add(new AppEntry
                {
                    Name = GetAppDisplayName(pkg),
                    PackageName = pkg,
                    ApkPath = apk,
                    Type = type,
                    IsDisabled = disabledPackages != null && disabledPackages.Contains(pkg)
                });
            }
            return apps;
        }

        private HashSet<string> ParsePackageNameSet(string output)
        {
            HashSet<string> packages = new HashSet<string>(StringComparer.Ordinal);
            if (String.IsNullOrWhiteSpace(output))
            {
                return packages;
            }

            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("package:"))
                {
                    continue;
                }

                string value = line.Substring("package:".Length).Trim();
                int eq = value.LastIndexOf('=');
                if (eq >= 0 && eq < value.Length - 1)
                {
                    value = value.Substring(eq + 1).Trim();
                }

                if (!String.IsNullOrWhiteSpace(value))
                {
                    packages.Add(value);
                }
            }
            return packages;
        }

        private string GetAppDisplayName(string packageName)
        {
            if (String.IsNullOrWhiteSpace(packageName))
            {
                return "";
            }
            int index = packageName.LastIndexOf('.');
            return index >= 0 && index < packageName.Length - 1 ? packageName.Substring(index + 1) : packageName;
        }

        private void ApplyAppTailEllipsis()
        {
            if (appsGrid == null || appsGrid.Columns.Count == 0)
            {
                return;
            }

            for (int rowIndex = 0; rowIndex < appsGrid.Rows.Count; rowIndex++)
            {
                DataGridViewRow row = appsGrid.Rows[rowIndex];
                AppEntry entry = row.Tag as AppEntry;
                if (entry == null)
                {
                    continue;
                }

                for (int columnIndex = 0; columnIndex < appsGrid.Columns.Count; columnIndex++)
                {
                    DataGridViewColumn column = appsGrid.Columns[columnIndex];
                    string fullText = GetAppCellFullText(entry, column.Name);
                    DataGridViewCell cell = row.Cells[columnIndex];
                    Font font = cell.InheritedStyle.Font == null ? appsGrid.Font : cell.InheritedStyle.Font;
                    cell.Value = TailEllipsisByPixels(fullText, font, Math.Max(16, column.Width - 12));
                    cell.ToolTipText = fullText;
                }
            }
        }

        private void AdjustAppsGridColumns()
        {
            if (appsGrid == null || appsGrid.Columns.Count == 0 || appsGrid.ClientSize.Width <= 0)
            {
                return;
            }

            int nameWidth = MeasureGridColumnWidth(appsGrid, "Name", GetLongestAppValue("Name"), 12);
            int versionNameWidth = MeasureGridColumnWidth(appsGrid, "VersionName", GetLongestAppValue("VersionName"), 18);
            int typeWidth = MeasureGridColumnWidth(appsGrid, "Type", GetLongestAppValue("Type"), 18);
            int available = Math.Max(120, appsGrid.ClientSize.Width - nameWidth - versionNameWidth - typeWidth - GetGridScrollbarAllowance(appsGrid) - 4);

            appsGrid.Columns["Name"].Width = nameWidth;
            appsGrid.Columns["PackageName"].Width = available;
            appsGrid.Columns["VersionName"].Width = versionNameWidth;
            appsGrid.Columns["Type"].Width = typeWidth;
        }

        private string GetLongestAppValue(string columnName)
        {
            string longest = columnName == "VersionName" ? "版本号" : columnName == "PackageName" ? "包名" : columnName == "Type" ? "类型" : "名称";
            for (int i = 0; i < appsGrid.Rows.Count; i++)
            {
                AppEntry entry = appsGrid.Rows[i].Tag as AppEntry;
                if (entry == null)
                {
                    continue;
                }
                string value = GetAppCellFullText(entry, columnName);
                if (!String.IsNullOrEmpty(value) && value.Length > longest.Length)
                {
                    longest = value;
                }
            }
            return longest;
        }

        private string GetAppCellFullText(AppEntry entry, string columnName)
        {
            if (columnName == "Name")
            {
                return GetAppListDisplayName(entry);
            }
            if (columnName == "PackageName")
            {
                return entry.PackageName;
            }
            if (columnName == "VersionName")
            {
                return entry.VersionName;
            }
            if (columnName == "Type")
            {
                return entry.Type;
            }
            return "";
        }

        private string GetAppListDisplayName(AppEntry entry)
        {
            if (entry == null)
            {
                return "";
            }
            return entry.IsDisabled ? "*" + entry.Name : entry.Name;
        }

        private string GuessAppType(string apkPath)
        {
            if (String.IsNullOrWhiteSpace(apkPath))
            {
                return "未知";
            }
            string lower = apkPath.ToLowerInvariant();
            if (lower.StartsWith("/data/"))
            {
                return "用户应用";
            }
            return "系统应用";
        }

        private void FillAppVersion(string serial, AppEntry app)
        {
            ProcessResult result = RunAdbShellCapture(serial, "dumpsys package " + app.PackageName);
            string[] lines = result.Output.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("versionName="))
                {
                    app.VersionName = line.Substring("versionName=".Length);
                }
                else if (line.IndexOf("versionCode=") >= 0)
                {
                    int idx = line.IndexOf("versionCode=");
                    string rest = line.Substring(idx + "versionCode=".Length);
                    string[] parts = SplitWhitespace(rest);
                    if (parts.Length > 0)
                    {
                        app.VersionCode = parts[0];
                    }
                }
            }
        }

        private AppEntry SelectedApp()
        {
            AppEntry app = CurrentSelectedApp();
            if (app == null)
            {
                MessageBox.Show(this, "请先选择一个应用。", "未选择应用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return app;
        }

        private AppEntry CurrentSelectedApp()
        {
            if (appsGrid == null || appsGrid.SelectedRows.Count == 0)
            {
                return null;
            }
            return appsGrid.SelectedRows[0].Tag as AppEntry;
        }

        private void PickAndInstallApps()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要安装到设备的 APK";
                dialog.Filter = "APK 文件 (*.apk)|*.apk|All files (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    InstallApkFiles(dialog.FileNames);
                }
            }
        }

        private void InstallApkFiles(string[] paths)
        {
            string serial = SelectedSerial();
            if (serial == null)
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            List<string> apkPaths = new List<string>();
            List<string> ignoredPaths = new List<string>();
            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    {
                        ignoredPaths.Add(path ?? "");
                        continue;
                    }

                    if (String.Equals(Path.GetExtension(path), ".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        apkPaths.Add(path);
                    }
                    else
                    {
                        ignoredPaths.Add(path);
                    }
                }
            }

            if (apkPaths.Count == 0)
            {
                MessageBox.Show(this, "请选择至少一个 APK 文件。", "未找到 APK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ignoredPaths.Count > 0)
            {
                AppendOutput("已忽略非 APK 或不存在的文件: " + ignoredPaths.Count);
            }

            List<ApkInstallItem> installItems = new List<ApkInstallItem>();
            List<AdbFileTransferItem> uploadTransfers = new List<AdbFileTransferItem>();
            string batchId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            for (int i = 0; i < apkPaths.Count; i++)
            {
                string apkPath = apkPaths[i];
                string displayName = Path.GetFileName(apkPath);
                string remotePath = "/data/local/tmp/adbquicktool_install_" + batchId + "_" + (i + 1) + "_" + SanitizeRemoteTempFileName(displayName);
                ApkInstallItem item = new ApkInstallItem
                {
                    LocalPath = apkPath,
                    RemotePath = remotePath,
                    DisplayName = displayName
                };
                installItems.Add(item);
                uploadTransfers.Add(CreateUploadTransfer(serial, apkPath, remotePath));
            }

            AppendOutput("准备上传并安装 APK 到设备 " + serial + ": " + apkPaths.Count + " 个");
            StartAdbFileTransferBatchAsync(uploadTransfers, delegate(bool uploadSuccess)
            {
                if (!uploadSuccess)
                {
                    AppendOutput("APK 上传失败，已取消安装。");
                    CleanupUploadedApkFilesAsync(serial, adb, installItems);
                    return;
                }

                InstallUploadedApkFilesAsync(serial, adb, installItems);
            });
        }

        private void InstallUploadedApkFilesAsync(string serial, string adb, List<ApkInstallItem> installItems)
        {
            if (installItems == null || installItems.Count == 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool anySuccess = false;
                for (int i = 0; i < installItems.Count; i++)
                {
                    ApkInstallItem item = installItems[i];
                    string installCommand = "pm install -r " + ShellQuote(item.RemotePath);
                    string args = "-s " + QuoteArg(serial) + " shell " + installCommand;
                    ProcessResult result = RunProcess(adb, args, null, true);
                    bool installSuccess = IsAdbInstallSuccess(result);
                    if (installSuccess)
                    {
                        anySuccess = true;
                    }

                    string cleanupArgs = "-s " + QuoteArg(serial) + " shell rm -f " + ShellQuote(item.RemotePath);
                    ProcessResult cleanupResult = RunProcess(adb, cleanupArgs, null, false);
                    string displayName = item.DisplayName;
                    string argsForUi = args;
                    string cleanupArgsForUi = cleanupArgs;
                    ProcessResult resultForUi = result;
                    ProcessResult cleanupResultForUi = cleanupResult;
                    bool installSuccessForUi = installSuccess;
                    BeginInvokeIfAvailable(delegate
                    {
                        AppendOutput("> " + adb + " " + argsForUi);
                        AppendProcessResult(resultForUi);
                        if (cleanupResultForUi == null || cleanupResultForUi.ExitCode != 0)
                        {
                            AppendOutput("> " + adb + " " + cleanupArgsForUi);
                            AppendProcessResult(cleanupResultForUi);
                        }
                        AppendOutput((installSuccessForUi ? "安装完成: " : "安装失败: ") + displayName);
                    });
                }

                BeginInvokeIfAvailable(delegate
                {
                    if (anySuccess)
                    {
                        RefreshApps();
                    }
                });
            });
        }

        private void CleanupUploadedApkFilesAsync(string serial, string adb, List<ApkInstallItem> installItems)
        {
            if (installItems == null || installItems.Count == 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < installItems.Count; i++)
                {
                    ApkInstallItem item = installItems[i];
                    if (item == null || String.IsNullOrWhiteSpace(item.RemotePath))
                    {
                        continue;
                    }

                    string args = "-s " + QuoteArg(serial) + " shell rm -f " + ShellQuote(item.RemotePath);
                    RunProcess(adb, args, null, false);
                }
            });
        }

        private string SanitizeRemoteTempFileName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "app.apk";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if ((ch >= 'a' && ch <= 'z') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch == '.' ||
                    ch == '_' ||
                    ch == '-')
                {
                    builder.Append(ch);
                }
                else
                {
                    builder.Append('_');
                }
            }

            string result = builder.ToString().Trim('_');
            return String.IsNullOrWhiteSpace(result) ? "app.apk" : result;
        }

        private void OpenSelectedApp()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }
            RunAdbShell(serial, "monkey -p " + app.PackageName + " -c android.intent.category.LAUNCHER 1");
        }

        private void RestartSelectedApp()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }
            RunAdbShell(serial, "am force-stop " + app.PackageName + " && monkey -p " + app.PackageName + " -c android.intent.category.LAUNCHER 1");
        }

        private void ClearSelectedAppData()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }

            if (MessageBox.Show(this, "确认清除「" + app.PackageName + "」的数据？此操作不可恢复。", "确认清除数据", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                RunAdbShell(serial, "pm clear " + app.PackageName);
            }
        }

        private void KillSelectedApp()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }
            RunAdbShell(serial, "am force-stop " + app.PackageName);
        }

        private void FreezeOrEnableSelectedApp()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }

            string actionText = app.IsDisabled ? "激活应用" : "冻结应用";
            string command = (app.IsDisabled ? "pm enable --user 0 " : "pm disable-user --user 0 ") + ShellQuote(app.PackageName);
            AppendOutput(actionText + ": " + app.PackageName);
            RunAdbShell(serial, command, delegate(bool success) { RefreshApps(); });
        }

        private void UninstallSelectedApp()
        {
            string serial = SelectedSerial();
            AppEntry app = SelectedApp();
            if (serial == null || app == null)
            {
                return;
            }

            if (MessageBox.Show(this, "确认卸载「" + app.PackageName + "」？此操作不可恢复。", "确认卸载应用", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            RunAdbShell(serial, "pm uninstall " + app.PackageName, delegate(bool success) { RefreshApps(); });
        }

        private void CopySelectedAppPackageName()
        {
            AppEntry app = SelectedApp();
            if (app == null)
            {
                return;
            }
            Clipboard.SetText(app.PackageName ?? "");
            AppendOutput("已复制包名: " + app.PackageName);
        }

        private void ShowSelectedAppProperties()
        {
            AppEntry app = SelectedApp();
            if (app == null)
            {
                return;
            }

            MessageBox.Show(
                this,
                "名称: " + app.Name + Environment.NewLine +
                "包名: " + app.PackageName + Environment.NewLine +
                "版本号: " + app.VersionName + Environment.NewLine +
                "版本代码: " + app.VersionCode + Environment.NewLine +
                "类型: " + app.Type + Environment.NewLine +
                "状态: " + (app.IsDisabled ? "冻结" : "正常") + Environment.NewLine +
                "APK 路径: " + app.ApkPath,
                "应用属性",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RunAdbShell(string serial, string command)
        {
            RunAdbShell(serial, command, null);
        }

        private void RunAdbShell(string serial, string command, Action<bool> onCompleted)
        {
            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                if (onCompleted != null)
                {
                    onCompleted(false);
                }
                return;
            }

            string args = "-s " + QuoteArg(serial) + " shell " + command;
            AppendOutput("> " + adb + " " + args);
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProcessResult result = RunProcess(adb, args, null, true);
                bool success = !ShouldReconnectForDeviceCommandResult(result);
                bool reconnectAttempted = false;
                bool reconnectSucceeded = false;
                string connectArguments = null;
                ProcessResult connectResult = null;
                ProcessResult stateResult = null;
                ProcessResult retryResult = null;
                bool reconnectSuppressed = IsDeviceReconnectSuppressed(serial);
                if (!success && !reconnectSuppressed)
                {
                    reconnectAttempted = true;
                    reconnectSucceeded = TryReconnectDeviceCore(adb, serial, out connectArguments, out connectResult, out stateResult);
                    if (reconnectSucceeded)
                    {
                        retryResult = RunProcess(adb, args, null, true);
                        success = !ShouldReconnectForDeviceCommandResult(retryResult);
                    }
                }

                BeginInvokeIfAvailable(delegate
                {
                    AppendProcessResult(result);
                    if (reconnectAttempted)
                    {
                        AppendOutput("设备命令失败，尝试重新连接设备: " + serial);
                        AppendOutput("> " + adb + " " + connectArguments);
                        AppendProcessResult(connectResult);
                    }
                    if (reconnectSucceeded)
                    {
                        MarkDeviceConnected(serial);
                        AppendOutput("> " + adb + " " + args + " [重试]");
                        AppendProcessResult(retryResult);
                    }
                    if (!success && !reconnectSuppressed)
                    {
                        AppendOutput("设备命令仍失败，已刷新设备状态: " + serial);
                        RefreshDevicesIncrementalFromCmd();
                    }
                    if (onCompleted != null)
                    {
                        onCompleted(success);
                    }
                });
            });
        }

        private ProcessResult RunAdbShellCapture(string serial, string command)
        {
            string adb = GetAdbPath();
            string args = "-s " + QuoteArg(serial) + " shell " + command;
            ProcessResult result = RunProcess(adb, args, null, false);
            if (!ShouldReconnectForDeviceCommandResult(result))
            {
                return result;
            }
            if (IsDeviceReconnectSuppressed(serial))
            {
                return result;
            }

            AppendOutput("设备命令失败，尝试重新连接设备: " + serial);
            string connectArguments;
            ProcessResult connectResult;
            ProcessResult stateResult;
            bool reconnectSucceeded = TryReconnectDeviceCore(adb, serial, out connectArguments, out connectResult, out stateResult);
            AppendOutput("> " + adb + " " + connectArguments);
            AppendProcessResult(connectResult);
            if (reconnectSucceeded)
            {
                BeginInvokeIfAvailable(delegate { MarkDeviceConnected(serial); });
                result = RunProcess(adb, args, null, false);
                if (!ShouldReconnectForDeviceCommandResult(result))
                {
                    return result;
                }
            }

            AppendOutput("设备命令仍失败，已刷新设备状态: " + serial);
            BeginInvokeIfAvailable(delegate { RefreshDevicesIncrementalFromCmd(); });
            return result;
        }

        private void RunAdbAndLog(string arguments)
        {
            string adb = GetAdbPath();
            AppendOutput("> " + adb + " " + arguments);
            ProcessResult result = RunProcess(adb, arguments, null, true);
            AppendProcessResult(result);
        }

        private AdbFileTransferItem CreateDownloadTransfer(string serial, string remotePath, string localPath, string displayName, long totalBytes)
        {
            return new AdbFileTransferItem
            {
                Serial = serial,
                Operation = "下载",
                Source = remotePath,
                Target = localPath,
                DisplayName = String.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(localPath) : displayName,
                TotalBytes = Math.Max(0L, totalBytes),
                Arguments = "-s " + QuoteArg(serial) + " pull " + QuoteArg(remotePath) + " " + QuoteArg(localPath)
            };
        }

        private AdbFileTransferItem CreateUploadTransfer(string serial, string localPath, string remotePath)
        {
            return new AdbFileTransferItem
            {
                Serial = serial,
                Operation = "上传",
                Source = localPath,
                Target = remotePath,
                DisplayName = Path.GetFileName(localPath),
                TotalBytes = GetLocalTransferSize(localPath),
                Arguments = "-s " + QuoteArg(serial) + " push " + QuoteArg(localPath) + " " + QuoteArg(remotePath)
            };
        }

        private long GetLocalTransferSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }

                if (Directory.Exists(path))
                {
                    long total = 0L;
                    string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            total += new FileInfo(files[i]).Length;
                        }
                        catch
                        {
                        }
                    }
                    return total;
                }
            }
            catch
            {
            }

            return 0L;
        }

        private void StartAdbFileTransferBatchAsync(List<AdbFileTransferItem> transfers, Action<bool> onCompleted)
        {
            if (transfers == null || transfers.Count == 0)
            {
                return;
            }

            string adb = GetAdbPath();
            if (!ValidateExeOrCommand(adb, "adb.exe"))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref fileTransferInProgressFlag, 1, 0) != 0)
            {
                AppendOutput("文件传输正在进行中，请等待当前上传/下载完成。");
                return;
            }

            string progressKey = Guid.NewGuid().ToString("N");
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool allSuccess = true;
                try
                {
                    for (int i = 0; i < transfers.Count; i++)
                    {
                        AdbFileTransferItem transfer = transfers[i];
                        AdbFileTransferProgress progress = new AdbFileTransferProgress
                        {
                            Key = progressKey,
                            Operation = transfer.Operation,
                            Index = i + 1,
                            Total = transfers.Count,
                            DisplayName = transfer.DisplayName,
                            Percent = -1,
                            TotalBytes = transfer.TotalBytes,
                            TransferredBytes = 0L,
                            LastSampleBytes = 0L,
                            SpeedBytesPerSecond = 0L,
                            LastSampleUtc = DateTime.MinValue,
                            LastUiUpdateUtc = DateTime.MinValue
                        };
                        if (String.Equals(transfer.Operation, "下载", StringComparison.Ordinal))
                        {
                            progress.LocalProgressPath = transfer.Target;
                        }
                        else if (String.Equals(transfer.Operation, "上传", StringComparison.Ordinal) && File.Exists(transfer.Source))
                        {
                            progress.AdbPath = adb;
                            progress.RemoteProgressSerial = transfer.Serial;
                            progress.RemoteProgressPath = transfer.Target;
                        }

                        PrepareAdbFileTransfer(transfer);
                        ReportAdbFileTransferProgress(progress, "准备", true, false);
                        ProcessResult result = RunAdbFileTransferProcess(adb, transfer.Arguments, progress);
                        bool success = result != null && result.ExitCode == 0;
                        ReportAdbFileTransferProgress(progress, success ? "完成" : "失败", true, i == transfers.Count - 1 || !success);

                        if (!success)
                        {
                            allSuccess = false;
                            AppendOutput("> " + adb + " " + transfer.Arguments);
                            AppendProcessResult(result);
                            break;
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref fileTransferInProgressFlag, 0);
                    if (onCompleted != null && !IsDisposed && IsHandleCreated)
                    {
                        try
                        {
                            BeginInvoke((Action)delegate { onCompleted(allSuccess); });
                        }
                        catch
                        {
                        }
                    }
                }
            });
        }

        private void PrepareAdbFileTransfer(AdbFileTransferItem transfer)
        {
            if (transfer == null)
            {
                return;
            }

            if (String.Equals(transfer.Operation, "下载", StringComparison.Ordinal) && transfer.TotalBytes > 0 && !String.IsNullOrWhiteSpace(transfer.Target))
            {
                try
                {
                    string dir = Path.GetDirectoryName(transfer.Target);
                    if (!String.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(transfer.Target))
                    {
                        File.Delete(transfer.Target);
                    }
                }
                catch
                {
                }
            }
        }

        private ProcessResult RunAdbFileTransferProcess(string adb, string arguments, AdbFileTransferProgress progress)
        {
            ProcessResult result = new ProcessResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(adb, arguments);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (Process process = Process.Start(psi))
                {
                    byte[] outputBytes = null;
                    byte[] errorBytes = null;
                    Exception outputException = null;
                    Exception errorException = null;
                    using (ManualResetEvent outputDone = new ManualResetEvent(false))
                    using (ManualResetEvent errorDone = new ManualResetEvent(false))
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                outputBytes = ReadTransferStreamBytes(process.StandardOutput.BaseStream, progress);
                            }
                            catch (Exception ex)
                            {
                                outputException = ex;
                            }
                            finally
                            {
                                outputDone.Set();
                            }
                        });

                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                errorBytes = ReadTransferStreamBytes(process.StandardError.BaseStream, progress);
                            }
                            catch (Exception ex)
                            {
                                errorException = ex;
                            }
                            finally
                            {
                                errorDone.Set();
                            }
                        });

                        while (!process.WaitForExit(1000))
                        {
                            ReportAdbFileTransferProgress(progress, "传输中", false, false);
                        }
                        process.WaitForExit();
                        outputDone.WaitOne();
                        errorDone.WaitOne();
                    }
                    result.ExitCode = process.ExitCode;
                    result.Output = outputException == null ? DecodeProcessBytes(outputBytes) : outputException.Message;
                    result.Error = errorException == null ? DecodeProcessBytes(errorBytes) : errorException.Message;
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }

            return result;
        }

        private byte[] ReadTransferStreamBytes(Stream stream, AdbFileTransferProgress progress)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memory.Write(buffer, 0, read);
                    string chunk = Encoding.Default.GetString(buffer, 0, read);
                    UpdateAdbFileTransferPercent(progress, chunk);
                }
                return memory.ToArray();
            }
        }

        private void UpdateAdbFileTransferPercent(AdbFileTransferProgress progress, string text)
        {
            if (progress == null || String.IsNullOrEmpty(text))
            {
                return;
            }

            int percent = ExtractLastPercent(text);
            if (percent >= 0)
            {
                lock (progress.SyncRoot)
                {
                    progress.Percent = percent;
                    if (progress.TotalBytes > 0)
                    {
                        ApplyAdbFileTransferByteSampleLocked(progress, progress.TotalBytes * percent / 100L, DateTime.UtcNow);
                    }
                }
            }

            ReportAdbFileTransferProgress(progress, "传输中", false, false);
        }

        private long GetObservedAdbFileTransferBytes(AdbFileTransferProgress progress)
        {
            if (progress == null)
            {
                return -1L;
            }

            if (!String.IsNullOrWhiteSpace(progress.LocalProgressPath))
            {
                try
                {
                    if (File.Exists(progress.LocalProgressPath))
                    {
                        return new FileInfo(progress.LocalProgressPath).Length;
                    }
                }
                catch
                {
                }
            }

            if (!String.IsNullOrWhiteSpace(progress.AdbPath) &&
                !String.IsNullOrWhiteSpace(progress.RemoteProgressSerial) &&
                !String.IsNullOrWhiteSpace(progress.RemoteProgressPath))
            {
                DateTime now = DateTime.UtcNow;
                lock (progress.SyncRoot)
                {
                    if (now - progress.LastObservedCheckUtc < TimeSpan.FromSeconds(1))
                    {
                        return -1L;
                    }
                    progress.LastObservedCheckUtc = now;
                }

                return ReadRemoteFileSize(progress.AdbPath, progress.RemoteProgressSerial, progress.RemoteProgressPath);
            }

            return -1L;
        }

        private long ReadRemoteFileSize(string adb, string serial, string remotePath)
        {
            try
            {
                string command = "stat -c %s " + ShellQuote(remotePath) + " 2>/dev/null || wc -c < " + ShellQuote(remotePath) + " 2>/dev/null";
                string args = "-s " + QuoteArg(serial) + " shell " + command;
                ProcessResult result = RunProcess(adb, args, null, false);
                if (result == null || result.ExitCode != 0)
                {
                    return -1L;
                }

                return ExtractFirstLong((result.Output ?? "") + "\n" + (result.Error ?? ""));
            }
            catch
            {
                return -1L;
            }
        }

        private long ExtractFirstLong(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return -1L;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!Char.IsDigit(text[i]))
                {
                    continue;
                }

                int start = i;
                while (i < text.Length && Char.IsDigit(text[i]))
                {
                    i++;
                }

                long value;
                if (Int64.TryParse(text.Substring(start, i - start), out value))
                {
                    return value;
                }
            }

            return -1L;
        }

        private void ApplyAdbFileTransferByteSampleLocked(AdbFileTransferProgress progress, long currentBytes, DateTime nowUtc)
        {
            if (progress == null || currentBytes < 0)
            {
                return;
            }

            if (progress.TotalBytes > 0)
            {
                currentBytes = Math.Min(currentBytes, progress.TotalBytes);
            }

            if (progress.LastSampleUtc != DateTime.MinValue)
            {
                double seconds = (nowUtc - progress.LastSampleUtc).TotalSeconds;
                if (seconds > 0.05)
                {
                    long delta = Math.Max(0L, currentBytes - progress.LastSampleBytes);
                    progress.SpeedBytesPerSecond = (long)(delta / seconds);
                }
            }

            progress.TransferredBytes = Math.Max(progress.TransferredBytes, currentBytes);
            progress.LastSampleBytes = currentBytes;
            progress.LastSampleUtc = nowUtc;

            if (progress.TotalBytes > 0)
            {
                progress.Percent = (int)Math.Max(0L, Math.Min(100L, progress.TransferredBytes * 100L / progress.TotalBytes));
            }
        }

        private int ExtractLastPercent(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (int i = text.LastIndexOf('%'); i >= 0; i = i <= 0 ? -1 : text.LastIndexOf('%', i - 1))
            {
                int end = i - 1;
                while (end >= 0 && Char.IsWhiteSpace(text[end]))
                {
                    end--;
                }

                int start = end;
                while (start >= 0 && Char.IsDigit(text[start]))
                {
                    start--;
                }
                start++;

                if (start <= end)
                {
                    int percent;
                    if (Int32.TryParse(text.Substring(start, end - start + 1), out percent))
                    {
                        if (percent >= 0 && percent <= 100)
                        {
                            return percent;
                        }
                    }
                }
            }

            return -1;
        }

        private void ReportAdbFileTransferProgress(AdbFileTransferProgress progress, string status, bool force, bool complete)
        {
            if (progress == null)
            {
                return;
            }

            string line;
            long observedBytes = GetObservedAdbFileTransferBytes(progress);
            lock (progress.SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                if (observedBytes >= 0)
                {
                    ApplyAdbFileTransferByteSampleLocked(progress, observedBytes, now);
                }
                else if (complete && String.Equals(status, "完成", StringComparison.Ordinal) && progress.TotalBytes > 0)
                {
                    ApplyAdbFileTransferByteSampleLocked(progress, progress.TotalBytes, now);
                }

                if (!force && now - progress.LastUiUpdateUtc < TimeSpan.FromSeconds(1))
                {
                    return;
                }
                progress.LastUiUpdateUtc = now;

                line = status + ": " + progress.Operation + " " + progress.Index + "/" + progress.Total + " " + FormatAdbFileTransferProgressText(progress);
                if (!String.IsNullOrWhiteSpace(progress.DisplayName))
                {
                    line += " " + progress.DisplayName;
                }
            }

            UpdateOutputProgressLine(progress.Key, line, complete);
        }

        private string FormatAdbFileTransferProgressText(AdbFileTransferProgress progress)
        {
            string percent = progress.Percent >= 0 ? progress.Percent + "%" : "等待进度";
            string speed = FormatBytesPerSecond(progress.SpeedBytesPerSecond);
            if (progress.TotalBytes > 0)
            {
                return percent + " " + FormatBytes(progress.TransferredBytes) + "/" + FormatBytes(progress.TotalBytes) + " " + speed;
            }

            if (progress.TransferredBytes > 0)
            {
                return percent + " " + FormatBytes(progress.TransferredBytes) + " " + speed;
            }

            return percent + " " + speed;
        }

        private string FormatBytesPerSecond(long bytesPerSecond)
        {
            return FormatBytes(bytesPerSecond) + "/s";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0B";
            }

            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int idx = 0;
            while (size >= 1024.0 && idx < units.Length - 1)
            {
                size /= 1024.0;
                idx++;
            }

            return idx == 0 ? size.ToString("0") + units[idx] : size.ToString("0.0") + units[idx];
        }

        private bool IsAdbInstallSuccess(ProcessResult result)
        {
            if (result == null || result.ExitCode != 0)
            {
                return false;
            }

            string text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).ToLowerInvariant();
            return !ContainsAdbFailureText(text);
        }

        private void RunProcessAndLog(string fileName, string arguments, string workingDirectory)
        {
            AppendOutput("> " + fileName + " " + arguments);
            ProcessResult result = RunProcess(fileName, arguments, workingDirectory, true);
            AppendProcessResult(result);
        }

        private bool ValidateExeOrCommand(string pathOrCommand, string displayName)
        {
            if (String.IsNullOrWhiteSpace(pathOrCommand))
            {
                MessageBox.Show(this, "请先配置 " + displayName + " 路径。", "缺少工具路径", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (pathOrCommand.IndexOf(Path.DirectorySeparatorChar) >= 0 || pathOrCommand.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                if (!File.Exists(pathOrCommand))
                {
                    MessageBox.Show(this, displayName + " 不存在:\r\n" + pathOrCommand, "路径无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
            return true;
        }

        private ProcessResult RunProcess(string fileName, string arguments, string workingDirectory, bool includeExitCode)
        {
            ProcessResult result = new ProcessResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                if (!String.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                using (Process process = Process.Start(psi))
                {
                    byte[] outputBytes = null;
                    byte[] errorBytes = null;
                    Exception outputException = null;
                    Exception errorException = null;
                    using (ManualResetEvent outputDone = new ManualResetEvent(false))
                    using (ManualResetEvent errorDone = new ManualResetEvent(false))
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                outputBytes = ReadStreamBytes(process.StandardOutput.BaseStream);
                            }
                            catch (Exception ex)
                            {
                                outputException = ex;
                            }
                            finally
                            {
                                outputDone.Set();
                            }
                        });

                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                errorBytes = ReadStreamBytes(process.StandardError.BaseStream);
                            }
                            catch (Exception ex)
                            {
                                errorException = ex;
                            }
                            finally
                            {
                                errorDone.Set();
                            }
                        });

                        process.WaitForExit();
                        outputDone.WaitOne();
                        errorDone.WaitOne();
                    }
                    result.ExitCode = process.ExitCode;
                    result.Output = outputException == null ? DecodeProcessBytes(outputBytes) : outputException.Message;
                    result.Error = errorException == null ? DecodeProcessBytes(errorBytes) : errorException.Message;
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }

            if (includeExitCode)
            {
                result.Output = result.Output + Environment.NewLine + "[exit code] " + result.ExitCode;
            }
            return result;
        }

        private byte[] ReadStreamBytes(Stream stream)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private string DecodeProcessBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }

            try
            {
                UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
                return strictUtf8.GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private int MeasureGridColumnWidth(DataGridView grid, string columnName, string sampleText, int extraPadding)
        {
            if (grid == null || !grid.Columns.Contains(columnName))
            {
                return 60;
            }

            DataGridViewColumn column = grid.Columns[columnName];
            Font font = grid.Font;
            int headerWidth = TextRenderer.MeasureText(column.HeaderText, font).Width;
            int textWidth = TextRenderer.MeasureText(sampleText ?? "", font).Width;
            return Math.Max(50, Math.Max(headerWidth, textWidth) + extraPadding);
        }

        private int GetGridScrollbarAllowance(DataGridView grid)
        {
            if (grid == null)
            {
                return 0;
            }

            int headerHeight = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            int rowsHeight = grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible);
            bool needsVerticalScrollbar = headerHeight + rowsHeight > grid.ClientSize.Height;
            return needsVerticalScrollbar ? SystemInformation.VerticalScrollBarWidth + 2 : 0;
        }

        private void AppendProcessResult(ProcessResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!String.IsNullOrEmpty(result.Output))
            {
                AppendOutput(result.Output.TrimEnd());
            }
            if (!String.IsNullOrEmpty(result.Error))
            {
                AppendOutput(result.Error.TrimEnd());
            }
        }

        private void AppendOutput(string text)
        {
            if (outputBox == null || outputBox.IsDisposed)
            {
                return;
            }

            if (outputBox.InvokeRequired)
            {
                try
                {
                    outputBox.BeginInvoke(new Action<string>(AppendOutput), text);
                }
                catch
                {
                }
                return;
            }

            outputBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.ScrollToCaret();
        }

        private void UpdateOutputProgressLine(string key, string text, bool complete)
        {
            if (outputBox == null || outputBox.IsDisposed)
            {
                return;
            }

            if (outputBox.InvokeRequired)
            {
                try
                {
                    outputBox.BeginInvoke(new Action<string, string, bool>(UpdateOutputProgressLine), key, text, complete);
                }
                catch
                {
                }
                return;
            }

            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text;
            bool useExistingLine =
                !String.IsNullOrEmpty(outputProgressKey) &&
                String.Equals(outputProgressKey, key, StringComparison.Ordinal) &&
                outputProgressLineStart >= 0 &&
                outputProgressLineStart <= outputBox.TextLength;

            if (!useExistingLine)
            {
                outputProgressKey = key;
                outputProgressLineStart = outputBox.TextLength;
                outputBox.AppendText(line + Environment.NewLine);
            }
            else
            {
                int replaceStart = outputProgressLineStart;
                int replaceEnd = outputBox.Text.IndexOf('\n', replaceStart);
                if (replaceEnd < 0)
                {
                    replaceEnd = outputBox.TextLength;
                }
                if (replaceEnd > replaceStart && outputBox.Text[replaceEnd - 1] == '\r')
                {
                    replaceEnd--;
                }

                outputBox.Select(replaceStart, Math.Max(0, replaceEnd - replaceStart));
                outputBox.SelectedText = line;
            }

            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.SelectionLength = 0;
            outputBox.ScrollToCaret();

            if (complete)
            {
                outputProgressKey = null;
                outputProgressLineStart = -1;
            }
        }

        private void InitializeEmbeddedShellDebugLog()
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                embeddedShellDebugLogPath = Path.Combine(logDir, "embedded-shell-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                File.WriteAllText(
                    embeddedShellDebugLogPath,
                    "# Embedded shell debug log" + Environment.NewLine +
                    "# Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                embeddedShellDebugLogPath = null;
            }
        }

        private void LogEmbeddedShellDebug(TerminalSession session, string eventName, string text)
        {
            if (String.IsNullOrWhiteSpace(embeddedShellDebugLogPath))
            {
                return;
            }

            try
            {
                string serial = session == null ? "" : (session.Serial ?? "");
                string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" +
                    "[serial=" + serial + "]" +
                    "[" + (eventName ?? "event") + "] " +
                    EscapeTextForDebugLog(text) +
                    Environment.NewLine;
                lock (embeddedShellDebugLogLock)
                {
                    File.AppendAllText(embeddedShellDebugLogPath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        private string EscapeTextForDebugLog(string text)
        {
            if (text == null)
            {
                return "<null>";
            }

            StringBuilder builder = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                switch (ch)
                {
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\0':
                        builder.Append("\\0");
                        break;
                    default:
                        if (Char.IsControl(ch))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("X4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }
            return builder.ToString();
        }

        private string[] SplitWhitespace(string input)
        {
            return input.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private string QuoteArg(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private string QuoteCmdToken(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private string ShellQuote(string value)
        {
            if (value == null)
            {
                return "''";
            }
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private string CombineRemotePath(string basePath, string name)
        {
            if (String.IsNullOrWhiteSpace(basePath) || basePath == "/")
            {
                return "/" + name;
            }
            return basePath.TrimEnd('/') + "/" + name;
        }

        private string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value);
            for (int i = 0; i < builder.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (builder[i] == invalid[j])
                    {
                        builder[i] = '_';
                        break;
                    }
                }
            }
            return builder.ToString().Replace(':', '_');
        }
    }

    public sealed class TextEditForm : Form
    {
        private readonly string filePath;
        private readonly TextBox editor;

        public TextEditForm(string filePath)
        {
            this.filePath = filePath;
            Text = "编辑设备文件 - " + Path.GetFileName(filePath);
            Width = 900;
            Height = 650;
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.Padding = new Padding(8);

            editor = new TextBox();
            editor.Dock = DockStyle.Fill;
            editor.Multiline = true;
            editor.ScrollBars = ScrollBars.Both;
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.WordWrap = false;
            editor.Font = new Font("Consolas", 10F);
            editor.Text = File.ReadAllText(filePath, Encoding.UTF8);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;

            Button save = new Button();
            save.Text = "保存并上传";
            save.Width = 110;
            save.Height = 30;
            save.Click += delegate
            {
                File.WriteAllText(this.filePath, editor.Text, Encoding.UTF8);
                DialogResult = DialogResult.OK;
                Close();
            };

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Width = 80;
            cancel.Height = 30;
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(editor, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            Controls.Add(layout);
        }
    }

    public sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}
