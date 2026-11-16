using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ESPRIT.NetApi.Ribbon;

namespace EspritSolidFixture
{
    internal sealed class ExtensionController : IDisposable
    {
        private const string ButtonKey = "EspritSolidFixture.CreateFixture";
        private const string NativeMachineKey = "Setup.Machine";

        private readonly Esprit.Application _app;
        private readonly IRibbon _ribbon;
        private readonly Timer _startupTimer;
        private readonly Icon _icon;
        private IRibbonItems _homeItems;
        private FixturePreparationDialog _dialog;
        private bool _disposed;

        public ExtensionController(Esprit.Application app)
        {
            _app = app;
            _ribbon = (IRibbon)app.Ribbon;
            _icon = LoadFixtureIcon();
            _ribbon.OnButtonClick += OnButtonClick;
            LoadTooltips();

            _startupTimer = new Timer { Interval = 500 };
            _startupTimer.Tick += StartupTimerOnTick;
            _startupTimer.Start();
            TryAddHomeButton();
        }

        private void StartupTimerOnTick(object sender, EventArgs e) => TryAddHomeButton();

        private void TryAddHomeButton()
        {
            if (_disposed) return;
            try
            {
                var machineItems = _ribbon.FindRibbonItems(NativeMachineKey);
                if (machineItems == null || machineItems.Count == 0) return;

                object parent = machineItems[0].Parent;
                _homeItems = parent as IRibbonItems;
                if (_homeItems == null && parent is IRibbonGroup group) _homeItems = group.Items;
                if (_homeItems == null) return;

                if (!_homeItems.Contains(ButtonKey))
                    _homeItems.AddButton(ButtonKey, "Preparación de utillaje", true, _icon);

                _startupTimer.Stop();
            }
            catch (Exception ex) { Log("No se pudo añadir el botón a Inicio: " + ex.Message); }
        }

        private void OnButtonClick(object sender, ButtonClickEventArgs e)
        {
            if (e.Key != ButtonKey) return;
            e.Handled = true;

            Esprit.Document document = null;
            try { document = (Esprit.Document)_app.Document; } catch { }
            if (!EspritSetupAssembler.HasMachine(document))
            {
                MessageBox.Show(new EspritWindowOwner(new IntPtr(Convert.ToInt64(_app.HWND))),
                    "El documento no contiene una máquina.\r\n\r\n" +
                    "Monte o seleccione una máquina antes de preparar el utillaje.",
                    "Preparación de utillaje", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ActivatePartView();
            ShowSelectionDialog();
        }

        private void ActivatePartView()
        {
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                if (document == null) return;
                for (int index = 1; index <= document.Windows.Count; index++)
                {
                    Esprit.Window window = document.Windows[index];
                    if (window.ViewType != EspritConstants.espViewType.espViewTypePart) continue;
                    window.SetActiveView();
                    window.Refresh();
                    return;
                }
            }
            catch (Exception ex) { Log("No se pudo activar la vista de pieza: " + ex.Message); }
        }

        private void ShowSelectionDialog()
        {
            if (_app.Document == null) return;
            if (_dialog != null && !_dialog.IsDisposed)
            {
                _dialog.BringToFront();
                _dialog.Activate();
                return;
            }

            _dialog = new FixturePreparationDialog(_app);
            _dialog.FormClosed += DialogOnFormClosed;
            _dialog.Show(new EspritWindowOwner(new IntPtr(Convert.ToInt64(_app.HWND))));
            _dialog.PositionAtOwnerTopLeft(new IntPtr(Convert.ToInt64(_app.HWND)));
        }

        private void DialogOnFormClosed(object sender, FormClosedEventArgs e)
        {
            if (_dialog != null) _dialog.FormClosed -= DialogOnFormClosed;
            _dialog = null;
        }

        private void LoadTooltips()
        {
            try
            {
                string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string file = Path.Combine(folder ?? string.Empty, "EspritSolidFixture.Tooltips.xml");
                if (File.Exists(file)) _ribbon.LoadToolTipConfiguration(file);
            }
            catch (Exception ex) { Log("No se pudo cargar la descripción del botón: " + ex.Message); }
        }

        private static Icon LoadFixtureIcon()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("EspritSolidFixture.Resources.Fixture.png"))
                using (var bitmap = new Bitmap(stream))
                {
                    IntPtr handle = bitmap.GetHicon();
                    try { return (Icon)Icon.FromHandle(handle).Clone(); }
                    finally { DeleteObject(handle); }
                }
            }
            catch { return (Icon)SystemIcons.Application.Clone(); }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private void Log(string message)
        {
            try
            {
                _app.EventWindow.AddMessage(EspritConstants.espMessageType.espMessageTypeInformation,
                    "EspritSolidFixture", message);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _startupTimer.Stop();
            _startupTimer.Tick -= StartupTimerOnTick;
            _startupTimer.Dispose();
            _ribbon.OnButtonClick -= OnButtonClick;

            try
            {
                if (_homeItems != null && _homeItems.Contains(ButtonKey)) _homeItems.Remove(ButtonKey);
            }
            catch { }

            if (_dialog != null && !_dialog.IsDisposed)
            {
                _dialog.FormClosed -= DialogOnFormClosed;
                _dialog.Close();
                _dialog.Dispose();
            }
            _icon.Dispose();
        }

        private sealed class EspritWindowOwner : IWin32Window
        {
            public EspritWindowOwner(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }
    }
}
