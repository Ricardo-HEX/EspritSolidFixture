using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EspritSolidFixture
{
    internal sealed class FixturePreparationDialog : Form
    {
        private static readonly Color HeaderGray = Color.FromArgb(218, 218, 218);
        private static readonly Color SelectionOrange = Color.FromArgb(255, 218, 181);

        private readonly Esprit.Application _app;
        private readonly List<SolidChoice> _fixtures = new List<SolidChoice>();
        private SolidChoice _part;
        private SolidChoice _stock;
        private readonly ListBox _partBox;
        private readonly ListBox _stockBox;
        private readonly ListBox _fixtureBox;
        private readonly NumericUpDown _phase;
        private readonly TextBox _folderText;
        private readonly Button _accept;
        private readonly Label _hint;
        private readonly Label _countLabel;
        private readonly CheckBox _combine;
        private readonly RadioButton _mountBottom;
        private readonly RadioButton _mountTop;
        private readonly ComboBox _destination;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 300 };
        private readonly Esprit.Group _documentGroup;
        private readonly Timer _selectionRemovalTimer;
        private readonly HashSet<string> _pendingRemovalKeys = new HashSet<string>();
        private SelectionRole? _activeSelectionRole;
        private SelectionRole? _pendingRemovalRole;
        private bool _synchronizingSelection;

        public FixturePreparationDialog(Esprit.Application app)
        {
            _app = app;
            Text = "Preparación de utillaje";
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ShowIcon = true;
            Icon = LoadEspritIcon();
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(640, 1010);
            MinimumSize = new Size(656, 1049);
            BackColor = Color.White;

            try
            {
                _documentGroup = ((Esprit.Document)_app.Document).Group as Esprit.Group;
                if (_documentGroup != null)
                {
                    _documentGroup.OnAdd += DocumentGroupOnAdd;
                    _documentGroup.OnRemove += DocumentGroupOnRemove;
                }
            }
            catch { }

            _selectionRemovalTimer = new Timer { Interval = 180 };
            _selectionRemovalTimer.Tick += SelectionRemovalTimerOnTick;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(18, 16, 18, 14),
                BackColor = Color.White,
                AutoScroll = false
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label
            {
                Text = "Seleccione la pieza, el bruto y los sólidos que formarán el utillaje.",
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = Color.FromArgb(70, 77, 83),
                Padding = new Padding(2, 7, 2, 6),
                Margin = new Padding(0, 0, 0, 8)
            });

            layout.Controls.Add(Section("Pieza"));
            _partBox = SingleSelectionBox("Haz clic para seleccionar el sólido de pieza");
            _partBox.MouseDown += (s, e) => ActivateNativeSelection(SelectionRole.Part);
            layout.Controls.Add(_partBox);

            layout.Controls.Add(Section("Bruto sólido"));
            _stockBox = SingleSelectionBox("Haz clic para seleccionar el sólido de bruto");
            _stockBox.MouseDown += (s, e) => ActivateNativeSelection(SelectionRole.Stock);
            layout.Controls.Add(_stockBox);

            layout.Controls.Add(Section("Utillajes"));
            _fixtureBox = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 220,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false
            };
            _fixtureBox.MouseDown += FixtureBoxOnMouseDown;
            _fixtureBox.KeyDown += FixtureBoxOnKeyDown;
            var fixtureMenu = new ContextMenuStrip();
            fixtureMenu.Items.Add("Eliminar", CreateDeleteIcon(), RemoveSelectedFixtures);
            _fixtureBox.ContextMenuStrip = fixtureMenu;
            layout.Controls.Add(_fixtureBox);

            _countLabel = new Label
            {
                Text = "0 sólidos seleccionados",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(88, 96, 103),
                Padding = new Padding(0, 5, 2, 0),
                Margin = new Padding(0, 0, 0, 5)
            };
            layout.Controls.Add(_countLabel);

            _combine = new CheckBox
            {
                Text = "Combinar sólidos",
                Appearance = Appearance.Button,
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Margin = new Padding(0, 3, 0, 10),
                Cursor = Cursors.Hand
            };
            _combine.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            _combine.CheckedChanged += (s, e) => StyleCombineButton();
            _toolTip.SetToolTip(_combine,
                "Crea un único GDML con todos los sólidos seleccionados.");
            layout.Controls.Add(_combine);

            layout.Controls.Add(Section("Cara de montaje"));
            var mountingChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 82,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.White
            };
            mountingChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            mountingChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _mountBottom = MountingChoice("Cara inferior\r\nZ normal", true);
            _mountTop = MountingChoice("Cara superior\r\nGiro Y 180° + Z 180°", false);
            _mountBottom.Margin = new Padding(0, 0, 6, 0);
            _mountTop.Margin = new Padding(6, 0, 0, 0);
            _mountBottom.CheckedChanged += (s, e) => StyleMountingChoices();
            _mountTop.CheckedChanged += (s, e) => StyleMountingChoices();
            mountingChoices.Controls.Add(_mountBottom, 0, 0);
            mountingChoices.Controls.Add(_mountTop, 1, 0);
            layout.Controls.Add(mountingChoices);
            StyleMountingChoices();

            layout.Controls.Add(Section("Destino del montaje"));
            _destination = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 8)
            };
            _destination.Items.AddRange(new object[]
            {
                "Configuración actual",
                "Nueva configuración"
            });
            _destination.SelectedIndex = 0;
            _destination.SelectedIndexChanged += DestinationOnSelectedIndexChanged;
            layout.Controls.Add(_destination);

            _hint = new Label
            {
                Text = "Haz clic en el recuadro para seleccionar sólidos.",
                Dock = DockStyle.Top,
                Height = 46,
                ForeColor = Color.FromArgb(78, 86, 92),
                BackColor = Color.FromArgb(246, 248, 249),
                Padding = new Padding(10, 10, 8, 7),
                Margin = new Padding(0, 0, 0, 4)
            };
            layout.Controls.Add(_hint);

            layout.Controls.Add(Section("Fase"));
            _phase = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Dock = DockStyle.Top,
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 5)
            };
            _phase.ValueChanged += PhaseOnValueChanged;
            layout.Controls.Add(_phase);

            layout.Controls.Add(Section("Carpeta de salida"));
            _folderText = new TextBox
            {
                Text = "Se calculará desde el CAD del utillaje",
                ReadOnly = true,
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 4)
            };
            layout.Controls.Add(_folderText);
            _toolTip.SetToolTip(_folderText, _folderText.Text);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                Padding = new Padding(16, 16, 16, 12),
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = SystemColors.Control
            };
            _accept = ActionButton("Aceptar", AcceptPreparation);
            _accept.Enabled = false;
            var cancel = ActionButton("Cancelar", CancelPreparation);
            StylePrimaryButton(_accept);
            StyleSecondaryButton(cancel);
            _accept.EnabledChanged += (s, e) => StylePrimaryButton(_accept);
            bottom.Controls.Add(_accept);
            bottom.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(bottom);
            AcceptButton = _accept;
            CancelButton = cancel;

        }

        public void PositionAtOwnerTopLeft(IntPtr ownerHandle)
        {
            if (ownerHandle != IntPtr.Zero && GetWindowRect(ownerHandle, out NativeRect rect))
            {
                Location = new Point(rect.Left + 18, Math.Max(rect.Top + 70, rect.Top + 10));
                return;
            }
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + 18, area.Top + 70);
        }

        private void FixtureBoxOnMouseDown(object sender, MouseEventArgs e)
        {
            int itemIndex = _fixtureBox.IndexFromPoint(e.Location);

            if (e.Button == MouseButtons.Right && itemIndex >= 0)
            {
                if (!_fixtureBox.SelectedIndices.Contains(itemIndex))
                {
                    _fixtureBox.ClearSelected();
                    _fixtureBox.SelectedIndex = itemIndex;
                }
                return;
            }

            // Los elementos existentes se pueden seleccionar para eliminarlos.
            // Un clic en la zona vacía activa la selección nativa de ESPRIT.
            if (e.Button == MouseButtons.Left && itemIndex < 0)
                ActivateNativeSelection(SelectionRole.Fixtures);
        }

        private void ActivateNativeSelection(SelectionRole role)
        {
            Esprit.Document document;
            try { document = (Esprit.Document)_app.Document; }
            catch { return; }
            if (document == null) return;

            _activeSelectionRole = role;
            _selectionRemovalTimer.Stop();
            _pendingRemovalKeys.Clear();
            _pendingRemovalRole = null;
            _partBox.BackColor = role == SelectionRole.Part ? SelectionOrange : Color.White;
            _stockBox.BackColor = role == SelectionRole.Stock ? SelectionOrange : Color.White;
            _fixtureBox.BackColor = role == SelectionRole.Fixtures ? SelectionOrange : Color.White;

            _synchronizingSelection = true;
            try
            {
                document.Group.Clear();
                if (role == SelectionRole.Part && _part != null)
                    document.Group.Add(_part.Solid);
                else if (role == SelectionRole.Stock && _stock != null)
                    document.Group.Add(_stock.Solid);
                else if (role == SelectionRole.Fixtures)
                    foreach (SolidChoice fixture in _fixtures) document.Group.Add(fixture.Solid);
            }
            finally { _synchronizingSelection = false; }

            _hint.Text = role == SelectionRole.Part
                ? "Selecciona la pieza con un clic o una ventana en la vista."
                : role == SelectionRole.Stock
                    ? "Selecciona el bruto con un clic o una ventana en la vista."
                    : "Selecciona utillajes con clics o una ventana en la vista.";
            ActivatePartView();
            document.Refresh();
        }

        private void DocumentGroupOnAdd(ref Array objects, object digitizedInfo)
        {
            if (_synchronizingSelection || !_activeSelectionRole.HasValue || IsDisposed) return;

            // La selección por ventana suele quitar primero el grupo anterior y
            // añadir inmediatamente el nuevo. Si hay altas, esas bajas no deben
            // eliminar elementos ya guardados en la extensión.
            _selectionRemovalTimer.Stop();
            _pendingRemovalKeys.Clear();
            _pendingRemovalRole = null;

            var additions = new List<SolidChoice>();
            foreach (object selected in objects)
            {
                Esprit.Solid solid = ResolveOwningSolid(selected);
                if (solid != null && additions.All(x => x.Key != Convert.ToString(solid.Key)))
                    additions.Add(SolidChoice.FromSolid(solid));
            }

            SelectionRole role = _activeSelectionRole.Value;
            if (role == SelectionRole.Part && additions.Count > 0)
                _part = additions[0];
            else if (role == SelectionRole.Stock && additions.Count > 0)
                _stock = additions[0];
            else if (role == SelectionRole.Fixtures)
                foreach (SolidChoice solid in additions)
                    if (_fixtures.All(x => x.Key != solid.Key)) _fixtures.Add(solid);

            RefreshState();
            // ESPRIT puede devolver caras, aristas o vértices al seleccionar por
            // clic o ventana. Sustituimos esos subelementos por sus sólidos para
            // mantener simultáneamente la selección por ventana y el filtro sólido.
            try { SynchronizeGraphicSelection((Esprit.Document)_app.Document); } catch { }
            _hint.Text = additions.Count == 0
                ? "La selección no contenía sólidos completos."
                : additions.Count + (additions.Count == 1
                    ? " sólido añadido."
                    : " sólidos añadidos.");
        }

        private Esprit.Solid ResolveOwningSolid(object selected)
        {
            if (selected is Esprit.Solid solid) return solid;

            EspritSolids.ISolidBody selectedBody = null;
            try
            {
                if (selected is EspritSolids.ISolidFace face)
                    selectedBody = face.SolidBody;
                else if (selected is EspritSolids.ISolidEdge edge)
                    selectedBody = edge.SolidBody;
                else if (selected is EspritSolids.ISolidVertex vertex)
                    selectedBody = vertex.SolidBody;
                else if (selected is EspritSolids.ISolidBody body)
                    selectedBody = body;
            }
            catch { }

            if (selectedBody == null) return null;

            int bodyIdentity;
            try { bodyIdentity = selectedBody.Identity; }
            catch { return null; }

            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                foreach (Esprit.Solid candidate in document.Solids)
                {
                    try
                    {
                        var candidateBody = (EspritSolids.ISolidBody)candidate.SolidBody;
                        if (candidateBody != null && candidateBody.Identity == bodyIdentity)
                            return candidate;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private void DocumentGroupOnRemove(ref Array objects)
        {
            if (_synchronizingSelection || !_activeSelectionRole.HasValue || IsDisposed) return;

            _pendingRemovalKeys.Clear();
            foreach (object selected in objects)
            {
                if (selected is Esprit.Solid solid)
                    _pendingRemovalKeys.Add(Convert.ToString(solid.Key));
            }
            if (_pendingRemovalKeys.Count == 0) return;

            _pendingRemovalRole = _activeSelectionRole;
            _selectionRemovalTimer.Stop();
            _selectionRemovalTimer.Start();
        }

        private void SelectionRemovalTimerOnTick(object sender, EventArgs e)
        {
            _selectionRemovalTimer.Stop();
            if (!_pendingRemovalRole.HasValue || _pendingRemovalKeys.Count == 0) return;

            if (_pendingRemovalRole == SelectionRole.Part &&
                _part != null && _pendingRemovalKeys.Contains(_part.Key))
                _part = null;
            else if (_pendingRemovalRole == SelectionRole.Stock &&
                     _stock != null && _pendingRemovalKeys.Contains(_stock.Key))
                _stock = null;
            else if (_pendingRemovalRole == SelectionRole.Fixtures)
                _fixtures.RemoveAll(x => _pendingRemovalKeys.Contains(x.Key));

            int removed = _pendingRemovalKeys.Count;
            _pendingRemovalKeys.Clear();
            _pendingRemovalRole = null;
            RefreshState();
            _hint.Text = removed == 1
                ? "1 sólido retirado de la selección."
                : removed + " sólidos retirados de la selección.";
        }

        private void PhaseOnValueChanged(object sender, EventArgs e)
        {
            if (_phase.Value >= 2)
            {
                _mountTop.Checked = true;
                _destination.SelectedIndex = (int)MountDestination.NewConfiguration;
            }
            RefreshState();
        }

        private void DestinationOnSelectedIndexChanged(object sender, EventArgs e)
        {
            if ((MountDestination)_destination.SelectedIndex == MountDestination.NewConfiguration)
            {
                _mountTop.Checked = true;
                int nextPhase = GetNextConfigurationNumber();
                if (_phase.Value != nextPhase)
                {
                    _phase.Value = nextPhase;
                    return;
                }
            }
            RefreshState();
        }

        private int GetNextConfigurationNumber()
        {
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                // La configuración inicial es la 1. Según cómo se creó el montaje,
                // ESPRIT puede registrar las adicionales en MachineSetupChanges,
                // MachineSetupSequences o en ambas colecciones.
                int existingConfigurations = Math.Max(
                    document.MachineSetupChanges.Count,
                    document.MachineSetupSequences.Count);
                return Math.Max(2, existingConfigurations + 1);
            }
            catch { return 2; }
        }

        private void FixtureBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;
            RemoveSelectedFixtures(sender, EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void RemoveSelectedFixtures(object sender, EventArgs e)
        {
            var keys = new HashSet<string>(
                _fixtureBox.SelectedItems.Cast<SolidChoice>().Select(x => x.Key));
            if (keys.Count == 0) return;
            _fixtures.RemoveAll(x => keys.Contains(x.Key));

            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                SynchronizeGraphicSelection(document);
            }
            catch { }

            RefreshState();
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
            catch { }
        }

        private void RefreshState()
        {
            RefreshSingleBox(_partBox, _part, "Haz clic para seleccionar el sólido de pieza");
            RefreshSingleBox(_stockBox, _stock, "Haz clic para seleccionar el sólido de bruto");

            _fixtureBox.BeginUpdate();
            _fixtureBox.Items.Clear();
            foreach (SolidChoice fixture in _fixtures) _fixtureBox.Items.Add(fixture);
            _fixtureBox.EndUpdate();

            _countLabel.Text = _fixtures.Count == 1
                ? "1 sólido seleccionado"
                : _fixtures.Count + " sólidos seleccionados";
            _combine.Enabled = _fixtures.Count > 1;
            if (!_combine.Enabled) _combine.Checked = false;
            StyleCombineButton();

            string folder = DetermineOutputFolder();
            _folderText.Text = folder ?? "No se pudo determinar la ruta del CAD";
            _toolTip.SetToolTip(_folderText, _folderText.Text);
            bool reusePreviousPart = CanReusePreviousPart() && _part == null && _stock == null;
            bool definePartAndStock = _part != null && _stock != null;
            _accept.Enabled = (reusePreviousPart || definePartAndStock) &&
                              _fixtures.Count > 0 && folder != null;

            if (CanReusePreviousPart() && _part == null && _stock == null)
                _hint.Text = "Se reutilizarán la pieza y el bruto de la fase anterior.";
        }

        private void SynchronizeGraphicSelection(Esprit.Document document)
        {
            if (document == null) return;
            _synchronizingSelection = true;
            try
            {
                document.Group.Clear();
                if (_activeSelectionRole == SelectionRole.Part)
                {
                    if (_part != null) document.Group.Add(_part.Solid);
                }
                else if (_activeSelectionRole == SelectionRole.Stock)
                {
                    if (_stock != null) document.Group.Add(_stock.Solid);
                }
                else if (_activeSelectionRole == SelectionRole.Fixtures)
                {
                    foreach (SolidChoice fixture in _fixtures) document.Group.Add(fixture.Solid);
                }
                else
                {
                    if (_part != null) document.Group.Add(_part.Solid);
                    if (_stock != null) document.Group.Add(_stock.Solid);
                    foreach (SolidChoice fixture in _fixtures) document.Group.Add(fixture.Solid);
                }
            }
            finally { _synchronizingSelection = false; }
            document.Refresh();
        }

        private static Bitmap CreateDeleteIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(210, 35, 35), 2.4F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 4, 4, 12, 12);
                graphics.DrawLine(pen, 12, 4, 4, 12);
            }
            return bitmap;
        }

        private void AcceptPreparation(object sender, EventArgs e)
        {
            _activeSelectionRole = null;
            string folder = DetermineOutputFolder();
            bool reusePreviousPart = CanReusePreviousPart() && _part == null && _stock == null;
            bool definePartAndStock = _part != null && _stock != null;
            if ((!reusePreviousPart && !definePartAndStock) || _fixtures.Count == 0 || folder == null)
                return;
            IReadOnlyList<string> paths;
            double mountingStackHeight = 0;
            double workOffsetBaseZ = 0;
            try
            {
                var document = (Esprit.Document)_app.Document;
                var exporter = new GdmlFixtureExporter(document);
                IEnumerable<SolidChoice> fixtureOrder =
                    _fixtures.OrderBy(x => GetMinimumZ(x.Solid));
                var orderedChoices = fixtureOrder.ToList();
                var orderedFixtures = orderedChoices.Select(x => x.Solid).ToList();
                mountingStackHeight = _combine.Checked && orderedChoices.Count > 1
                    ? orderedChoices.Max(x => GetMaximumZ(x.Solid)) -
                      orderedChoices.Min(x => GetMinimumZ(x.Solid))
                    : orderedChoices.Sum(x =>
                        GetMaximumZ(x.Solid) - GetMinimumZ(x.Solid));
                workOffsetBaseZ = _mountTop.Checked
                    ? orderedChoices.Max(x => GetMaximumZ(x.Solid))
                    : orderedChoices.Min(x => GetMinimumZ(x.Solid));
                paths = exporter.Export(
                    orderedFixtures, folder, Decimal.ToInt32(_phase.Value),
                    _combine.Checked, _mountTop.Checked);
            }
            catch (Exception ex)
            {
                ShowStageError("crear los archivos GDML de utillaje", ex);
                return;
            }

            string workpieceKey;
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                var assembler = new EspritSetupAssembler(document);
                workpieceKey = reusePreviousPart
                    ? assembler.GetExistingWorkpieceKey()
                    : assembler.ConfigurePartAndStock(_part.Solid, _stock.Solid);
            }
            catch (Exception ex)
            {
                ShowStageError("actualizar la pieza y el bruto", ex);
                return;
            }

            try
            {
                var document = (Esprit.Document)_app.Document;
                var assembler = new EspritSetupAssembler(document);
                bool newConfiguration =
                    (MountDestination)_destination.SelectedIndex == MountDestination.NewConfiguration;
                assembler.MountFixturesAndWorkpiece(
                    paths, workpieceKey, newConfiguration, _mountTop.Checked,
                    mountingStackHeight, workOffsetBaseZ);
            }
            catch (Exception ex)
            {
                ShowStageError("montar el utillaje y la pieza en la máquina", ex);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowStageError(string stage, Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo " + stage + ":\r\n" + exception.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private bool CanReusePreviousPart()
        {
            return _phase.Value >= 2 ||
                   (MountDestination)_destination.SelectedIndex == MountDestination.NewConfiguration;
        }

        private void CancelPreparation(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private string DetermineOutputFolder()
        {
            if (_fixtures.Count == 0) return null;

            string fixturesRoot;
            try
            {
                fixturesRoot = _app.Configuration.GetFileDirectory(
                    EspritConstants.espFileType.espFileTypeFixtures);
            }
            catch { return null; }
            if (string.IsNullOrWhiteSpace(fixturesRoot)) return null;

            SolidChoice reference = _fixtures.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.CadPath));
            string cadName = reference == null ? string.Empty : Path.GetFileNameWithoutExtension(reference.CadPath);
            if (string.IsNullOrWhiteSpace(cadName))
            {
                try { cadName = Path.GetFileNameWithoutExtension(_app.Document.FileName); } catch { }
            }
            if (string.IsNullOrWhiteSpace(cadName)) cadName = "Pieza";
            cadName = SanitizeFileName(cadName);
            return Path.Combine(fixturesRoot, "Utillajes_" + cadName);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim().TrimEnd('.');
        }

        private static double GetMinimumZ(Esprit.Solid solid)
        {
            EspritSolids.ISolidBody body = (EspritSolids.ISolidBody)solid.SolidBody;
            double minimum = double.PositiveInfinity;
            foreach (EspritSolids.ISolidVertex vertex in body.SolidVertices)
                minimum = Math.Min(minimum, vertex.Geometry.Z);

            if (double.IsPositiveInfinity(minimum))
                throw new InvalidDataException("No se pudo calcular la altura del sólido " + solid.Number + ".");
            return minimum;
        }

        private static double GetMaximumZ(Esprit.Solid solid)
        {
            EspritSolids.ISolidBody body = (EspritSolids.ISolidBody)solid.SolidBody;
            double maximum = double.NegativeInfinity;
            foreach (EspritSolids.ISolidVertex vertex in body.SolidVertices)
                maximum = Math.Max(maximum, vertex.Geometry.Z);

            if (double.IsNegativeInfinity(maximum))
                throw new InvalidDataException("No se pudo calcular la altura del sólido " + solid.Number + ".");
            return maximum;
        }

        private static Icon LoadEspritIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                return icon == null ? (Icon)SystemIcons.Application.Clone() : (Icon)icon.Clone();
            }
            catch { return (Icon)SystemIcons.Application.Clone(); }
        }

        private static Label Section(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = HeaderGray,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Margin = new Padding(0, 8, 0, 6),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        private static ListBox SingleSelectionBox(string placeholder)
        {
            var box = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                Cursor = Cursors.Hand
            };
            box.Items.Add(placeholder);
            return box;
        }

        private static void RefreshSingleBox(ListBox box, SolidChoice choice, string placeholder)
        {
            box.BeginUpdate();
            box.Items.Clear();
            box.Items.Add(choice == null ? placeholder : choice.DisplayName);
            box.EndUpdate();
        }

        private static Button ActionButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = false,
                Size = new Size(100, 34),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.Click += click;
            return button;
        }

        private static RadioButton MountingChoice(string text, bool isChecked)
        {
            var choice = new RadioButton
            {
                Text = text,
                Checked = isChecked,
                Appearance = Appearance.Button,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Cursor = Cursors.Hand
            };
            choice.FlatAppearance.BorderSize = 1;
            choice.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            return choice;
        }

        private void StyleMountingChoices()
        {
            StyleMountingChoice(_mountBottom);
            StyleMountingChoice(_mountTop);
        }

        private static void StyleMountingChoice(RadioButton choice)
        {
            choice.BackColor = choice.Checked
                ? Color.FromArgb(225, 242, 250)
                : Color.White;
            choice.ForeColor = choice.Checked
                ? Color.FromArgb(0, 91, 135)
                : Color.FromArgb(47, 55, 61);
            choice.FlatAppearance.BorderColor = choice.Checked
                ? Color.FromArgb(0, 120, 184)
                : Color.FromArgb(165, 171, 176);
            choice.FlatAppearance.BorderSize = choice.Checked ? 2 : 1;
        }

        private static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            if (button.Enabled)
            {
                button.BackColor = Color.FromArgb(0, 120, 184);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Color.FromArgb(0, 102, 158);
            }
            else
            {
                button.BackColor = Color.FromArgb(232, 235, 237);
                button.ForeColor = Color.FromArgb(145, 150, 154);
                button.FlatAppearance.BorderColor = Color.FromArgb(211, 215, 218);
            }
        }

        private static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(47, 55, 61);
        }

        private void StyleCombineButton()
        {
            if (!_combine.Enabled)
            {
                _combine.Text = "Combinar sólidos";
                _combine.BackColor = Color.FromArgb(242, 244, 245);
                _combine.ForeColor = Color.FromArgb(145, 150, 154);
                _combine.FlatAppearance.BorderColor = Color.FromArgb(211, 215, 218);
                return;
            }
            if (_combine.Checked)
            {
                _combine.Text = "✓  Combinar sólidos";
                _combine.BackColor = Color.FromArgb(225, 239, 213);
                _combine.ForeColor = Color.FromArgb(52, 92, 24);
                _combine.FlatAppearance.BorderColor = Color.FromArgb(111, 174, 57);
            }
            else
            {
                _combine.Text = "Combinar sólidos";
                _combine.BackColor = Color.White;
                _combine.ForeColor = Color.FromArgb(47, 55, 61);
                _combine.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            }
        }

        private sealed class SolidChoice
        {
            public string Key { get; private set; }
            public string DisplayName { get; private set; }
            public string CadPath { get; private set; }
            public Esprit.Solid Solid { get; private set; }

            public static SolidChoice FromSolid(Esprit.Solid solid)
            {
                string path = string.Empty;
                try { path = solid.CadFile == null ? string.Empty : solid.CadFile.FileName; } catch { }
                string name = string.IsNullOrWhiteSpace(path) ? "Sólido" : Path.GetFileNameWithoutExtension(path);
                return new SolidChoice
                {
                    Key = Convert.ToString(solid.Key),
                    DisplayName = name + " — Sólido " + solid.Number,
                    CadPath = path,
                    Solid = solid
                };
            }
            public override string ToString() => DisplayName;
        }

        private enum SelectionRole
        {
            Part,
            Stock,
            Fixtures
        }

        private enum MountDestination
        {
            CurrentConfiguration = 0,
            NewConfiguration = 1
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_documentGroup != null)
            {
                try { _documentGroup.OnAdd -= DocumentGroupOnAdd; } catch { }
                try { _documentGroup.OnRemove -= DocumentGroupOnRemove; } catch { }
            }
            _selectionRemovalTimer.Stop();
            _selectionRemovalTimer.Tick -= SelectionRemovalTimerOnTick;
            _selectionRemovalTimer.Dispose();
            base.OnFormClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
