using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EspritSolidFixture
{
    internal static class ModernGdmlFileDialog
    {
        private static readonly Guid ClientGuid =
            new Guid("4D70C733-255D-4DF4-933D-88C56082C9D4");
        private static int _previewPaneSuppressed;

        public static string Show(IntPtr owner, string title, string initialFolder)
        {
            IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialog();
            IShellItem folder = null;
            IShellItem result = null;
            try
            {
                dialog.SetTitle(title);
                Guid clientGuid = ClientGuid;
                dialog.SetClientGuid(ref clientGuid);
                dialog.SetOptions(FOS.FORCEFILESYSTEM | FOS.FILEMUSTEXIST |
                                  FOS.PATHMUSTEXIST | FOS.NOCHANGEDIR);
                var filters = new[]
                {
                    new COMDLG_FILTERSPEC("Accesorios ESPRIT (*.gdml)", "*.gdml"),
                    new COMDLG_FILTERSPEC("Todos los archivos (*.*)", "*.*")
                };
                dialog.SetFileTypes((uint)filters.Length, filters);
                dialog.SetFileTypeIndex(1);

                if (!string.IsNullOrWhiteSpace(initialFolder) &&
                    SHCreateItemFromParsingName(initialFolder, IntPtr.Zero,
                        typeof(IShellItem).GUID, out folder) == 0)
                    dialog.SetFolder(folder);

                SuppressPreviewPane(title);
                int resultCode = dialog.Show(owner);
                if (resultCode == unchecked((int)0x800704C7)) return null;
                Marshal.ThrowExceptionForHR(resultCode);
                dialog.GetResult(out result);
                result.GetDisplayName(SIGDN.FILESYSPATH, out IntPtr pathPointer);
                try { return Marshal.PtrToStringUni(pathPointer); }
                finally { Marshal.FreeCoTaskMem(pathPointer); }
            }
            finally
            {
                if (result != null) Marshal.FinalReleaseComObject(result);
                if (folder != null) Marshal.FinalReleaseComObject(folder);
                Marshal.FinalReleaseComObject(dialog);
            }
        }

        private static void SuppressPreviewPane(string title)
        {
            if (Volatile.Read(ref _previewPaneSuppressed) != 0) return;
            var thread = new Thread(() =>
            {
                int processId = Process.GetCurrentProcess().Id;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    IntPtr dialogWindow = FindWindow(processId, title);
                    if (dialogWindow != IntPtr.Zero)
                    {
                        Thread.Sleep(120);
                        SetForegroundWindow(dialogWindow);
                        // Alt+P es el comando nativo del Explorador para ocultar
                        // el panel de vista previa. Se ejecuta una sola vez por
                        // sesión de ESPRIT y Windows conserva después ese estado.
                        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_P, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_P, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        Interlocked.Exchange(ref _previewPaneSuppressed, 1);
                        return;
                    }
                    Thread.Sleep(50);
                }
            }) { IsBackground = true, Name = "GDML preview pane suppressor" };
            thread.Start();
        }

        private static IntPtr FindWindow(int processId, string title)
        {
            IntPtr result = IntPtr.Zero;
            EnumWindows((window, state) =>
            {
                GetWindowThreadProcessId(window, out uint windowProcessId);
                if (windowProcessId != processId || !IsWindowVisible(window)) return true;
                var text = new StringBuilder(256);
                GetWindowText(window, text, text.Capacity);
                if (!string.Equals(text.ToString(), title, StringComparison.Ordinal)) return true;
                result = window;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path,
            IntPtr bindContext, [In] Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text,
            int maximumCount);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode,
            uint flags, UIntPtr extraInfo);

        private const byte VK_MENU = 0x12;
        private const byte VK_P = 0x50;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(FOS options);
            void GetOptions(out FOS options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, int alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }

        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog : IFileDialog
        {
            [PreserveSig] new int Show(IntPtr parent);
            new void SetFileTypes(uint count,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] filters);
            new void SetFileTypeIndex(uint index);
            new void GetFileTypeIndex(out uint index);
            new void Advise(IntPtr events, out uint cookie);
            new void Unadvise(uint cookie);
            new void SetOptions(FOS options);
            new void GetOptions(out FOS options);
            new void SetDefaultFolder(IShellItem item);
            new void SetFolder(IShellItem item);
            new void GetFolder(out IShellItem item);
            new void GetCurrentSelection(out IShellItem item);
            new void SetFileName(string name);
            new void GetFileName(out string name);
            new void SetTitle(string title);
            new void SetOkButtonLabel(string text);
            new void SetFileNameLabel(string label);
            new void GetResult(out IShellItem item);
            new void AddPlace(IShellItem item, int alignment);
            new void SetDefaultExtension(string extension);
            new void Close(int result);
            new void SetClientGuid(ref Guid guid);
            new void ClearClientData();
            new void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId,
                out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(SIGDN displayName, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem item, uint hint, out int order);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COMDLG_FILTERSPEC
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string Name;
            [MarshalAs(UnmanagedType.LPWStr)] public string Spec;
            public COMDLG_FILTERSPEC(string name, string spec)
            {
                Name = name;
                Spec = spec;
            }
        }

        [Flags]
        private enum FOS : uint
        {
            FORCEFILESYSTEM = 0x40,
            NOCHANGEDIR = 0x8,
            PATHMUSTEXIST = 0x800,
            FILEMUSTEXIST = 0x1000
        }

        private enum SIGDN : uint
        {
            FILESYSPATH = 0x80058000
        }
    }
}
