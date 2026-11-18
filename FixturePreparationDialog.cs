using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        private readonly Control _stockSection;
        private readonly Control _stockChoices;
        private readonly Panel _stockContext;
        private readonly Label _stockGuidance;
        private readonly Control _stockSolidRow;
        private readonly Control _stockFileRow;
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
        private readonly RadioButton _destinationFile;
        private readonly Panel _newFilePanel;
        private Label _machinePathText;
        private TextBox _espritFileName;
        private string _newOutputDirectory;
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
        private string _newMachinePath;
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

            _stockSection = Section("Tipo de bruto");
            layout.Controls.Add(_stockSection);
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
            _stockChoices = stockChoices;
            layout.Controls.Add(stockChoices);

            _stockContext = new Panel
            {
                Dock = DockStyle.Top,
                // Igual que las filas de Pieza y Utillajes. La altura anterior
                // recortaba el borde inferior del cuadro de selección.
                Height = 46,
                Margin = new Padding(3, 0, 3, 3),
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
                Image = CreateFileBrowseIcon(),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(26, 26),
                Margin = new Padding(2, 1, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                TabStop = true,
                AccessibleName = "Seleccionar archivo STL"
            };
            _stockBrowse.FlatAppearance.BorderSize = 0;
            _stockBrowse.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 239, 167);
            _stockBrowse.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 224, 102);
            _stockBrowse.Click += BrowseStockFile;
            _stockBrowse.Enter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            _stockBrowse.Leave += (s, e) => ShowNativeSelectionButtonBorder((Button)s, false);
            _stockBrowse.MouseEnter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            _stockBrowse.MouseLeave += (s, e) =>
            {
                var button = (Button)s;
                if (!button.Focused) ShowNativeSelectionButtonBorder(button, false);
            };
            _toolTip.SetToolTip(_stockBrowse, "Seleccionar un archivo STL");
            var stockFileRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 46,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.White
            };
            stockFileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stockFileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            _stockFileText.Dock = DockStyle.Fill;
            _stockFileText.Margin = new Padding(0, 0, 4, 0);
            stockFileRow.Controls.Add(_stockFileText, 0, 0);
            stockFileRow.Controls.Add(_stockBrowse, 1, 0);
            _stockFileRow = stockFileRow;
            _stockContext.Controls.Add(_stockGuidance);
            _stockContext.Controls.Add(_stockSolidRow);
            _stockContext.Controls.Add(_stockFileRow);
            layout.Controls.Add(_stockContext);

            layout.Controls.Add(Section("Utillajes"));
            _fixtureBox = new ListBox
            {
                Dock = DockStyle.Top,
                // Altura fija: al superar el espacio visible, ListBox muestra
                // su barra vertical sin hacer crecer la ventana.
                Height = 150,
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
                Height = 72,
                ColumnCount = 3,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
            _destinationCurrent = MountingChoice("Configuración principal\r\nMontar en la primera fase", true);
            _destinationNew = MountingChoice("Nueva configuración\r\nCrear la siguiente fase", false);
            _destinationFile = MountingChoice("Nuevo archivo ESPRIT\r\nCrear la siguiente fase", false);
            _destinationCurrent.Margin = new Padding(0, 0, 4, 0);
            _destinationNew.Margin = new Padding(2, 0, 2, 0);
            _destinationFile.Margin = new Padding(4, 0, 0, 0);
            _destinationCurrent.CheckedChanged += DestinationOnCheckedChanged;
            _destinationNew.CheckedChanged += DestinationOnCheckedChanged;
            _destinationFile.CheckedChanged += DestinationOnCheckedChanged;
            destinationChoices.Controls.Add(_destinationCurrent, 0, 0);
            destinationChoices.Controls.Add(_destinationNew, 1, 0);
            destinationChoices.Controls.Add(_destinationFile, 2, 0);
            layout.Controls.Add(destinationChoices);
            StyleMountingChoice(_destinationCurrent);
            StyleMountingChoice(_destinationNew);
            StyleMountingChoice(_destinationFile);

            _newFilePanel = CreateNewFilePanel();
            _newFilePanel.Visible = false;
            layout.Controls.Add(_newFilePanel);

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
                // Los tres botones pierden exactamente 4 px de ancho total.
                // Así quedan iguales y los dos espacios interiores miden 6 px.
                Margin = mode == StockMode.Esprit
                    ? new Padding(0, 0, 4, 0)
                    : mode == StockMode.Solid
                        ? new Padding(2, 0, 2, 0)
                        : new Padding(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            choice.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            choice.CheckedChanged += StockChoiceOnCheckedChanged;
            return choice;
        }

        private Panel CreateNewFilePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(8, 7, 8, 7),
                BackColor = Color.FromArgb(246, 248, 249)
            };
            panel.Controls.Add(new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = EspritGreen
            });

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Color.Transparent
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            fields.Controls.Add(NewFileFieldLabel("Máquina"), 0, 0);
            _machinePathText = new Label
            {
                Text = "Seleccione una máquina .mprj",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(7, 0, 4, 0),
                AutoEllipsis = true,
                Margin = new Padding(0, 2, 4, 2)
            };
            fields.Controls.Add(_machinePathText, 1, 0);
            var browseMachine = NativeBrowseButton("Seleccionar máquina para el nuevo archivo");
            browseMachine.Image = CreateFolderBrowseIcon();
            browseMachine.Click += BrowseNewMachine;
            fields.Controls.Add(browseMachine, 2, 0);

            fields.Controls.Add(NewFileFieldLabel("Nombre ESPRIT"), 0, 1);
            _espritFileName = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 4, 2)
            };
            _espritFileName.TextChanged += (s, e) => RefreshState();
            fields.Controls.Add(_espritFileName, 1, 1);
            var browseOutput = NativeBrowseButton("Cambiar carpeta del nuevo archivo ESPRIT");
            browseOutput.Image = CreateFolderBrowseIcon();
            browseOutput.Click += BrowseNewOutputDirectory;
            fields.Controls.Add(browseOutput, 2, 1);

            panel.Controls.Add(fields);
            return panel;
        }

        private static Label NewFileFieldLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        private Button NativeBrowseButton(string tooltip)
        {
            var button = new Button
            {
                Image = CreateFileBrowseIcon(),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(26, 26),
                Margin = new Padding(2, 3, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                TabStop = true,
                AccessibleName = tooltip
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 239, 167);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 224, 102);
            button.Enter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            button.Leave += (s, e) => ShowNativeSelectionButtonBorder((Button)s, false);
            button.MouseEnter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            button.MouseLeave += (s, e) =>
            {
                var hovered = (Button)s;
                if (!hovered.Focused) ShowNativeSelectionButtonBorder(hovered, false);
            };
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private void BrowseNewMachine(object sender, EventArgs e)
        {
            string machineFolder = string.Empty;
            try
            {
                machineFolder = _app.Configuration.GetFileDirectory(
                    EspritConstants.espFileType.espFileTypeMachineSetup);
            }
            catch { }

            using (var dialog = new OpenFileDialog
            {
                Title = "Seleccionar máquina para el nuevo archivo ESPRIT",
                Filter = "Máquinas ESPRIT (*.mprj)|*.mprj|Todos los archivos (*.*)|*.*",
                InitialDirectory = Directory.Exists(machineFolder) ? machineFolder : string.Empty,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _newMachinePath = dialog.FileName;
                _machinePathText.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                _toolTip.SetToolTip(_machinePathText, dialog.FileName);
                RefreshState();
            }
        }

        private void BrowseNewOutputDirectory(object sender, EventArgs e)
        {
            string initialDirectory = GetNewOutputDirectory();
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Seleccione la carpeta del nuevo archivo ESPRIT",
                SelectedPath = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _newOutputDirectory = dialog.SelectedPath;
                _toolTip.SetToolTip(_espritFileName,
                    Path.Combine(_newOutputDirectory, _espritFileName.Text.Trim() + ".esprit"));
                RefreshState();
            }
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
            _stockFileRow.Visible = file;

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
                _stockFileRow.BringToFront();
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
            StyleMountingChoice(_destinationFile);
            bool newFile = _destinationFile.Checked;
            _stockSection.Visible = !newFile;
            _stockChoices.Visible = !newFile;
            _stockContext.Visible = !newFile;
            _newFilePanel.Visible = newFile;
            if (_destinationNew.Checked || newFile)
            {
                _mountTop.Checked = true;
            }
            if (newFile && string.IsNullOrWhiteSpace(_espritFileName.Text))
                _espritFileName.Text = GetDefaultEspritFileName(GetNextConfigurationNumber());
            _destinationFile.Text = (newFile ? "✓  " : string.Empty) +
                "Nuevo archivo ESPRIT\r\nCrear Fase " + GetNextConfigurationNumber();
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
            return _destinationNew.Checked || _destinationFile.Checked
                ? GetNextConfigurationNumber()
                : 1;
        }

        private string GetDefaultEspritFileName(int phase)
        {
            string baseName = "Pieza";
            try
            {
                string fileName = ((Esprit.Document)_app.Document).FileName;
                if (!string.IsNullOrWhiteSpace(fileName))
                    baseName = Path.GetFileNameWithoutExtension(fileName);
            }
            catch { }
            baseName = Regex.Replace(baseName, @"_Fase_\d+$", string.Empty,
                RegexOptions.IgnoreCase);
            return SanitizeFileName(baseName) + "_Fase_" + phase;
        }

        private string GetNewEspritPath()
        {
            if (!_destinationFile.Checked || _espritFileName == null) return null;
            string directory = GetNewOutputDirectory();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;
            string name = SanitizeFileName(Path.GetFileNameWithoutExtension(
                _espritFileName.Text.Trim()));
            return string.IsNullOrWhiteSpace(name)
                ? null
                : Path.Combine(directory, name + ".esprit");
        }

        private string GetNewOutputDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_newOutputDirectory))
                return _newOutputDirectory;
            try
            {
                string currentPath = ((Esprit.Document)_app.Document).FileName;
                return Path.GetDirectoryName(currentPath);
            }
            catch { return null; }
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
            bool newFileReady = _destinationFile.Checked &&
                                (_part != null || HasExistingPart()) &&
                                File.Exists(_newMachinePath) &&
                                GetNewEspritPath() != null;
            _accept.Enabled = (_destinationFile.Checked
                    ? newFileReady
                    : reusePreviousPart || definePartAndStock) &&
                _fixtures.Count > 0 && folder != null;

            if (_destinationFile.Checked && string.IsNullOrWhiteSpace(_newMachinePath))
                _hint.Text = "Seleccione la máquina que se cargará en el nuevo archivo ESPRIT.";
            else if (_destinationFile.Checked && _part == null)
                _hint.Text = "Se reutilizará la pieza actual y el material restante de la última operación.";
            else if (CanReusePreviousPart() && _part == null && _stock == null)
                _hint.Text = "Se reutilizarán la pieza y el bruto de la fase anterior.";
        }

        private bool HasExistingPart()
        {
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                return document.Workpieces.Count > 0 &&
                       document.Workpieces[1].Parts.Count > 0;
            }
            catch { return false; }
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
            bool newFile = _destinationFile.Checked;
            bool newFileReady = newFile && (_part != null || HasExistingPart()) &&
                                File.Exists(_newMachinePath) && GetNewEspritPath() != null;
            if ((newFile ? !newFileReady : !reusePreviousPart && !definePartAndStock) ||
                _fixtures.Count == 0 || folder == null)
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
                WorkOffsetBaseZ = workOffsetBaseZ,
                NewEspritPath = newFile ? GetNewEspritPath() : null,
                NewMachinePath = newFile ? _newMachinePath : null,
                SelectedPart = newFile ? _part?.Solid : null
            };
            if (newFile)
            {
                CompleteMount(pending);
                return;
            }
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                var assembler = new EspritSetupAssembler(document);
                if (reusePreviousPart)
                    assembler.ValidateExistingWorkpiece();
                else if (_stockMode == StockMode.Solid)
                    assembler.ConfigurePartAndStock(_part.Solid, _stock.Solid);
                else if (_stockMode == StockMode.File)
                    assembler.ConfigurePartAndFileStock(_part.Solid, _stockFilePath);
                else
                {
                    assembler.ConfigurePartOnly(_part.Solid);
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
                if (!string.IsNullOrWhiteSpace(pending.NewEspritPath))
                    assembler.CreateNewPhaseFile(
                        pending.Paths, pending.NewEspritPath, pending.NewMachinePath,
                        pending.SelectedPart, pending.RotateAssembly,
                        pending.MountingStackHeight, pending.WorkOffsetBaseZ);
                else
                    assembler.MountFixturesAndWorkpiece(
                        pending.Paths, pending.CreateNewConfiguration,
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
            return _destinationNew.Checked || _destinationFile.Checked;
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

        private static Bitmap CreateFileBrowseIcon()
        {
            var bitmap = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var page = new SolidBrush(Color.FromArgb(116, 125, 131)))
            using (var fold = new SolidBrush(Color.FromArgb(212, 217, 220)))
            using (var cursor = new SolidBrush(Color.FromArgb(42, 49, 54)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillRectangle(page, 2, 2, 11, 13);
                graphics.FillPolygon(fold, new[]
                {
                    new Point(9, 2), new Point(13, 6), new Point(9, 6)
                });
                graphics.FillPolygon(cursor, new[]
                {
                    new Point(10, 8), new Point(17, 13), new Point(14, 14),
                    new Point(16, 17), new Point(14, 18), new Point(12, 15),
                    new Point(10, 17)
                });
            }
            return bitmap;
        }

        private static Bitmap CreateFolderBrowseIcon()
        {
            var bitmap = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var back = new SolidBrush(Color.FromArgb(224, 167, 38)))
            using (var front = new SolidBrush(Color.FromArgb(247, 205, 83)))
            using (var edge = new Pen(Color.FromArgb(177, 128, 27), 1F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillPolygon(back, new[]
                {
                    new Point(1, 4), new Point(7, 4), new Point(9, 6),
                    new Point(17, 6), new Point(17, 15), new Point(1, 15)
                });
                graphics.DrawPolygon(edge, new[]
                {
                    new Point(1, 4), new Point(7, 4), new Point(9, 6),
                    new Point(17, 6), new Point(17, 15), new Point(1, 15)
                });
                graphics.FillPolygon(front, new[]
                {
                    new Point(2, 8), new Point(17, 8), new Point(14, 16),
                    new Point(1, 16)
                });
                graphics.DrawPolygon(edge, new[]
                {
                    new Point(2, 8), new Point(17, 8), new Point(14, 16),
                    new Point(1, 16)
                });
            }
            return bitmap;
        }

        private static Bitmap CreateStockModeIcon(StockMode mode)
        {
            // La altura se mantiene en 22 px para que ImageAboveText no recorte
            // el borde inferior dentro del RadioButton de WinForms.
            var bitmap = new Bitmap(30, 22);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var front = new SolidBrush(Color.FromArgb(178, 183, 186)))
            using (var top = new SolidBrush(Color.FromArgb(220, 223, 224)))
            using (var side = new SolidBrush(Color.FromArgb(137, 145, 150)))
            using (var dark = new Pen(Color.FromArgb(112, 119, 123), 0.9F))
            using (var yellow = new SolidBrush(Color.FromArgb(239, 196, 67)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (mode == StockMode.Esprit)
                {
                    DrawNativeCube(graphics, dark, top, front, side, 3, 2, 16, 15);
                }
                else if (mode == StockMode.Solid)
                {
                    // Dos sólidos y un cursor, siguiendo el comando nativo de ESPRIT.
                    DrawNativeCube(graphics, dark, top, front, side, 2, 8, 10, 10);
                    DrawNativeCube(graphics, dark, top, front, side, 13, 2, 10, 10);
                    using (var cursorFill = new SolidBrush(Color.FromArgb(55, 62, 66)))
                    {
                        var cursor = new[]
                        {
                            new Point(12, 12), new Point(17, 17), new Point(14, 17),
                            new Point(16, 20), new Point(14, 21), new Point(12, 18),
                            new Point(10, 20)
                        };
                        graphics.FillPolygon(cursorFill, cursor);
                    }
                }
                else
                {
                    // Carpeta abierta con todo el contorno visible.
                    graphics.FillPolygon(yellow, new[]
                    {
                        new Point(3, 6), new Point(3, 20), new Point(26, 20),
                        new Point(26, 9), new Point(15, 9), new Point(13, 6)
                    });
                    graphics.DrawPolygon(dark, new[]
                    {
                        new Point(3, 6), new Point(13, 6), new Point(15, 9),
                        new Point(26, 9), new Point(26, 20), new Point(3, 20)
                    });
                    using (var openFill = new SolidBrush(Color.FromArgb(250, 213, 92)))
                    {
                        graphics.FillPolygon(openFill, new[]
                        {
                            new Point(3, 11), new Point(28, 11),
                            new Point(24, 21), new Point(3, 21)
                        });
                    }
                    graphics.DrawPolygon(dark, new[]
                    {
                        new Point(3, 11), new Point(28, 11),
                        new Point(24, 21), new Point(3, 21)
                    });
                }
                using (var plus = new Pen(Color.FromArgb(67, 161, 45), 2F))
                {
                    if (mode != StockMode.File)
                    {
                        graphics.DrawLine(plus, 25, 12, 25, 20);
                        graphics.DrawLine(plus, 21, 16, 29, 16);
                    }
                }
            }
            return bitmap;
        }

        private static void DrawNativeCube(Graphics graphics, Pen outline, Brush top, Brush front,
            Brush side, int x, int y, int width, int height)
        {
            int depth = Math.Max(2, width / 4);
            var topFace = new[]
            {
                new Point(x, y + depth), new Point(x + depth, y),
                new Point(x + width, y), new Point(x + width - depth, y + depth)
            };
            var frontFace = new[]
            {
                new Point(x, y + depth), new Point(x + width - depth, y + depth),
                new Point(x + width - depth, y + height), new Point(x, y + height - depth)
            };
            var sideFace = new[]
            {
                new Point(x + width - depth, y + depth), new Point(x + width, y),
                new Point(x + width, y + height - depth), new Point(x + width - depth, y + height)
            };
            graphics.FillPolygon(top, topFace);
            graphics.FillPolygon(front, frontFace);
            graphics.FillPolygon(side, sideFace);
            graphics.DrawPolygon(outline, topFace);
            graphics.DrawPolygon(outline, frontFace);
            graphics.DrawPolygon(outline, sideFace);
        }

        private static Label Section(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 22,
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
            const string checkPrefix = "✓  ";
            string baseText = choice.Text.StartsWith(checkPrefix, StringComparison.Ordinal)
                ? choice.Text.Substring(checkPrefix.Length)
                : choice.Text;
            choice.Text = choice.Checked ? checkPrefix + baseText : baseText;
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
            public bool CreateNewConfiguration { get; set; }
            public bool RotateAssembly { get; set; }
            public double MountingStackHeight { get; set; }
            public double WorkOffsetBaseZ { get; set; }
            public string NewEspritPath { get; set; }
            public string NewMachinePath { get; set; }
            public Esprit.Solid SelectedPart { get; set; }
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
