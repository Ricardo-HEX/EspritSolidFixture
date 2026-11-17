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
        private static readonly Color SelectionBackground = Color.FromArgb(236, 249, 250);
        private static readonly Color EspritBlue = Color.FromArgb(0, 120, 184);
        private static readonly Color EspritGreen = Color.FromArgb(111, 174, 57);

        private readonly Esprit.Application _app;
        private readonly List<SolidChoice> _fixtures = new List<SolidChoice>();
        private SolidChoice _part;
        private SolidChoice _stock;
        private readonly ListBox _partBox;
        private readonly ListBox _stockBox;
        private readonly ListBox _fixtureBox;
        private readonly RadioButton _stockEsprit;
        private readonly RadioButton _stockSolid;
        private readonly RadioButton _stockFile;
        private readonly Panel _stockContext;
        private readonly Label _stockGuidance;
        private readonly Control _stockSolidRow;
        private readonly Label _stockFileText;
        private readonly Button _stockBrowse;
        private readonly Button _accept;
        private readonly Label _hint;
        private readonly Label _countLabel;
        private readonly CheckBox _combine;
        private readonly RadioButton _mountBottom;
        private readonly RadioButton _mountTop;
        private readonly RadioButton _destinationCurrent;
        private readonly RadioButton _destinationNew;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 300 };
        private readonly Esprit.Group _documentGroup;
        private readonly Timer _selectionRemovalTimer;
        private readonly HashSet<string> _pendingRemovalKeys = new HashSet<string>();
        private readonly Dictionary<SelectionRole, Button> _selectionButtons =
            new Dictionary<SelectionRole, Button>();
        private SelectionRole? _activeSelectionRole;
        private SelectionRole? _pendingRemovalRole;
        private bool _solidSelectionMode;
        private bool _synchronizingSelection;
        private int? _digitizeEventId;
        private StockMode _stockMode = StockMode.Solid;
        private string _stockFilePath;
        private readonly Timer _partSetupTimer;
        private bool _partSetupWasEditing;
        private PendingMount _pendingPartSetupMount;
        private bool _initializing = true;

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
            ClientSize = new Size(680, 960);
            MinimumSize = new Size(696, 999);
            BackColor = Color.White;

            try
            {
                _documentGroup = ((Esprit.Document)_app.Document).Group as Esprit.Group;
                if (_documentGroup != null)
                {
                    _documentGroup.OnAdd += DocumentGroupOnAdd;
                    _documentGroup.OnRemove += DocumentGroupOnRemove;
                }
                Esprit.Document document = (Esprit.Document)_app.Document;
                document.OnDigitize += DocumentOnDigitize;
                document.OnDigitizeAbort += DocumentOnDigitizeAbort;
            }
            catch { }

            _selectionRemovalTimer = new Timer { Interval = 180 };
            _selectionRemovalTimer.Tick += SelectionRemovalTimerOnTick;

            _partSetupTimer = new Timer { Interval = 300 };
            _partSetupTimer.Tick += PartSetupTimerOnTick;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(20, 14, 20, 12),
                BackColor = Color.White,
                AutoScroll = false
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(IntroductionPanel());

            layout.Controls.Add(Section("Pieza"));
            _partBox = SingleSelectionBox();
            _partBox.AccessibleName = "Sólido de pieza";
            _partBox.MouseDown += (s, e) => ActivateWindowSelection(SelectionRole.Part);
            layout.Controls.Add(SelectionRow(_partBox, SelectionRole.Part));

            layout.Controls.Add(Section("Tipo de bruto"));
            var stockChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 66,
                ColumnCount = 3,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.White
            };
            for (int index = 0; index < 3; index++)
                stockChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            _stockEsprit = StockChoice("Desde ESPRIT", StockMode.Esprit);
            _stockSolid = StockChoice("Sólido", StockMode.Solid);
            _stockFile = StockChoice("Desde archivo", StockMode.File);
            _stockSolid.Checked = true;
            stockChoices.Controls.Add(_stockEsprit, 0, 0);
            stockChoices.Controls.Add(_stockSolid, 1, 0);
            stockChoices.Controls.Add(_stockFile, 2, 0);
            layout.Controls.Add(stockChoices);

            _stockContext = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            _stockGuidance = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(78, 86, 92)
            };
            _stockBox = SingleSelectionBox();
            _stockBox.AccessibleName = "Sólido de bruto";
            _stockBox.MouseDown += (s, e) => ActivateWindowSelection(SelectionRole.Stock);
            _stockSolidRow = SelectionRow(_stockBox, SelectionRole.Stock);
            _stockSolidRow.Dock = DockStyle.Fill;
            _stockSolidRow.Margin = new Padding(0);
            _stockFileText = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = SelectionBackground,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 4, 0),
                AutoEllipsis = true
            };
            _stockBrowse = new Button
            {
                Text = "Examinar…",
                Dock = DockStyle.Right,
                Width = 116,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            _stockBrowse.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            _stockBrowse.Click += BrowseStockFile;
            _stockContext.Controls.Add(_stockGuidance);
            _stockContext.Controls.Add(_stockSolidRow);
            _stockContext.Controls.Add(_stockBrowse);
            _stockContext.Controls.Add(_stockFileText);
            layout.Controls.Add(_stockContext);
            UpdateStockModeUi();

            layout.Controls.Add(Section("Utillajes"));
            _fixtureBox = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = SelectionBackground,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false,
                AccessibleName = "Sólidos de utillaje"
            };
            _fixtureBox.MouseDown += FixtureBoxOnMouseDown;
            _fixtureBox.KeyDown += FixtureBoxOnKeyDown;
            var fixtureMenu = new ContextMenuStrip();
            fixtureMenu.Items.Add("Eliminar", CreateDeleteIcon(), RemoveSelectedFixtures);
            _fixtureBox.ContextMenuStrip = fixtureMenu;
            layout.Controls.Add(SelectionRow(_fixtureBox, SelectionRole.Fixtures));

            _countLabel = new Label
            {
                Text = "0 sólidos seleccionados",
                Dock = DockStyle.Top,
                Height = 26,
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
                Height = 36,
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Margin = new Padding(0, 2, 0, 8),
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
                Height = 72,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 8),
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
            var destinationChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _destinationCurrent = MountingChoice("Configuración actual\r\nUsar el montaje activo", true);
            _destinationNew = MountingChoice("Nueva configuración\r\nCrear la siguiente fase", false);
            _destinationCurrent.Margin = new Padding(0, 0, 6, 0);
            _destinationNew.Margin = new Padding(6, 0, 0, 0);
            _destinationCurrent.CheckedChanged += DestinationOnCheckedChanged;
            _destinationNew.CheckedChanged += DestinationOnCheckedChanged;
            destinationChoices.Controls.Add(_destinationCurrent, 0, 0);
            destinationChoices.Controls.Add(_destinationNew, 1, 0);
            layout.Controls.Add(destinationChoices);
            StyleMountingChoice(_destinationCurrent);
            StyleMountingChoice(_destinationNew);

            _hint = new Label
            {
                Text = "Recuadro: selección por ventana   •   Flecha: selección de sólido",
                Dock = DockStyle.Top,
                Height = 40,
                ForeColor = Color.FromArgb(78, 86, 92),
                BackColor = Color.FromArgb(246, 248, 249),
                Padding = new Padding(10, 8, 8, 6),
                Margin = new Padding(0, 0, 0, 4)
            };
            layout.Controls.Add(_hint);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                Padding = new Padding(18, 14, 18, 10),
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

            _initializing = false;
            UpdateStockModeUi();
            RefreshState();

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
                ActivateWindowSelection(SelectionRole.Fixtures);
        }

        private Control SelectionRow(Control selectionBox, SelectionRole role)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = selectionBox.Height,
                ColumnCount = 2,
                Margin = selectionBox.Margin,
                Padding = new Padding(0),
                BackColor = Color.White
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));

            selectionBox.Dock = DockStyle.Fill;
            selectionBox.Margin = new Padding(0, 0, 4, 0);
            var arrow = new Button
            {
                Image = CreateSelectionCursorIcon(),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(26, 26),
                Margin = new Padding(2, 1, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                TabStop = true,
                AccessibleName = "Seleccionar sólido completo"
            };
            arrow.FlatAppearance.BorderSize = 0;
            arrow.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 239, 167);
            arrow.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 224, 102);
            arrow.Click += (s, e) => ActivateSolidSelection(role);
            arrow.Enter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            arrow.Leave += (s, e) => ShowNativeSelectionButtonBorder((Button)s, false);
            arrow.MouseEnter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            arrow.MouseLeave += (s, e) =>
            {
                var button = (Button)s;
                if (!button.Focused && (!_activeSelectionRole.HasValue ||
                    _activeSelectionRole.Value != role || !_solidSelectionMode))
                    ShowNativeSelectionButtonBorder(button, false);
            };
            _toolTip.SetToolTip(arrow,
                "Selección individual: elige un sólido completo en la vista");

            row.Controls.Add(selectionBox, 0, 0);
            row.Controls.Add(arrow, 1, 0);
            _selectionButtons[role] = arrow;
            return row;
        }

        private RadioButton StockChoice(string text, StockMode mode)
        {
            var choice = new RadioButton
            {
                Text = text,
                Tag = mode,
                Image = CreateStockModeIcon(mode),
                TextImageRelation = TextImageRelation.ImageAboveText,
                ImageAlign = ContentAlignment.MiddleCenter,
                Appearance = Appearance.Button,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Margin = new Padding(mode == StockMode.Esprit ? 0 : 4, 0,
                    mode == StockMode.File ? 0 : 4, 0),
                Cursor = Cursors.Hand
            };
            choice.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            choice.CheckedChanged += StockChoiceOnCheckedChanged;
            return choice;
        }

        private void StockChoiceOnCheckedChanged(object sender, EventArgs e)
        {
            var choice = sender as RadioButton;
            if (choice == null) return;
            choice.BackColor = choice.Checked
                ? Color.FromArgb(225, 242, 250)
                : Color.White;
            choice.ForeColor = choice.Checked
                ? Color.FromArgb(0, 91, 135)
                : Color.FromArgb(47, 55, 61);
            choice.FlatAppearance.BorderColor = choice.Checked
                ? EspritBlue
                : Color.FromArgb(165, 171, 176);
            choice.FlatAppearance.BorderSize = choice.Checked ? 2 : 1;
            if (!choice.Checked || !(choice.Tag is StockMode mode)) return;
            _stockMode = mode;
            if (_initializing) return;
            UpdateStockModeUi();
            RefreshState();
        }

        private void UpdateStockModeUi()
        {
            if (_stockGuidance == null) return;
            bool solid = _stockMode == StockMode.Solid;
            bool file = _stockMode == StockMode.File;
            _stockGuidance.Visible = !solid && !file;
            _stockSolidRow.Visible = solid;
            _stockFileText.Visible = file;
            _stockBrowse.Visible = file;

            if (_stockMode == StockMode.Esprit)
                _stockGuidance.Text =
                    "Al aceptar, cree el bruto que necesite en Configuración de la pieza.";
            else if (file)
                _stockFileText.Text = string.IsNullOrWhiteSpace(_stockFilePath)
                    ? "Ningún archivo STL seleccionado"
                    : _stockFilePath;

            if (solid) _stockSolidRow.BringToFront();
            else if (file)
            {
                _stockBrowse.BringToFront();
                _stockFileText.BringToFront();
            }
            else _stockGuidance.BringToFront();
        }

        private void BrowseStockFile(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo STL para el bruto",
                Filter = "Archivos STL (*.stl)|*.stl|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _stockFilePath = dialog.FileName;
                UpdateStockModeUi();
                RefreshState();
            }
        }

        private static void ShowNativeSelectionButtonBorder(Button button, bool visible)
        {
            button.FlatAppearance.BorderSize = visible ? 1 : 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(218, 174, 49);
            button.BackColor = visible ? Color.FromArgb(255, 239, 167) : Color.White;
        }

        private void ActivateWindowSelection(SelectionRole role)
        {
            ActivateSelection(role, false);
        }

        private void ActivateSolidSelection(SelectionRole role)
        {
            ActivateSelection(role, true);
        }

        private void ActivateSelection(SelectionRole role, bool solidOnly)
        {
            Esprit.Document document;
            try { document = (Esprit.Document)_app.Document; }
            catch { return; }
            if (document == null) return;

            _activeSelectionRole = role;
            _solidSelectionMode = solidOnly;
            foreach (KeyValuePair<SelectionRole, Button> entry in _selectionButtons)
                ShowNativeSelectionButtonBorder(entry.Value,
                    solidOnly && entry.Key == role);
            _selectionRemovalTimer.Stop();
            _pendingRemovalKeys.Clear();
            _pendingRemovalRole = null;
            _partBox.BackColor = role == SelectionRole.Part ? SelectionOrange : SelectionBackground;
            _stockBox.BackColor = role == SelectionRole.Stock ? SelectionOrange : SelectionBackground;
            _fixtureBox.BackColor = role == SelectionRole.Fixtures ? SelectionOrange : SelectionBackground;

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

            _hint.Text = solidOnly
                ? "Selección de sólidos activa. Haz clic sobre un sólido."
                : "Selección por ventana activa. Arrastra una ventana en la vista.";
            ActivatePartView();
            if (solidOnly)
                EnableSolidDigitizing(document);
            else
                DisableSolidDigitizing(document);
            document.Refresh();
        }

        private void EnableSolidDigitizing(Esprit.Document document)
        {
            DisableSolidDigitizing(document);
            try
            {
                object[] mask =
                {
                    EspritConstants.espDigitizeType.espDigitizeSolidModel,
                    EspritConstants.espDigitizeType.espDigitizeAllowBoxing
                };
                _digitizeEventId = document.EnableDigitizeEvents(
                    "Selecciona uno o varios sólidos", mask);
            }
            catch
            {
                _digitizeEventId = null;
            }
        }

        private void DisableSolidDigitizing(Esprit.Document document)
        {
            if (!_digitizeEventId.HasValue || document == null) return;
            try { document.DisableDigitizeEvents(_digitizeEventId.Value); } catch { }
            _digitizeEventId = null;
        }

        private void DocumentOnDigitize(object elementObject, object parentObject,
            int graphicObjectIndex, int itemIndex)
        {
            if (!_activeSelectionRole.HasValue || IsDisposed) return;

            Esprit.Solid solid = ResolveOwningSolid(elementObject) ?? ResolveOwningSolid(parentObject);
            if (solid == null) return;

            AddSelectedSolids(new[] { SolidChoice.FromSolid(solid) });
        }

        private void DocumentOnDigitizeAbort()
        {
            _digitizeEventId = null;
            if (!IsDisposed)
                _hint.Text = "Selección finalizada. Haz clic en un recuadro para reactivarla.";
        }

        private void DocumentGroupOnAdd(ref Array objects, object digitizedInfo)
        {
            if (_synchronizingSelection || _solidSelectionMode ||
                !_activeSelectionRole.HasValue || IsDisposed) return;

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

            AddSelectedSolids(additions);
            if (additions.Count == 0)
                _hint.Text = "La selección no contenía sólidos completos.";
        }

        private void AddSelectedSolids(IReadOnlyCollection<SolidChoice> additions)
        {
            if (!_activeSelectionRole.HasValue || additions == null) return;

            SelectionRole role = _activeSelectionRole.Value;
            int added = 0;
            int removed = 0;
            if (role == SelectionRole.Part && additions.Count > 0)
            {
                SolidChoice selected = additions.First();
                if (_part != null && _part.Key == selected.Key)
                {
                    _part = null;
                    removed = 1;
                }
                else
                {
                    _part = selected;
                    added = 1;
                }
            }
            else if (role == SelectionRole.Stock && additions.Count > 0)
            {
                SolidChoice selected = additions.First();
                if (_stock != null && _stock.Key == selected.Key)
                {
                    _stock = null;
                    removed = 1;
                }
                else
                {
                    _stock = selected;
                    added = 1;
                }
            }
            else if (role == SelectionRole.Fixtures)
                foreach (SolidChoice solid in additions)
                {
                    int existingIndex = _fixtures.FindIndex(x => x.Key == solid.Key);
                    if (existingIndex >= 0)
                    {
                        _fixtures.RemoveAt(existingIndex);
                        removed++;
                    }
                    else
                    {
                        _fixtures.Add(solid);
                        added++;
                    }
                }

            RefreshState();
            try { SynchronizeGraphicSelection((Esprit.Document)_app.Document); } catch { }
            if (additions.Count == 0)
                _hint.Text = "La selección no contenía sólidos completos.";
            else if (added > 0 && removed > 0)
                _hint.Text = added + " añadido(s) y " + removed + " retirado(s).";
            else if (removed > 0)
                _hint.Text = removed == 1
                    ? "1 sólido retirado de la selección."
                    : removed + " sólidos retirados de la selección.";
            else
                _hint.Text = added == 1
                    ? "1 sólido añadido."
                    : added + " sólidos añadidos.";
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
            if (_synchronizingSelection || _solidSelectionMode ||
                !_activeSelectionRole.HasValue || IsDisposed) return;

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

        private void DestinationOnCheckedChanged(object sender, EventArgs e)
        {
            StyleMountingChoice(_destinationCurrent);
            StyleMountingChoice(_destinationNew);
            if (_destinationNew.Checked)
            {
                _mountTop.Checked = true;
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

        private int GetAutomaticPhaseNumber()
        {
            return _destinationNew.Checked
                ? GetNextConfigurationNumber()
                : 1;
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
            RefreshSingleBox(_partBox, _part);
            RefreshSingleBox(_stockBox, _stock);

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
            bool reusePreviousPart = CanReusePreviousPart() && _part == null && _stock == null;
            bool stockReady = _stockMode == StockMode.Esprit ||
                              (_stockMode == StockMode.Solid && _stock != null) ||
                              (_stockMode == StockMode.File && File.Exists(_stockFilePath));
            bool definePartAndStock = _part != null && stockReady;
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
            bool stockReady = _stockMode == StockMode.Esprit ||
                              (_stockMode == StockMode.Solid && _stock != null) ||
                              (_stockMode == StockMode.File && File.Exists(_stockFilePath));
            bool definePartAndStock = _part != null && stockReady;
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
                    orderedFixtures, folder, GetAutomaticPhaseNumber(),
                    _combine.Checked, _mountTop.Checked);
            }
            catch (Exception ex)
            {
                ShowStageError("crear los archivos GDML de utillaje", ex);
                return;
            }

            var pending = new PendingMount
            {
                Paths = paths,
                CreateNewConfiguration = _destinationNew.Checked,
                RotateAssembly = _mountTop.Checked,
                MountingStackHeight = mountingStackHeight,
                WorkOffsetBaseZ = workOffsetBaseZ
            };
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                var assembler = new EspritSetupAssembler(document);
                if (reusePreviousPart)
                    pending.WorkpieceKey = assembler.GetExistingWorkpieceKey();
                else if (_stockMode == StockMode.Solid)
                    pending.WorkpieceKey = assembler.ConfigurePartAndStock(_part.Solid, _stock.Solid);
                else if (_stockMode == StockMode.File)
                    pending.WorkpieceKey = assembler.ConfigurePartAndFileStock(
                        _part.Solid, _stockFilePath);
                else
                {
                    pending.WorkpieceKey = assembler.ConfigurePartOnly(_part.Solid);
                    BeginNativeStockDefinition(pending);
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowStageError("actualizar la pieza y el bruto", ex);
                return;
            }

            CompleteMount(pending);
        }

        private void CompleteMount(PendingMount pending)
        {
            try
            {
                var document = (Esprit.Document)_app.Document;
                var assembler = new EspritSetupAssembler(document);
                assembler.MountFixturesAndWorkpiece(
                    pending.Paths, pending.WorkpieceKey, pending.CreateNewConfiguration,
                    pending.RotateAssembly, pending.MountingStackHeight,
                    pending.WorkOffsetBaseZ);
            }
            catch (Exception ex)
            {
                ShowStageError("montar el utillaje y la pieza en la máquina", ex);
                return;
            }
            DialogResult = DialogResult.OK;
            // El montaje puede finalizar desde un Tick. Se difiere el cierre
            // hasta salir del evento para que WinForms destruya bien la ventana.
            BeginInvoke(new Action(Close));
        }

        private void BeginNativeStockDefinition(PendingMount pending)
        {
            Hide();
            try
            {
                // El cliente elige libremente Bloque, Barra, Sólido o Archivo
                // dentro del editor nativo. RunCommand devuelve el control al
                // iniciar el editor; un temporizador espera a que el usuario salga.
                _pendingPartSetupMount = pending;
                _partSetupWasEditing = false;
                _app.RunCommand("Setup.Part");
                try
                {
                    _partSetupWasEditing =
                        ((Esprit.Document)_app.Document).PartSetup.IsEditing;
                }
                catch { }
                _partSetupTimer.Start();
            }
            catch (Exception ex)
            {
                _partSetupTimer.Stop();
                _pendingPartSetupMount = null;
                try
                {
                    if (_app.Document == null || IsDisposed) return;
                }
                catch { return; }
                Show();
                Activate();
                ShowStageError("abrir o completar la definición del bruto", ex);
            }
        }

        private void PartSetupTimerOnTick(object sender, EventArgs e)
        {
            if (_pendingPartSetupMount == null)
            {
                _partSetupTimer.Stop();
                return;
            }

            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                if (document == null)
                {
                    _partSetupTimer.Stop();
                    _pendingPartSetupMount = null;
                    return;
                }

                if (document.PartSetup.IsEditing)
                {
                    _partSetupWasEditing = true;
                    return;
                }
                if (!_partSetupWasEditing) return;

                _partSetupTimer.Stop();
                PendingMount pending = _pendingPartSetupMount;
                _pendingPartSetupMount = null;
                bool stockCreated = document.Workpieces.Count > 0 &&
                                    document.Workpieces[1].Stocks.Count > 0;
                if (!stockCreated)
                {
                    Show();
                    Activate();
                    _hint.Text =
                        "No se creó ningún bruto en ESPRIT. Puede intentarlo de nuevo.";
                    return;
                }
                CompleteMount(pending);
            }
            catch (Exception ex)
            {
                _partSetupTimer.Stop();
                _pendingPartSetupMount = null;
                try
                {
                    if (_app.Document == null || IsDisposed) return;
                }
                catch { return; }
                Show();
                Activate();
                ShowStageError("completar la definición del bruto", ex);
            }
        }

        private void ShowStageError(string stage, Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo " + stage + ":\r\n" + exception.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private bool CanReusePreviousPart()
        {
            return _destinationNew.Checked;
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

        private static Control IntroductionPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.FromArgb(247, 249, 250),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 8, 10, 7)
            };
            panel.Controls.Add(new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = EspritGreen
            });
            panel.Controls.Add(new Label
            {
                Text = "Prepare el conjunto para el montaje",
                Dock = DockStyle.Top,
                Height = 23,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 45, 52)
            });
            panel.Controls.Add(new Label
            {
                Text = "Seleccione pieza, bruto y uno o varios sólidos de utillaje.",
                Dock = DockStyle.Bottom,
                Height = 21,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Color.FromArgb(82, 91, 98)
            });
            return panel;
        }

        private static Bitmap CreateSelectionCursorIcon()
        {
            var bitmap = new Bitmap(16, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var fill = new SolidBrush(Color.FromArgb(42, 49, 54)))
            using (var outline = new Pen(Color.White, 1.2F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                PointF[] cursor =
                {
                    new PointF(2, 1), new PointF(2.5F, 14), new PointF(6, 10.8F),
                    new PointF(8.7F, 16.5F), new PointF(11.2F, 15.3F),
                    new PointF(8.4F, 9.8F), new PointF(13.5F, 9.2F)
                };
                graphics.FillPolygon(fill, cursor);
                graphics.DrawPolygon(outline, cursor);
            }
            return bitmap;
        }

        private static Bitmap CreateStockModeIcon(StockMode mode)
        {
            var bitmap = new Bitmap(28, 24);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var gray = new SolidBrush(Color.FromArgb(170, 176, 180)))
            using (var dark = new Pen(Color.FromArgb(105, 113, 119), 1.2F))
            using (var yellow = new SolidBrush(Color.FromArgb(239, 196, 67)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (mode == StockMode.Esprit)
                {
                    graphics.FillRectangle(gray, 5, 6, 17, 14);
                    graphics.DrawRectangle(dark, 5, 6, 17, 14);
                    graphics.DrawLine(dark, 5, 6, 10, 2);
                    graphics.DrawLine(dark, 22, 6, 17, 2);
                    graphics.DrawLine(dark, 10, 2, 17, 2);
                }
                else if (mode == StockMode.Solid)
                {
                    graphics.FillRectangle(gray, 3, 10, 10, 10);
                    graphics.FillRectangle(gray, 15, 10, 10, 10);
                    graphics.FillRectangle(gray, 9, 2, 10, 10);
                    graphics.DrawRectangle(dark, 3, 10, 10, 10);
                    graphics.DrawRectangle(dark, 15, 10, 10, 10);
                    graphics.DrawRectangle(dark, 9, 2, 10, 10);
                }
                else
                {
                    graphics.FillRectangle(yellow, 3, 7, 22, 14);
                    graphics.FillRectangle(yellow, 5, 3, 9, 6);
                    graphics.DrawRectangle(dark, 3, 7, 22, 14);
                    graphics.DrawLine(dark, 5, 3, 14, 3);
                    graphics.DrawLine(dark, 14, 3, 17, 7);
                }
                using (var plus = new Pen(Color.FromArgb(67, 161, 45), 2F))
                {
                    graphics.DrawLine(plus, 23, 15, 23, 23);
                    graphics.DrawLine(plus, 19, 19, 27, 19);
                }
            }
            return bitmap;
        }

        private static Label Section(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = HeaderGray,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(9, 0, 0, 0),
            Margin = new Padding(0, 7, 0, 4),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        private static ListBox SingleSelectionBox()
        {
            var box = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = SelectionBackground,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            return box;
        }

        private static void RefreshSingleBox(ListBox box, SolidChoice choice)
        {
            box.BeginUpdate();
            box.Items.Clear();
            if (choice != null) box.Items.Add(choice.DisplayName);
            box.EndUpdate();
        }

        private static Button ActionButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = false,
                Size = new Size(108, 36),
                Margin = new Padding(10, 0, 0, 0),
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
                ? EspritBlue
                : Color.FromArgb(165, 171, 176);
            choice.FlatAppearance.BorderSize = choice.Checked ? 2 : 1;
        }

        private static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            if (button.Enabled)
            {
                button.BackColor = EspritBlue;
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

        private enum StockMode
        {
            Esprit,
            Solid,
            File
        }

        private sealed class PendingMount
        {
            public IReadOnlyList<string> Paths { get; set; }
            public string WorkpieceKey { get; set; }
            public bool CreateNewConfiguration { get; set; }
            public bool RotateAssembly { get; set; }
            public double MountingStackHeight { get; set; }
            public double WorkOffsetBaseZ { get; set; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                DisableSolidDigitizing(document);
                document.OnDigitize -= DocumentOnDigitize;
                document.OnDigitizeAbort -= DocumentOnDigitizeAbort;
            }
            catch { }
            if (_documentGroup != null)
            {
                try { _documentGroup.OnAdd -= DocumentGroupOnAdd; } catch { }
                try { _documentGroup.OnRemove -= DocumentGroupOnRemove; } catch { }
            }
            _selectionRemovalTimer.Stop();
            _selectionRemovalTimer.Tick -= SelectionRemovalTimerOnTick;
            _selectionRemovalTimer.Dispose();
            _partSetupTimer.Stop();
            _partSetupTimer.Tick -= PartSetupTimerOnTick;
            _partSetupTimer.Dispose();
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
