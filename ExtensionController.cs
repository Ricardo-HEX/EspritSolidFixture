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
            if (!EnsureMachine(document)) return;

            ActivatePartView();
            ShowSelectionDialog();
        }

        private bool EnsureMachine(Esprit.Document document)
        {
            if (EspritSetupAssembler.HasMachine(document)) return true;

            var owner = new EspritWindowOwner(new IntPtr(Convert.ToInt64(_app.HWND)));
            DialogResult answer = MessageBox.Show(owner,
                "El documento no contiene una máquina.\r\n\r\n" +
                "¿Desea seleccionar y montar una máquina ahora?",
                "Preparación de utillaje", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (answer != DialogResult.Yes) return false;

            string machineFolder = string.Empty;
            try
            {
                machineFolder = _app.Configuration.GetFileDirectory(
                    EspritConstants.espFileType.espFileTypeMachineSetup);
            }
            catch { }

            using (var dialog = new OpenFileDialog
            {
                Title = "Seleccionar máquina de ESPRIT",
                Filter = "Máquinas ESPRIT (*.mprj)|*.mprj|Todos los archivos (*.*)|*.*",
                InitialDirectory = Directory.Exists(machineFolder) ? machineFolder : string.Empty,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

                Esprit.MachineSetup setup = document.InitialMachineSetup;
                try
                {
                    setup.BeginEdit();
                    setup.MachineFileName = dialog.FileName;
                    setup.EndEdit();
                    document.Refresh();
                }
                catch (Exception ex)
                {
                    try { setup.CancelEdit(); } catch { }
                    MessageBox.Show(owner,
                        "No se pudo montar la máquina seleccionada:\r\n" + ex.Message,
                        "Preparación de utillaje", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }
            }

            if (EspritSetupAssembler.HasMachine(document)) return true;
            MessageBox.Show(owner,
                "ESPRIT no reconoció el archivo seleccionado como una máquina válida.",
                "Preparación de utillaje", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
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
                if (_dialog.Visible)
                {
                    _dialog.BringToFront();
                    _dialog.Activate();
                    return;
                }

                bool partSetupEditing = false;
                try
                {
                    partSetupEditing = ((Esprit.Document)_app.Document).PartSetup.IsEditing;
                }
                catch { }
                if (partSetupEditing) return;

                // Una instancia oculta fuera de Configuración de la pieza es
                // un formulario residual de un montaje ya finalizado o abortado.
                _dialog.FormClosed -= DialogOnFormClosed;
                try { _dialog.Close(); } catch { }
                _dialog.Dispose();
                _dialog = null;
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
                using (var bitmap = new Bitmap(32, 32))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var fixture = new SolidBrush(Color.FromArgb(75, 105, 125)))
                    using (var fixtureTop = new SolidBrush(Color.FromArgb(120, 145, 160)))
                    using (var part = new SolidBrush(Color.FromArgb(66, 174, 210)))
                    using (var outline = new Pen(Color.FromArgb(45, 68, 82), 1.2F))
                    using (var plus = new Pen(Color.FromArgb(67, 161, 45), 2.4F))
                    {
                        // Utillaje: base y dos apoyos simples.
                        graphics.FillRectangle(fixture, 4, 19, 22, 8);
                        graphics.FillRectangle(fixtureTop, 3, 16, 24, 5);
                        graphics.FillRectangle(fixture, 5, 13, 5, 5);
                        graphics.FillRectangle(fixture, 20, 13, 5, 5);
                        graphics.DrawRectangle(outline, 4, 19, 22, 8);

                        // Pieza: una forma limpia y diferenciada sobre el utillaje.
                        graphics.FillRectangle(part, 10, 7, 10, 9);
                        graphics.DrawRectangle(outline, 10, 7, 10, 9);
                        graphics.DrawLine(outline, 10, 7, 14, 4);
                        graphics.DrawLine(outline, 20, 7, 16, 4);
                        graphics.DrawLine(outline, 14, 4, 16, 4);

                        // Acción de crear/añadir.
                        graphics.DrawLine(plus, 26, 3, 26, 12);
                        graphics.DrawLine(plus, 21.5F, 7.5F, 30.5F, 7.5F);
                    }
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
