using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace EspritSolidFixture
{
    internal sealed class FixturePreparationDialog : Form
    {
        private static readonly Color HeaderGray = Color.FromArgb(225, 231, 235);
        private static readonly Color SelectionOrange = Color.FromArgb(255, 218, 181);
        private static readonly Color SelectionBackground = Color.FromArgb(238, 248, 252);
        private static readonly Color EspritBlue = Color.FromArgb(0, 120, 184);
        private static readonly Color EspritGreen = Color.FromArgb(111, 174, 57);

        private readonly Esprit.Application _app;
        private readonly List<SolidChoice> _fixtures = new List<SolidChoice>();
        private readonly List<MountingElementSpec> _mountingElements =
            new List<MountingElementSpec>();
        private SolidChoice _part;
        private SolidChoice _stock;
        private readonly ListBox _partBox;
        private readonly ListBox _stockBox;
        private readonly ListBox _fixtureBox;
        private readonly TreeView _mountingTree;
        private readonly Panel _fixtureSelectionPanel;
        private readonly Button _fixtureToolbarButton;
        private readonly Control _zeroSection;
        private readonly Panel _zeroEditorPanel;
        private readonly TableLayoutPanel _zeroEditorLayout;
        private readonly Panel _zeroSummaryPanel;
        private readonly Panel _zeroPointRow;
        private readonly Label _zeroSummaryText;
        private readonly RadioButton _zeroAutomatic;
        private readonly RadioButton _zeroManual;
        private readonly Label _zeroPointText;
        private readonly Button _zeroPick;
        private readonly Button _zeroApply;
        private readonly TextBox _gripDepth;
        private readonly Label _gripDepthLabel;
        private readonly TableLayoutPanel _zeroOptions;
        private readonly CheckBox _createWorkPlane;
        private readonly Panel _jawConfigurationPanel;
        private readonly PictureBox _jawPreview;
        private readonly RadioButton _jawInnerPair;
        private readonly RadioButton _jawOuterPair;
        private readonly RadioButton _jawNormal;
        private readonly RadioButton _jawReversed;
        private readonly Control _mountingFaceSection;
        private readonly Control _mountingChoices;
        private readonly Control _destinationSection;
        private readonly Control _destinationChoices;
        private readonly RadioButton _stockBlock;
        private readonly RadioButton _stockBar;
        private readonly RadioButton _stockSolid;
        private readonly RadioButton _stockFile;
        private readonly Control _stockSection;
        private readonly Control _stockChoices;
        private readonly Panel _stockContext;
        private readonly Panel _stockParameters;
        private readonly Button _stockAutoFit;
        private readonly Button _stockApply;
        private readonly Label[] _stockParameterLabels;
        private readonly TextBox[] _stockParameterValues;
        private readonly ComboBox[] _stockAlignments;
        private readonly Label[] _stockMarginLabels;
        private readonly Label[] _stockMarginSecondaryLabels;
        private readonly TextBox[,] _stockMarginValues;
        private readonly TableLayoutPanel[] _stockMarginRows;
        private readonly Control _stockSolidRow;
        private readonly Control _stockFileRow;
        private readonly Label _stockFileText;
        private readonly Button _stockBrowse;
        private readonly Button _accept;
        private readonly Label _validationStatus;
        private readonly Label _hint;
        private readonly Label _countLabel;
        private readonly CheckBox _combine;
        private readonly RadioButton _mountBottom;
        private readonly RadioButton _mountTop;
        private readonly RadioButton _destinationCurrent;
        private readonly RadioButton _destinationNew;
        private readonly RadioButton _destinationFile;
        private readonly Panel _newFilePanel;
        private TextBox _machinePathText;
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
        private bool _updatingStockParameters;
        private int? _digitizeEventId;
        private StockMode _stockMode = StockMode.Block;
        private string _stockFilePath;
        private string _newMachinePath;
        private bool _hasManualZero;
        private string _jawPreviewFile;
        private List<PointF> _jawGeometryPreview = new List<PointF>();
        private double _manualZeroX;
        private double _manualZeroY;
        private double _manualZeroZ;
        private readonly bool _initializing = true;

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
            ClientSize = new Size(680, 1020);
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

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(20, 14, 20, 4),
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
                ColumnCount = 4,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = Color.White
            };
            for (int index = 0; index < 4; index++)
                stockChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _stockBlock = StockChoice("Bloque", StockMode.Block);
            _stockBar = StockChoice("Barra", StockMode.Bar);
            _stockSolid = StockChoice("Sólido", StockMode.Solid);
            _stockFile = StockChoice("Desde archivo", StockMode.File);
            _stockBlock.Checked = true;
            stockChoices.Controls.Add(_stockBlock, 0, 0);
            stockChoices.Controls.Add(_stockBar, 1, 0);
            stockChoices.Controls.Add(_stockSolid, 2, 0);
            stockChoices.Controls.Add(_stockFile, 3, 0);
            _stockChoices = stockChoices;
            layout.Controls.Add(stockChoices);

            _stockContext = new Panel
            {
                Dock = DockStyle.Top,
                // Igual que las filas de Pieza y Utillajes. La altura anterior
                // recortaba el borde inferior del cuadro de selección.
                Height = 46,
                Margin = new Padding(0, 0, 0, 3),
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            _stockParameters = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(246, 248, 249),
                Padding = new Padding(10, 7, 10, 7)
            };
            _stockAutoFit = new Button
            {
                Text = "Ajuste automático",
                Image = CreateAutoFitIcon(),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 4, 0),
                Padding = new Padding(8, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Cursor = Cursors.Hand
            };
            _stockAutoFit.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            _stockAutoFit.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 242, 250);
            _stockAutoFit.Click += AutoFitStock;
            _stockApply = new Button
            {
                Text = "Aplicar definición",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 0, 0),
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Cursor = Cursors.Hand
            };
            _stockApply.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            _stockApply.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 242, 250);
            _stockApply.Click += ApplyStockDefinition;
            var stockActions = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 31,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            stockActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            stockActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            stockActions.Controls.Add(_stockAutoFit, 0, 0);
            stockActions.Controls.Add(_stockApply, 1, 0);
            var parameterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            parameterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
            parameterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            parameterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
            parameterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            parameterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
            parameterGrid.SizeChanged += (sender, args) =>
            {
                int available = Math.Max(0, parameterGrid.ClientSize.Width - 32);
                int columnWidth = available / 3;
                parameterGrid.ColumnStyles[0].Width = columnWidth;
                parameterGrid.ColumnStyles[2].Width = columnWidth;
                parameterGrid.ColumnStyles[4].Width = available - (columnWidth * 2);
            };
            parameterGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _stockParameterLabels = new Label[3];
            _stockParameterValues = new TextBox[3];
            _stockAlignments = new ComboBox[3];
            _stockMarginLabels = new Label[3];
            _stockMarginSecondaryLabels = new Label[3];
            _stockMarginValues = new TextBox[3, 2];
            _stockMarginRows = new TableLayoutPanel[3];
            for (int index = 0; index < 3; index++)
            {
                var axisPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    BackColor = Color.Transparent
                };
                axisPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                axisPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                axisPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                axisPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
                var labelRow = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                labelRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                labelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
                labelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
                labelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0));
                _stockParameterLabels[index] = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(0),
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 55, 61)
                };
                _stockParameterValues[index] = StockDimensionInput();
                _stockParameterValues[index].TabIndex = index;
                _stockParameterValues[index].Margin = new Padding(0);
                _stockParameterValues[index].TextChanged += StockParameterOnChanged;
                WireStockTabNavigation(_stockParameterValues[index]);
                _stockAlignments[index] = StockAlignmentInput();
                _stockAlignments[index].TabIndex = 3 + index;
                _stockAlignments[index].Margin = new Padding(0);
                int axis = index;
                _stockAlignments[index].SelectedIndexChanged += (s, e) =>
                {
                    UpdateStockMarginUi(axis);
                    StockParameterOnChanged(s, e);
                };
                WireStockTabNavigation(_stockAlignments[index]);
                _stockMarginLabels[index] = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(6, 0, 0, 0),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 55, 61)
                };
                _stockMarginSecondaryLabels[index] = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(6, 0, 0, 0),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47, 55, 61)
                };
                _stockMarginRows[index] = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    BackColor = Color.Transparent
                };
                _stockMarginRows[index].RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                _stockMarginRows[index].ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
                _stockMarginRows[index].ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
                _stockMarginRows[index].ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0));
                for (int marginIndex = 0; marginIndex < 2; marginIndex++)
                {
                    TextBox margin = StockDimensionInput();
                    margin.Text = "0,00";
                    margin.TabIndex = index * 2 + marginIndex;
                    margin.Margin = marginIndex == 0
                        ? new Padding(0, 0, 3, 2)
                        : new Padding(3, 0, 0, 2);
                    margin.TextChanged += StockParameterOnChanged;
                    WireStockTabNavigation(margin);
                    _stockMarginValues[index, marginIndex] = margin;
                    _stockMarginRows[index].Controls.Add(margin, marginIndex + 1, 0);
                }
                labelRow.Controls.Add(_stockParameterLabels[index], 0, 0);
                labelRow.Controls.Add(_stockMarginLabels[index], 1, 0);
                labelRow.Controls.Add(_stockMarginSecondaryLabels[index], 2, 0);
                axisPanel.Controls.Add(labelRow, 0, 0);
                _stockMarginRows[index].Controls.Add(_stockParameterValues[index], 0, 0);
                axisPanel.Controls.Add(_stockMarginRows[index], 0, 1);
                axisPanel.Controls.Add(_stockAlignments[index], 0, 2);
                parameterGrid.Controls.Add(axisPanel, index * 2, 0);
            }
            _stockParameters.Controls.Add(parameterGrid);
            _stockParameters.Controls.Add(stockActions);
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
            _stockContext.Controls.Add(_stockParameters);
            _stockContext.Controls.Add(_stockSolidRow);
            _stockContext.Controls.Add(_stockFileRow);
            layout.Controls.Add(_stockContext);

            layout.Controls.Add(Section("Árbol de montaje"));
            var mountingToolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 3),
                BackColor = Color.White
            };
            mountingToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mountingToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var addActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                BackColor = Color.White
            };
            addActions.Controls.Add(TreeAddButton("Accesorio", BrowseAccessory));
            addActions.Controls.Add(TreeAddButton("Garras", BrowseJaws));
            _fixtureToolbarButton = TreeAddButton("Utillaje", ToggleFixtureSelection);
            addActions.Controls.Add(_fixtureToolbarButton);
            var editActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                BackColor = Color.White
            };
            editActions.Controls.Add(TreeIconButton(CreateRemoveIcon(), RemoveMountingElement,
                "Eliminar elemento"));
            editActions.Controls.Add(TreeIconButton(CreateMoveIcon(false), MoveMountingElementDown,
                "Bajar elemento"));
            editActions.Controls.Add(TreeIconButton(CreateMoveIcon(true), MoveMountingElementUp,
                "Subir elemento"));
            mountingToolbar.Controls.Add(addActions, 0, 0);
            mountingToolbar.Controls.Add(editActions, 1, 0);
            layout.Controls.Add(mountingToolbar);

            _mountingTree = new TreeView
            {
                Dock = DockStyle.Top,
                Height = 92,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                HideSelection = false,
                FullRowSelect = true,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                ShowNodeToolTips = true,
                ImageList = CreateMountingTreeImageList(),
                Margin = new Padding(0, 0, 0, 5),
                AccessibleName = "Árbol de montaje"
            };
            _mountingTree.NodeMouseDoubleClick += MountingTreeOnNodeMouseDoubleClick;
            layout.Controls.Add(_mountingTree);

            _fixtureSelectionPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Visible = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.White
            };
            var fixtureSelectionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.White
            };
            fixtureSelectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fixtureSelectionLayout.Controls.Add(Section("Seleccionar utillajes CAD"));
            _fixtureBox = new ListBox
            {
                Dock = DockStyle.Top,
                // Altura fija: al superar el espacio visible, ListBox muestra
                // su barra vertical sin hacer crecer la ventana.
                Height = 104,
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
            fixtureSelectionLayout.Controls.Add(SelectionRow(_fixtureBox, SelectionRole.Fixtures));

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
            fixtureSelectionLayout.Controls.Add(_countLabel);

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
            fixtureSelectionLayout.Controls.Add(_combine);

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
            fixtureSelectionLayout.Controls.Add(_hint);

            var finishFixtureSelection = new Button
            {
                Text = "✓  Terminar selección",
                Dock = DockStyle.Top,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = EspritBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 8),
                AccessibleName = "Terminar selección de utillajes"
            };
            finishFixtureSelection.FlatAppearance.BorderColor = EspritBlue;
            finishFixtureSelection.Click += FinishFixtureSelection;
            fixtureSelectionLayout.Controls.Add(finishFixtureSelection);
            _fixtureSelectionPanel.Controls.Add(fixtureSelectionLayout);
            layout.Controls.Add(_fixtureSelectionPanel);

            _zeroSection = Section("Cero pieza y amarre");
            layout.Controls.Add(_zeroSection);
            _zeroSummaryPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Margin = new Padding(0, 0, 0, 7),
                Padding = new Padding(12, 4, 5, 4),
                BackColor = Color.FromArgb(246, 248, 249)
            };
            _zeroSummaryText = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(78, 86, 92),
                AutoEllipsis = true,
                Text = "Cero automático  •  Plano con nombre del cero  •  Amarre 3,00 mm"
            };
            var editZero = new Button
            {
                Text = "Editar",
                Dock = DockStyle.Right,
                Width = 82,
                Margin = new Padding(4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Cursor = Cursors.Hand
            };
            editZero.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            editZero.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 242, 250);
            editZero.Click += ShowWorkZeroEditor;
            _zeroSummaryPanel.Controls.Add(_zeroSummaryText);
            _zeroSummaryPanel.Controls.Add(editZero);
            layout.Controls.Add(_zeroSummaryPanel);

            _zeroEditorPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(10, 8, 10, 8),
                BackColor = Color.FromArgb(246, 248, 249),
                Visible = false
            };
            _zeroEditorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            _zeroEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _zeroEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _zeroEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            _zeroEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _zeroEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var zeroModeChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            zeroModeChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            zeroModeChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _zeroAutomatic = MountingChoice("Automático", true);
            _zeroManual = MountingChoice("Seleccionar en la pieza", false);
            _zeroAutomatic.Margin = new Padding(0, 0, 4, 0);
            _zeroManual.Margin = new Padding(4, 0, 0, 0);
            _zeroAutomatic.CheckedChanged += WorkZeroModeOnChanged;
            _zeroManual.CheckedChanged += WorkZeroModeOnChanged;
            zeroModeChoices.Controls.Add(_zeroAutomatic, 0, 0);
            zeroModeChoices.Controls.Add(_zeroManual, 1, 0);
            _zeroEditorLayout.Controls.Add(zeroModeChoices, 0, 0);

            _zeroPointRow = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent,
                Visible = false
            };
            var zeroPointLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            zeroPointLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            zeroPointLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            _zeroPointText = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SelectionBackground,
                Text = "Seleccione un punto, vértice o centro",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 4, 0),
                AutoEllipsis = true
            };
            _zeroPointText.Margin = new Padding(0, 0, 4, 0);
            _zeroPick = new Button
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
                AccessibleName = "Seleccionar el cero de pieza en la vista"
            };
            _zeroPick.FlatAppearance.BorderSize = 0;
            _zeroPick.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 239, 167);
            _zeroPick.FlatAppearance.MouseDownBackColor = Color.FromArgb(255, 224, 102);
            _zeroPick.Click += PickWorkZero;
            _zeroPick.Enter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            _zeroPick.Leave += (s, e) => ShowNativeSelectionButtonBorder((Button)s, false);
            _zeroPick.MouseEnter += (s, e) => ShowNativeSelectionButtonBorder((Button)s, true);
            _zeroPick.MouseLeave += (s, e) =>
            {
                var button = (Button)s;
                if (!button.Focused) ShowNativeSelectionButtonBorder(button, false);
            };
            _toolTip.SetToolTip(_zeroPick, "Seleccionar el cero en la vista de pieza");
            zeroPointLayout.Controls.Add(_zeroPointText, 0, 0);
            zeroPointLayout.Controls.Add(_zeroPick, 1, 0);
            _zeroPointRow.Controls.Add(zeroPointLayout);
            _zeroEditorLayout.Controls.Add(_zeroPointRow, 0, 1);

            _zeroOptions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Margin = new Padding(0, 4, 0, 0),
                Padding = new Padding(0)
            };
            _zeroOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            _zeroOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
            _zeroOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _gripDepthLabel = new Label
            {
                Text = "Profundidad de amarre",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            _zeroOptions.Controls.Add(_gripDepthLabel, 0, 0);
            _gripDepth = StockDimensionInput();
            _gripDepth.Text = "3,00";
            _gripDepth.Margin = new Padding(0, 2, 8, 2);
            _gripDepth.TextChanged += (s, e) => RefreshState();
            _zeroOptions.Controls.Add(_gripDepth, 1, 0);
            _createWorkPlane = new CheckBox
            {
                Text = "Crear plano con el nombre del cero",
                Checked = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 0, 0)
            };
            _zeroOptions.Controls.Add(_createWorkPlane, 2, 0);
            _zeroEditorLayout.Controls.Add(_zeroOptions, 0, 2);

            _jawConfigurationPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 7, 0, 3),
                Padding = new Padding(8),
                BackColor = Color.White,
                Visible = false
            };
            var jawLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            jawLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            jawLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            jawLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            jawLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            _jawPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 9, 3),
                BackColor = Color.FromArgb(239, 244, 247),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Normal,
                AccessibleName = "Previsualización superior de las garras"
            };
            _jawPreview.Paint += PaintJawPreview;
            jawLayout.Controls.Add(_jawPreview, 0, 0);

            var jawOptions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(9, 0, 0, 3),
                Padding = new Padding(0)
            };
            jawOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            jawOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            jawOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            jawOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            jawOptions.Controls.Add(new Label
            {
                Text = "Pareja de montaje",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);
            var pairChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            pairChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pairChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _jawInnerPair = CompactChoice("Interior 1–2", true);
            _jawOuterPair = CompactChoice("Exterior 3–4", false);
            _jawInnerPair.CheckedChanged += JawConfigurationChanged;
            _jawOuterPair.CheckedChanged += JawConfigurationChanged;
            pairChoices.Controls.Add(_jawInnerPair, 0, 0);
            pairChoices.Controls.Add(_jawOuterPair, 1, 0);
            jawOptions.Controls.Add(pairChoices, 0, 1);
            jawOptions.Controls.Add(new Label
            {
                Text = "Orientación",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 2);
            var directionChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            directionChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            directionChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _jawNormal = CompactChoice("Normal", true);
            _jawReversed = CompactChoice("Invertida", false);
            _jawNormal.CheckedChanged += JawConfigurationChanged;
            _jawReversed.CheckedChanged += JawConfigurationChanged;
            directionChoices.Controls.Add(_jawNormal, 0, 0);
            directionChoices.Controls.Add(_jawReversed, 1, 0);
            jawOptions.Controls.Add(directionChoices, 0, 3);
            jawLayout.Controls.Add(jawOptions, 1, 0);
            var previewHint = new Label
            {
                Text = "Geometría real · Vista superior · Solo previsualización",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(78, 86, 92),
                Padding = new Padding(4, 0, 0, 0)
            };
            jawLayout.Controls.Add(previewHint, 0, 1);
            jawLayout.SetColumnSpan(previewHint, 2);
            _jawConfigurationPanel.Controls.Add(jawLayout);
            _zeroEditorLayout.Controls.Add(_jawConfigurationPanel, 0, 3);

            _zeroApply = new Button
            {
                Text = "Aplicar definición",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Cursor = Cursors.Hand
            };
            _zeroApply.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            _zeroApply.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 242, 250);
            _zeroApply.Click += ApplyWorkZeroDefinition;
            _zeroEditorLayout.Controls.Add(_zeroApply, 0, 4);
            _zeroEditorPanel.Controls.Add(_zeroEditorLayout);
            layout.Controls.Add(_zeroEditorPanel);

            _mountingFaceSection = Section("Cara de montaje");
            layout.Controls.Add(_mountingFaceSection);
            var mountingChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
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
            _mountBottom.CheckedChanged += MountingFaceOnCheckedChanged;
            _mountTop.CheckedChanged += MountingFaceOnCheckedChanged;
            mountingChoices.Controls.Add(_mountBottom, 0, 0);
            mountingChoices.Controls.Add(_mountTop, 1, 0);
            _mountingChoices = mountingChoices;
            layout.Controls.Add(mountingChoices);
            StyleMountingChoices();

            _destinationSection = Section("Destino del montaje");
            layout.Controls.Add(_destinationSection);
            var destinationChoices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 132,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            destinationChoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            destinationChoices.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            destinationChoices.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _destinationCurrent = MountingChoice("Configuración principal\r\nMontar en la primera fase", true);
            _destinationNew = MountingChoice("Nueva configuración\r\nCrear la siguiente fase", false);
            _destinationFile = MountingChoice("Nuevo archivo ESPRIT\r\nCrear la siguiente fase", false);
            _destinationCurrent.Margin = new Padding(0, 0, 6, 4);
            _destinationNew.Margin = new Padding(6, 0, 0, 4);
            _destinationFile.Margin = new Padding(0, 4, 0, 0);
            _destinationCurrent.CheckedChanged += DestinationOnCheckedChanged;
            _destinationNew.CheckedChanged += DestinationOnCheckedChanged;
            _destinationFile.CheckedChanged += DestinationOnCheckedChanged;
            destinationChoices.Controls.Add(_destinationCurrent, 0, 0);
            destinationChoices.Controls.Add(_destinationNew, 1, 0);
            destinationChoices.Controls.Add(_destinationFile, 0, 1);
            destinationChoices.SetColumnSpan(_destinationFile, 2);
            _destinationChoices = destinationChoices;
            layout.Controls.Add(destinationChoices);
            StyleMountingChoice(_destinationCurrent);
            StyleMountingChoice(_destinationNew);
            StyleMountingChoice(_destinationFile);

            _newFilePanel = CreateNewFilePanel();
            _newFilePanel.Visible = false;
            layout.Controls.Add(_newFilePanel);

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(18, 12, 18, 10),
                BackColor = SystemColors.Control
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 8, 4),
                BackColor = SystemColors.Control
            };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var statusIcon = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = CreateStatusInfoIcon(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Margin = new Padding(0)
            };
            _validationStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(88, 96, 103),
                AutoEllipsis = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                AccessibleName = "Estado de la preparación"
            };
            statusPanel.Controls.Add(statusIcon, 0, 0);
            statusPanel.Controls.Add(_validationStatus, 1, 0);
            var bottomActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _accept = ActionButton("Aceptar", AcceptPreparation);
            _accept.Enabled = false;
            var cancel = ActionButton("Cancelar", CancelPreparation);
            StylePrimaryButton(_accept);
            StyleSecondaryButton(cancel);
            _accept.EnabledChanged += (s, e) => StylePrimaryButton(_accept);
            bottomActions.Controls.Add(_accept);
            bottomActions.Controls.Add(cancel);
            bottom.Controls.Add(statusPanel, 0, 0);
            bottom.Controls.Add(bottomActions, 1, 0);

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
                // Todas las filas siguen la misma retícula que las cabeceras,
                // botones y árbol; la separación se controla fuera del borde.
                Margin = new Padding(0),
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

        private Button TreeActionButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(47, 55, 61),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 225, 245);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(155, 207, 237);
            button.Click += click;
            return button;
        }

        private Button TreeAddButton(string text, EventHandler click)
        {
            Button button = TreeActionButton(text, click);
            button.AutoSize = false;
            button.Width = 108;
            button.Image = CreateSmallAddIcon();
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(0, 1, 0, 0);
            return button;
        }

        private static Bitmap CreateSmallAddIcon()
        {
            // El centro óptico de las letras queda ligeramente por encima del
            // centro geométrico del botón.
            var bitmap = new Bitmap(13, 13);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(67, 161, 45), 1.8F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 6, 1, 6, 9);
                graphics.DrawLine(pen, 2, 5, 10, 5);
            }
            return bitmap;
        }

        private Button TreeIconButton(Image image, EventHandler click, string tooltip)
        {
            Button button = TreeActionButton(string.Empty, click);
            button.Width = 34;
            button.AutoSize = false;
            button.Image = image;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Margin = new Padding(0, 0, 4, 0);
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private static Bitmap CreateMoveIcon(bool upward)
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(39, 72, 92), 1.5F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int tipY = upward ? 3 : 13;
                int baseY = upward ? 7 : 9;
                graphics.DrawLine(pen, 8, upward ? 13 : 3, 8, tipY);
                graphics.DrawLine(pen, 8, tipY, 4, baseY);
                graphics.DrawLine(pen, 8, tipY, 12, baseY);
            }
            return bitmap;
        }

        private static Bitmap CreateRemoveIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(64, 71, 76), 1.6F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 4, 4, 12, 12);
                graphics.DrawLine(pen, 12, 4, 4, 12);
            }
            return bitmap;
        }

        private static ImageList CreateMountingTreeImageList()
        {
            var images = new ImageList
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };
            images.Images.Add("machine", LoadNativeTreeIcon("Setup.Machine.png",
                CreateTreeNodeIcon(Color.FromArgb(108, 168, 199), true)));
            images.Images.Add("accessory", CreateAccessoryTreeIcon());
            images.Images.Add("jaws", CreateJawsTreeIcon());
            images.Images.Add("fixture", CreateFixtureTreeIcon());
            images.Images.Add("part", LoadNativeTreeIcon("Setup.MountWorkpiece.png",
                CreateTreeNodeIcon(Color.FromArgb(112, 174, 207), false)));
            return images;
        }

        private static Bitmap LoadNativeTreeIcon(string fileName, Bitmap fallback)
        {
            try
            {
                string programFolder = Path.GetDirectoryName(
                    System.Windows.Forms.Application.ExecutablePath);
                string installRoot = Directory.GetParent(programFolder ?? string.Empty)?.FullName;
                string path = Path.Combine(installRoot ?? string.Empty,
                    "Help", "ToolTipImages", fileName);
                if (!File.Exists(path)) return fallback;

                using (Image source = Image.FromFile(path))
                {
                    var bitmap = new Bitmap(16, 16);
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode =
                            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode =
                            System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(0, 0, 16, 16));
                    }
                    fallback.Dispose();
                    return bitmap;
                }
            }
            catch { return fallback; }
        }

        private static Bitmap CreateStatusInfoIcon()
        {
            var bitmap = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var circle = new SolidBrush(Color.FromArgb(32, 145, 210)))
            using (var text = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillEllipse(circle, 1, 1, 16, 16);
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                graphics.DrawString("i", font, text, new RectangleF(1, 0, 16, 17), format);
            }
            return bitmap;
        }

        private static Bitmap CreateTreeNodeIcon(Color color, bool machine)
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var front = new SolidBrush(color))
            using (var top = new SolidBrush(ControlPaint.Light(color, 0.35F)))
            using (var side = new SolidBrush(ControlPaint.Dark(color, 0.15F)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Point[] topFace = { new Point(2, 5), new Point(6, 2), new Point(14, 2), new Point(10, 5) };
                Point[] frontFace = { new Point(2, 5), new Point(10, 5), new Point(10, 13), new Point(2, 13) };
                Point[] sideFace = { new Point(10, 5), new Point(14, 2), new Point(14, 10), new Point(10, 13) };
                graphics.FillPolygon(top, topFace);
                graphics.FillPolygon(front, frontFace);
                graphics.FillPolygon(side, sideFace);
                if (machine)
                {
                    using (var window = new SolidBrush(Color.FromArgb(225, 242, 250)))
                        graphics.FillRectangle(window, 4, 7, 4, 3);
                }
            }
            return bitmap;
        }

        private static Bitmap CreateJawsTreeIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var front = new SolidBrush(Color.FromArgb(160, 165, 168)))
            using (var top = new SolidBrush(Color.FromArgb(220, 223, 224)))
            using (var side = new SolidBrush(Color.FromArgb(120, 127, 132)))
            using (var outline = new Pen(Color.FromArgb(102, 109, 113), 0.8F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                DrawNativeCube(graphics, outline, top, front, side, 1, 7, 8, 7);
                DrawNativeCube(graphics, outline, top, front, side, 7, 3, 8, 7);
            }
            return bitmap;
        }

        private static Bitmap CreateAccessoryTreeIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var front = new SolidBrush(Color.FromArgb(165, 170, 173)))
            using (var top = new SolidBrush(Color.FromArgb(224, 226, 227)))
            using (var side = new SolidBrush(Color.FromArgb(121, 128, 132)))
            using (var outline = new Pen(Color.FromArgb(104, 111, 115), 0.8F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                DrawNativeCube(graphics, outline, top, front, side, 1, 3, 14, 12);
            }
            return bitmap;
        }

        private static Bitmap CreateFixtureTreeIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var front = new SolidBrush(Color.FromArgb(158, 164, 168)))
            using (var top = new SolidBrush(Color.FromArgb(222, 225, 226)))
            using (var side = new SolidBrush(Color.FromArgb(116, 124, 129)))
            using (var outline = new Pen(Color.FromArgb(98, 106, 111), 0.8F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                DrawNativeCube(graphics, outline, top, front, side, 1, 7, 14, 8);
                DrawNativeCube(graphics, outline, top, front, side, 4, 2, 9, 8);
            }
            return bitmap;
        }

        private void BrowseAccessory(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing) return;
                string path = BrowseFixtureFile("Seleccionar accesorio de ESPRIT");
                if (path == null) return;
                _mountingElements.Add(new MountingElementSpec(
                    MountingElementType.Accessory, path));
                RefreshState();
            }));
        }

        private void BrowseJaws(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            if (!_mountingElements.Any(x => x.Type == MountingElementType.Accessory))
            {
                MessageBox.Show(this,
                    "Añada primero la mordaza o el accesorio sobre el que se montarán las garras.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing) return;
                string path = BrowseFixtureFile("Seleccionar garras de ESPRIT");
                if (path == null) return;
                string parentAccessory = _mountingElements
                    .LastOrDefault(item => item.Type == MountingElementType.Accessory)?.FileName;
                _mountingElements.Add(new MountingElementSpec(
                    MountingElementType.Jaws, path, parentAccessory));
                _jawInnerPair.Checked = true;
                _jawNormal.Checked = true;
                UpdateJawConfigurationControls();
                RefreshState();
            }));
        }

        private void ToggleFixtureSelection(object sender, EventArgs e)
        {
            bool show = !_fixtureSelectionPanel.Visible;
            if (show)
            {
                EndSelectionMode(true);
                _fixtureSelectionPanel.Visible = true;
                _fixtureToolbarButton.BackColor = Color.FromArgb(232, 246, 252);
                SetPostSelectionControlsVisible(false);
                ActivateWindowSelection(SelectionRole.Fixtures);
                _fixtureSelectionPanel.ScrollControlIntoView(_fixtureBox);
                RefreshState();
            }
            else
                FinishFixtureSelection(sender, e);
        }

        private void FinishFixtureSelection(object sender, EventArgs e)
        {
            EndSelectionMode(true);
            _fixtureSelectionPanel.Visible = false;
            _fixtureToolbarButton.BackColor = Color.White;
            SetPostSelectionControlsVisible(true);
            RefreshState();
        }

        private void PrepareForExtensionCommand()
        {
            EndSelectionMode(true);
            if (!_fixtureSelectionPanel.Visible) return;
            _fixtureSelectionPanel.Visible = false;
            _fixtureToolbarButton.BackColor = Color.White;
            SetPostSelectionControlsVisible(true);
        }

        private void ShowWorkZeroEditor(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            _zeroSummaryPanel.Visible = false;
            _zeroEditorPanel.Visible = true;
            SetMountingOptionsVisible(false);
            UpdateWorkZeroEditorLayout();
            WorkZeroModeOnChanged(sender, e);
            RefreshState();
        }

        private void WorkZeroModeOnChanged(object sender, EventArgs e)
        {
            if (_zeroPointRow == null) return;
            _zeroPointRow.Visible = _zeroManual.Checked;
            UpdateWorkZeroEditorLayout();
            StyleMountingChoice(_zeroAutomatic);
            StyleMountingChoice(_zeroManual);
            RefreshState();
        }

        private void PickWorkZero(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            ActivatePartView();
            Hide();
            try
            {
                Esprit.Document document = (Esprit.Document)_app.Document;
                Esprit.Point point = document.GetPoint(
                    "Seleccione el cero de pieza: punto, vértice o centro");
                if (point != null)
                {
                    _manualZeroX = point.X;
                    _manualZeroY = point.Y;
                    _manualZeroZ = point.Z;
                    _hasManualZero = true;
                    _zeroManual.Checked = true;
                    _zeroPointText.Text = FormatZeroPoint();
                }
            }
            catch (COMException)
            {
                // Escape cancela el selector nativo sin alterar la definición.
            }
            finally
            {
                Show();
                Activate();
                BringToFront();
            }
            RefreshState();
        }

        private void ApplyWorkZeroDefinition(object sender, EventArgs e)
        {
            if (_zeroManual.Checked && !_hasManualZero)
            {
                SetValidationStatus("Seleccione el punto que se usará como cero de pieza.", false);
                return;
            }
            bool hasJaws = HasMountedJaws();
            double gripDepth = 0.0;
            if (hasJaws && !TryGetGripDepth(out gripDepth))
            {
                SetValidationStatus("Indique una profundidad de amarre válida.", false);
                _gripDepth.Focus();
                return;
            }

            string zeroText = _zeroManual.Checked ? FormatZeroPoint() : "Cero automático";
            string planeText = _createWorkPlane.Checked
                ? "Plano con nombre del cero"
                : "Sin plano de trabajo";
            UpdateWorkZeroSummary(zeroText, planeText,
                hasJaws ? (double?)gripDepth : null);
            _zeroEditorPanel.Visible = false;
            _zeroSummaryPanel.Visible = true;
            SetMountingOptionsVisible(true);
            RefreshState();
        }

        private string FormatZeroPoint()
        {
            return string.Format(CultureInfo.CurrentCulture,
                "X {0:0.###}  Y {1:0.###}  Z {2:0.###}",
                _manualZeroX, _manualZeroY, _manualZeroZ);
        }

        private bool TryGetGripDepth(out double value)
        {
            string text = (_gripDepth?.Text ?? string.Empty).Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float,
                CultureInfo.CurrentCulture, out value) ||
                double.TryParse(text.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value);
            return parsed && value >= 0.0;
        }

        private bool HasMountedJaws()
        {
            return _mountingElements.Any(item => item.Type == MountingElementType.Jaws);
        }

        private MountingElementSpec CurrentJawSpec() => _mountingElements
            .LastOrDefault(item => item.Type == MountingElementType.Jaws);

        private void JawConfigurationChanged(object sender, EventArgs e)
        {
            MountingElementSpec jaws = CurrentJawSpec();
            if (jaws == null) return;
            jaws.JawAdapterIndices.Clear();
            if (_jawOuterPair.Checked)
            {
                jaws.JawAdapterIndices.Add(3);
                jaws.JawAdapterIndices.Add(4);
            }
            else
            {
                jaws.JawAdapterIndices.Add(1);
                jaws.JawAdapterIndices.Add(2);
            }
            jaws.ReverseJaws = _jawReversed.Checked;
            StyleMountingChoice(_jawInnerPair);
            StyleMountingChoice(_jawOuterPair);
            StyleMountingChoice(_jawNormal);
            StyleMountingChoice(_jawReversed);
            _jawPreview.Invalidate();
            UpdateWorkZeroSummary();
        }

        private void UpdateJawConfigurationControls()
        {
            MountingElementSpec jaws = CurrentJawSpec();
            bool visible = jaws != null;
            _jawConfigurationPanel.Visible = visible;
            if (!visible)
            {
                _jawPreviewFile = null;
                _jawGeometryPreview.Clear();
                return;
            }
            if (!string.Equals(_jawPreviewFile, jaws.FileName,
                StringComparison.OrdinalIgnoreCase))
            {
                _jawPreviewFile = jaws.FileName;
                _jawGeometryPreview = EspritSetupAssembler.ReadGdmlTopView(jaws.FileName);
            }
            List<JawMountPoint> points = ReadJawMountPoints(jaws.ParentAccessoryFile);
            bool hasOuterPair = points.Count >= 4;
            _jawOuterPair.Enabled = hasOuterPair;
            if (!hasOuterPair && _jawOuterPair.Checked) _jawInnerPair.Checked = true;
            _jawPreview.Invalidate();
        }

        private void PaintJawPreview(object sender, PaintEventArgs e)
        {
            MountingElementSpec jaws = CurrentJawSpec();
            if (jaws == null) return;
            List<JawMountPoint> points = ReadJawMountPoints(jaws.ParentAccessoryFile);
            if (points.Count == 0)
            {
                using (var text = new SolidBrush(Color.FromArgb(88, 96, 103)))
                    e.Graphics.DrawString("No se encontraron puntos de montaje.", Font,
                        text, new RectangleF(8, 8, _jawPreview.Width - 16, 40));
                return;
            }

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var area = new RectangleF(13, 22, Math.Max(20, _jawPreview.Width - 26),
                Math.Max(20, _jawPreview.Height - 30));
            float centerX = area.Left + area.Width / 2F;
            float centerY = area.Top + area.Height / 2F;

            using (var fixture = new SolidBrush(Color.FromArgb(218, 223, 226)))
            using (var fixtureEdge = new Pen(Color.FromArgb(151, 159, 164)))
            {
                var body = new RectangleF(area.Left, area.Top,
                    area.Width, area.Height);
                e.Graphics.FillRectangle(fixture, body);
                e.Graphics.DrawRectangle(fixtureEdge, body.X, body.Y, body.Width, body.Height);
            }

            bool outerPair = jaws.JawAdapterIndices.Contains(3);
            float separation = outerPair ? area.Height * 0.32F : area.Height * 0.20F;
            float jawWidth = Math.Min(116F, area.Width * 0.58F);
            float jawHeight = Math.Min(34F, area.Height * 0.27F);
            float upperY = centerY - separation;
            float lowerY = centerY + separation;
            DrawJawGeometry(e.Graphics, centerX, upperY, jawWidth, jawHeight,
                !jaws.ReverseJaws);
            DrawJawGeometry(e.Graphics, centerX, lowerY, jawWidth, jawHeight,
                jaws.ReverseJaws);

            int first = outerPair ? 3 : 1;
            DrawJawNumber(e.Graphics, centerX - jawWidth / 2F - 12F, upperY,
                first, true);
            DrawJawNumber(e.Graphics, centerX - jawWidth / 2F - 12F, lowerY,
                first + 1, true);
            int otherFirst = outerPair ? 1 : 3;
            DrawJawNumber(e.Graphics, area.Right - 12F, centerY - 10F,
                otherFirst, false);
            DrawJawNumber(e.Graphics, area.Right - 12F, centerY + 10F,
                otherFirst + 1, false);

            string viewText = _jawGeometryPreview.Count > 0
                ? "Vista superior de las garras"
                : "Vista simplificada";
            using (var caption = new SolidBrush(Color.FromArgb(70, 79, 84)))
                e.Graphics.DrawString(viewText, Font, caption, area.Left, 2);
        }

        private void DrawJawNumber(Graphics graphics, float x, float y, int number,
            bool selected)
        {
            using (var fill = new SolidBrush(selected ? EspritBlue : Color.White))
            using (var edge = new Pen(selected ? EspritBlue : Color.FromArgb(130, 139, 144)))
            using (var text = new SolidBrush(selected ? Color.White : Color.FromArgb(70, 78, 83)))
            {
                graphics.FillEllipse(fill, x - 8, y - 8, 16, 16);
                graphics.DrawEllipse(edge, x - 8, y - 8, 16, 16);
                string value = number.ToString(CultureInfo.InvariantCulture);
                SizeF size = graphics.MeasureString(value, Font);
                graphics.DrawString(value, Font, text, x - size.Width / 2F,
                    y - size.Height / 2F);
            }
        }

        private void DrawJawGeometry(Graphics graphics, float centerX, float centerY,
            float width, float height, bool rotate180)
        {
            if (_jawGeometryPreview.Count == 0)
            {
                using (var jaw = new SolidBrush(Color.FromArgb(180, 196, 204)))
                using (var edge = new Pen(Color.FromArgb(78, 105, 117)))
                {
                    graphics.FillRectangle(jaw, centerX - width / 2F,
                        centerY - height / 2F, width, height);
                    graphics.DrawRectangle(edge, centerX - width / 2F,
                        centerY - height / 2F, width, height);
                }
                return;
            }

            float minX = _jawGeometryPreview.Min(point => point.X);
            float maxX = _jawGeometryPreview.Max(point => point.X);
            float minY = _jawGeometryPreview.Min(point => point.Y);
            float maxY = _jawGeometryPreview.Max(point => point.Y);
            float rangeX = Math.Max(0.001F, maxX - minX);
            float rangeY = Math.Max(0.001F, maxY - minY);
            float geometryCenterX = (minX + maxX) / 2F;
            float geometryCenterY = (minY + maxY) / 2F;
            float geometryScale = Math.Min(width / rangeX, height / rangeY);
            using (var shadow = new SolidBrush(Color.FromArgb(205, 104, 128, 139)))
            using (var pointBrush = new SolidBrush(Color.FromArgb(220, 54, 102, 123)))
            {
                graphics.FillRectangle(shadow, centerX - width / 2F,
                    centerY - height / 2F, width, height);
                foreach (PointF point in _jawGeometryPreview)
                {
                    float dx = (point.X - geometryCenterX) * geometryScale;
                    float dy = (point.Y - geometryCenterY) * geometryScale;
                    if (rotate180) { dx = -dx; dy = -dy; }
                    graphics.FillRectangle(pointBrush, centerX + dx - 0.7F,
                        centerY - dy - 0.7F, 1.4F, 1.4F);
                }
            }
        }

        private static List<JawMountPoint> ReadJawMountPoints(string fixtureFile)
        {
            var result = new List<JawMountPoint>();
            if (string.IsNullOrWhiteSpace(fixtureFile) || !File.Exists(fixtureFile))
                return result;
            try
            {
                XDocument document = XDocument.Load(fixtureFile);
                foreach (XElement adapter in document.Descendants()
                    .Where(node => node.Name.LocalName == "Adapter"))
                {
                    XElement translation = adapter.Descendants()
                        .FirstOrDefault(node => node.Name.LocalName == "Translation");
                    string[] coordinates = translation?.Attribute("trans")?.Value?
                        .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (coordinates == null || coordinates.Length < 3) continue;
                    if (!double.TryParse(coordinates[0], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double x) ||
                        !double.TryParse(coordinates[1], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double y) ||
                        !double.TryParse(coordinates[2], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double z)) continue;
                    result.Add(new JawMountPoint(x, y, z));
                }
            }
            catch { }
            return result;
        }

        private void UpdateWorkZeroEditorLayout()
        {
            if (_zeroOptions == null) return;
            bool hasJaws = HasMountedJaws();
            bool manual = _zeroManual.Checked;
            _gripDepthLabel.Visible = hasJaws;
            _gripDepth.Visible = hasJaws;
            UpdateJawConfigurationControls();
            _zeroOptions.SetCellPosition(_createWorkPlane,
                new TableLayoutPanelCellPosition(hasJaws ? 2 : 0, 0));
            _zeroOptions.SetColumnSpan(_createWorkPlane, hasJaws ? 1 : 3);
            _zeroEditorLayout.RowStyles[1].Height = manual ? 42 : 0;
            _zeroEditorLayout.RowStyles[3].Height = hasJaws ? 205 : 0;
            _zeroEditorPanel.Height = 16 + 38 + (manual ? 42 : 0) + 40 +
                                      (hasJaws ? 205 : 0) + 38;
        }

        private void UpdateWorkZeroSummary(string zeroText = null,
            string planeText = null, double? gripDepth = null)
        {
            zeroText = zeroText ?? (_zeroManual.Checked && _hasManualZero
                ? FormatZeroPoint()
                : "Cero automático");
            planeText = planeText ?? (_createWorkPlane.Checked
                ? "Plano con nombre del cero"
                : "Sin plano de trabajo");
            string summary = zeroText + "  •  " + planeText;
            if (HasMountedJaws())
            {
                string jawSummary = string.Empty;
                if (!gripDepth.HasValue && TryGetGripDepth(out double value))
                    gripDepth = value;
                if (gripDepth.HasValue)
                    jawSummary = "Amarre " + gripDepth.Value.ToString(
                        "0.##", CultureInfo.CurrentCulture) + " mm";
                MountingElementSpec jaws = CurrentJawSpec();
                if (jaws != null)
                    jawSummary += jaws.JawAdapterIndices.Contains(3)
                        ? "  •  Par exterior 3–4"
                        : "  •  Par interior 1–2";
                if (jaws?.ReverseJaws == true) jawSummary += "  •  Invertidas";
                if (!string.IsNullOrWhiteSpace(jawSummary))
                    summary += Environment.NewLine + jawSummary;
            }
            _zeroSummaryText.Text = summary;
        }

        private void SetMountingOptionsVisible(bool visible)
        {
            _mountingFaceSection.Visible = visible;
            _mountingChoices.Visible = visible;
            _destinationSection.Visible = visible;
            _destinationChoices.Visible = visible;
            _newFilePanel.Visible = visible && _destinationFile.Checked;
        }

        private void SetPostSelectionControlsVisible(bool visible)
        {
            _zeroSection.Visible = visible;
            if (!visible)
            {
                _zeroSummaryPanel.Visible = false;
                _zeroEditorPanel.Visible = false;
            }
            else
                _zeroSummaryPanel.Visible = true;
            SetMountingOptionsVisible(visible);
        }

        private void EndSelectionMode(bool clearGraphicSelection)
        {
            _selectionRemovalTimer.Stop();
            _pendingRemovalKeys.Clear();
            _pendingRemovalRole = null;
            _activeSelectionRole = null;
            _solidSelectionMode = false;

            try
            {
                var document = (Esprit.Document)_app.Document;
                DisableSolidDigitizing(document);
                if (clearGraphicSelection && document?.Group != null)
                {
                    _synchronizingSelection = true;
                    try { document.Group.Clear(); }
                    finally { _synchronizingSelection = false; }
                }
                document?.Refresh();
            }
            catch { }

            _partBox.BackColor = SelectionBackground;
            _stockBox.BackColor = SelectionBackground;
            _fixtureBox.BackColor = SelectionBackground;
        }

        private string BrowseFixtureFile(string title)
        {
            string folder = string.Empty;
            try
            {
                folder = _app.Configuration.GetFileDirectory(
                    EspritConstants.espFileType.espFileTypeFixtures);
            }
            catch { }
            using (var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "Accesorios ESPRIT (*.gdml)|*.gdml|Todos los archivos (*.*)|*.*",
                InitialDirectory = Directory.Exists(folder) ? folder : string.Empty,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true,
                AutoUpgradeEnabled = true
            })
            {
                return dialog.ShowDialog(this) == DialogResult.OK
                    ? dialog.FileName
                    : null;
            }
        }

        private void RemoveMountingElement(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            if (!(_mountingTree.SelectedNode?.Tag is MountingElementSpec element)) return;
            int index = _mountingElements.IndexOf(element);
            if (index < 0) return;
            _mountingElements.RemoveAt(index);
            if (element.Type == MountingElementType.Accessory)
            {
                while (index < _mountingElements.Count &&
                       _mountingElements[index].Type == MountingElementType.Jaws)
                    _mountingElements.RemoveAt(index);
            }
            RefreshState();
        }

        private void MoveMountingElementUp(object sender, EventArgs e) =>
            MoveMountingElement(-1);

        private void MoveMountingElementDown(object sender, EventArgs e) =>
            MoveMountingElement(1);

        private void MoveMountingElement(int delta)
        {
            PrepareForExtensionCommand();
            if (!(_mountingTree.SelectedNode?.Tag is MountingElementSpec element)) return;
            int index = _mountingElements.IndexOf(element);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= _mountingElements.Count) return;
            _mountingElements.RemoveAt(index);
            _mountingElements.Insert(target, element);
            if (!HasValidMountingOrder())
            {
                _mountingElements.RemoveAt(target);
                _mountingElements.Insert(index, element);
                return;
            }
            RefreshState();
        }

        private bool HasValidMountingOrder()
        {
            bool hasAccessory = false;
            foreach (MountingElementSpec element in _mountingElements)
            {
                if (element.Type == MountingElementType.Accessory)
                    hasAccessory = true;
                else if (!hasAccessory)
                    return false;
            }
            return true;
        }

        private void MountingTreeOnNodeMouseDoubleClick(object sender,
            TreeNodeMouseClickEventArgs e)
        {
            PrepareForExtensionCommand();
            if (e.Node?.Tag as string != "Machine" || !_destinationFile.Checked) return;
            BrowseNewMachine(sender, EventArgs.Empty);
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
                Margin = mode == StockMode.Block
                    ? new Padding(0, 0, 3, 0)
                    : mode == StockMode.Bar
                        ? new Padding(2, 0, 2, 0)
                        : mode == StockMode.Solid
                            ? new Padding(2, 0, 2, 0)
                            : new Padding(3, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            choice.FlatAppearance.BorderColor = Color.FromArgb(165, 171, 176);
            choice.CheckedChanged += StockChoiceOnCheckedChanged;
            choice.Click += (s, e) => ExpandStockEditor();
            return choice;
        }

        private static TextBox StockDimensionInput()
        {
            var input = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = "0,00",
                TextAlign = HorizontalAlignment.Left,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                AutoSize = false,
                Height = 27,
                MaxLength = 16
            };
            input.HandleCreated += (sender, args) =>
                SetTextBoxInnerMargins((TextBox)sender, 5, 5);
            return input;
        }

        private static void SetTextBoxInnerMargins(TextBox textBox, int left, int right)
        {
            if (textBox == null || !textBox.IsHandleCreated) return;
            int margins = (right << 16) | (left & 0xFFFF);
            SendMessage(textBox.Handle, EmSetMargins,
                (IntPtr)(EcLeftMargin | EcRightMargin), (IntPtr)margins);
        }

        private static ComboBox StockAlignmentInput()
        {
            var input = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System
            };
            input.Items.AddRange(new object[]
            {
                "Centrada",
                "Alinear a la izquierda",
                "Alinear a la derecha",
                "Izquierda Derecha Márgenes"
            });
            input.SelectedIndex = 0;
            return input;
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
            _machinePathText = new TextBox
            {
                Text = "Seleccione una máquina .mprj",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                TabStop = false,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 4, 2)
            };
            fields.Controls.Add(_machinePathText, 1, 0);
            var browseMachine = NativeBrowseButton("Seleccionar máquina para el nuevo archivo");
            browseMachine.Image = CreateFolderBrowseIcon();
            browseMachine.BackColor = panel.BackColor;
            browseMachine.Tag = panel.BackColor;
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
            browseOutput.BackColor = panel.BackColor;
            browseOutput.Tag = panel.BackColor;
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
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 6),
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
            string suggestedName = SanitizeFileName(Path.GetFileNameWithoutExtension(
                _espritFileName.Text.Trim()));
            using (var dialog = new SaveFileDialog
            {
                Title = "Guardar nueva fase de ESPRIT",
                Filter = "Archivos ESPRIT (*.esprit)|*.esprit",
                DefaultExt = "esprit",
                AddExtension = true,
                FileName = suggestedName,
                InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
                CheckPathExists = true,
                OverwritePrompt = true,
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _newOutputDirectory = Path.GetDirectoryName(dialog.FileName);
                _espritFileName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                _toolTip.SetToolTip(_espritFileName, dialog.FileName);
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
            PrepareForExtensionCommand();
            UpdateStockModeUi();
            RefreshState();
        }

        private void UpdateStockModeUi()
        {
            if (_stockContext == null) return;
            bool parametric = _stockMode == StockMode.Block || _stockMode == StockMode.Bar;
            bool solid = _stockMode == StockMode.Solid;
            bool file = _stockMode == StockMode.File;
            _stockContext.Height = parametric ? 130 : 46;
            _stockParameters.Visible = parametric;
            _stockSolidRow.Visible = solid;
            _stockFileRow.Visible = file;

            if (file)
                _stockFileText.Text = string.IsNullOrWhiteSpace(_stockFilePath)
                    ? "Ningún archivo STL seleccionado"
                    : _stockFilePath;

            if (parametric)
            {
                bool block = _stockMode == StockMode.Block;
                _stockAlignments[0].Visible = block;
                _stockAlignments[1].Visible = block;
                _stockAlignments[2].Visible = true;
                for (int index = 0; index < 3; index++)
                    UpdateStockMarginUi(index);
                _stockParameters.BringToFront();
                AutoFitStockValues();
            }
            else if (solid) _stockSolidRow.BringToFront();
            else if (file)
            {
                _stockFileRow.BringToFront();
            }
        }

        private void StockParameterOnChanged(object sender, EventArgs e)
        {
            if (_initializing || _updatingStockParameters) return;
            RefreshState();
        }

        private void UpdateStockMarginUi(int index)
        {
            if (_stockAlignments == null || _stockMarginRows == null) return;
            bool alignmentVisible = _stockAlignments[index].Visible;
            int alignment = _stockAlignments[index].SelectedIndex;
            bool singleMargin = alignmentVisible && (alignment == 1 || alignment == 2);
            bool doubleMargin = alignmentVisible && alignment == 3;
            bool showMargins = singleMargin || doubleMargin;

            string dimensionName = _stockMode == StockMode.Bar
                ? (index == 0 ? "Diámetro exterior" : index == 1 ? "Diámetro interior" : "Longitud Z")
                : (index == 0 ? "Longitud X" : index == 1 ? "Longitud Y" : "Altura Z");
            _stockParameterLabels[index].Text = dimensionName;
            _stockParameterValues[index].Visible = !doubleMargin;
            _stockParameterLabels[index].Visible = !doubleMargin;
            _stockMarginLabels[index].Visible = showMargins;
            _stockMarginSecondaryLabels[index].Visible = doubleMargin;
            _stockMarginLabels[index].Text = alignment == 2
                ? "Margen der."
                : "Margen izq.";
            _stockMarginSecondaryLabels[index].Text = "Margen der.";

            var labelRow = (TableLayoutPanel)_stockMarginLabels[index].Parent;
            if (doubleMargin)
            {
                SetStockColumns(_stockMarginRows[index], 0, 50, 50);
                SetStockColumns(labelRow, 0, 50, 50);
            }
            else if (showMargins)
            {
                SetStockColumns(_stockMarginRows[index], 58, 42, 0);
                SetStockColumns(labelRow, 58, 42, 0);
            }
            else
            {
                SetStockColumns(_stockMarginRows[index], 100, 0, 0);
                SetStockColumns(labelRow, 100, 0, 0);
            }
            _stockMarginValues[index, 0].Visible = showMargins;
            _stockMarginValues[index, 1].Visible = doubleMargin;
            _stockParameterValues[index].Margin =
                new Padding(0, 0, showMargins ? 3 : 0, 0);
            _stockMarginValues[index, 0].Margin = new Padding(showMargins ? 3 : 0, 0,
                doubleMargin ? 3 : 0, 0);
            _stockMarginValues[index, 1].Margin = new Padding(3, 0, 0, 0);
            _stockContext.Height = 130;
        }

        private static void SetStockColumns(TableLayoutPanel row,
            float first, float second, float third)
        {
            float[] widths = { first, second, third };
            for (int column = 0; column < widths.Length; column++)
            {
                bool collapsed = widths[column] <= 0;
                row.ColumnStyles[column].SizeType = collapsed
                    ? SizeType.Absolute
                    : SizeType.Percent;
                row.ColumnStyles[column].Width = collapsed ? 0 : widths[column];
            }
        }

        private void AutoFitStock(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            if (_part?.Solid == null)
            {
                SetValidationStatus("Seleccione primero el sólido de pieza.", false);
                return;
            }
            AutoFitStockValues();
            RefreshState();
        }

        private void ApplyStockDefinition(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
            if (!IsStockReady())
            {
                SetValidationStatus("Revise las medidas y los márgenes del bruto.", false);
                return;
            }
            _stockContext.Visible = false;
            SetValidationStatus(
                "Bruto definido. Pulse de nuevo Bloque o Barra para editarlo.", true);
        }

        private void ExpandStockEditor()
        {
            if (_stockMode != StockMode.Block && _stockMode != StockMode.Bar) return;
            _stockContext.Visible = true;
            UpdateStockModeUi();
        }

        private void AutoFitStockValues()
        {
            if (_part?.Solid == null) return;
            _updatingStockParameters = true;
            try
            {
                var bounds = GetBounds(_part.Solid);
                double x = ToDimension(bounds.MaximumX - bounds.MinimumX);
                double y = ToDimension(bounds.MaximumY - bounds.MinimumY);
                double z = ToDimension(bounds.MaximumZ - bounds.MinimumZ);
                if (_stockMode == StockMode.Block)
                {
                    SetStockValue(0, x);
                    SetStockValue(1, y);
                    SetStockValue(2, z);
                }
                else if (_stockMode == StockMode.Bar)
                {
                    SetStockValue(0, Math.Max(x, y));
                    SetStockValue(1, 0.0);
                    SetStockValue(2, z);
                }
            }
            finally { _updatingStockParameters = false; }
        }

        private static double ToDimension(double value) =>
            Math.Max(0.01, Math.Round(Math.Abs(value), 2));

        private void SetStockValue(int index, double value)
        {
            _stockParameterValues[index].Text =
                value.ToString("0.00", CultureInfo.CurrentCulture);
        }

        private void BrowseStockFile(object sender, EventArgs e)
        {
            PrepareForExtensionCommand();
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
            button.BackColor = visible
                ? Color.FromArgb(255, 239, 167)
                : button.Tag is Color baseColor ? baseColor : Color.White;
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
            bool activated = sender is RadioButton changedChoice && changedChoice.Checked;
            if (activated && !_initializing)
            {
                PrepareForExtensionCommand();
                _mountBottom.Checked = _destinationCurrent.Checked;
                _mountTop.Checked = !_destinationCurrent.Checked;
            }
            bool newFile = _destinationFile.Checked;
            _stockSection.Visible = !newFile;
            _stockChoices.Visible = !newFile;
            _stockContext.Visible = !newFile;
            _newFilePanel.Visible = newFile;
            int nextPhase = GetNextConfigurationNumber();
            if (newFile && string.IsNullOrWhiteSpace(_espritFileName.Text))
                _espritFileName.Text = GetDefaultEspritFileName(nextPhase);
            _destinationFile.Text = (newFile ? "✓  " : string.Empty) +
                "Nuevo archivo ESPRIT\r\nCrear la siguiente fase en otro archivo";
            RefreshState();
        }

        private void MountingFaceOnCheckedChanged(object sender, EventArgs e)
        {
            if (!_initializing && sender is RadioButton choice && choice.Checked)
                PrepareForExtensionCommand();
            StyleMountingChoices();
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
            RefreshMountingTree();
            UpdateWorkZeroEditorLayout();
            UpdateWorkZeroSummary();

            _countLabel.Text = _fixtures.Count == 1
                ? "1 sólido seleccionado"
                : _fixtures.Count + " sólidos seleccionados";
            _combine.Enabled = _fixtures.Count > 1;
            if (!_combine.Enabled) _combine.Checked = false;
            StyleCombineButton();

            string folder = _fixtures.Count > 0 ? DetermineOutputFolder() : string.Empty;
            bool reusePreviousPart = CanReusePreviousPart() && _part == null && _stock == null;
            bool stockReady = IsStockReady();
            bool definePartAndStock = _part != null && stockReady;
            bool newFileReady = _destinationFile.Checked &&
                                (_part != null || HasExistingPart()) &&
                                File.Exists(_newMachinePath) &&
                                GetNewEspritPath() != null;
            bool zeroReady = (!_zeroManual.Checked || _hasManualZero) &&
                             (!HasMountedJaws() || TryGetGripDepth(out _));
            bool ready = (_destinationFile.Checked
                    ? newFileReady
                    : reusePreviousPart || definePartAndStock) &&
                folder != null && zeroReady;
            _accept.Enabled = ready && !_fixtureSelectionPanel.Visible &&
                              !_zeroEditorPanel.Visible;

            if (_fixtureSelectionPanel.Visible)
                SetValidationStatus("Termine la selección de utillajes para continuar.", false);
            else if (_zeroEditorPanel.Visible)
                SetValidationStatus("Aplique la definición del cero y del amarre para continuar.", false);
            else if (_zeroManual.Checked && !_hasManualZero)
                SetValidationStatus("Seleccione el punto que se usará como cero de pieza.", false);
            else if (HasMountedJaws() && !TryGetGripDepth(out _))
                SetValidationStatus("Indique una profundidad de amarre válida.", false);
            else if (_destinationFile.Checked && !File.Exists(_newMachinePath))
                SetValidationStatus("Seleccione la máquina del nuevo archivo ESPRIT.", false);
            else if (_destinationFile.Checked && !(_part != null || HasExistingPart()))
                SetValidationStatus("Seleccione la pieza que se utilizará en el nuevo archivo.", false);
            else if (_destinationFile.Checked && GetNewEspritPath() == null)
                SetValidationStatus("Indique un nombre y una carpeta válidos para el nuevo archivo.", false);
            else if (!reusePreviousPart && _part == null)
                SetValidationStatus("Seleccione el sólido de pieza.", false);
            else if (!reusePreviousPart && !stockReady)
                SetValidationStatus("Defina o seleccione el bruto.", false);
            else if (folder == null)
                SetValidationStatus("No se pudo determinar la carpeta de accesorios.", false);
            else
                SetValidationStatus("Listo para crear el montaje.", true);

            if (_destinationFile.Checked && string.IsNullOrWhiteSpace(_newMachinePath))
                _hint.Text = "Seleccione la máquina que se cargará en el nuevo archivo ESPRIT.";
            else if (_destinationFile.Checked && _part == null)
                _hint.Text = "Se reutilizará la pieza actual y el material restante de la última operación.";
            else if (CanReusePreviousPart() && _part == null && _stock == null)
                _hint.Text = "Se reutilizarán la pieza y el bruto de la fase anterior.";
        }

        private void SetValidationStatus(string text, bool ready)
        {
            _validationStatus.Text = text;
            _validationStatus.ForeColor = ready
                ? Color.FromArgb(63, 132, 46)
                : Color.FromArgb(88, 96, 103);
            _toolTip.SetToolTip(_validationStatus, text);
        }

        private void RefreshMountingTree()
        {
            object selected = _mountingTree.SelectedNode?.Tag;
            _mountingTree.BeginUpdate();
            _mountingTree.Nodes.Clear();

            string machineName = "Máquina";
            string machinePath = _destinationFile.Checked && File.Exists(_newMachinePath)
                ? _newMachinePath
                : null;
            if (string.IsNullOrWhiteSpace(machinePath))
            {
                try { machinePath = ((Esprit.Document)_app.Document).InitialMachineSetup.MachineFileName; }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(machinePath))
                machineName = Path.GetFileNameWithoutExtension(machinePath);

            var root = new TreeNode(machineName)
            {
                Tag = "Machine",
                ImageKey = "machine",
                SelectedImageKey = "machine",
                ToolTipText = string.IsNullOrWhiteSpace(machinePath) ? machineName : machinePath
            };
            _mountingTree.Nodes.Add(root);
            TreeNode parent = root;
            foreach (MountingElementSpec element in _mountingElements)
            {
                string label = Path.GetFileNameWithoutExtension(element.FileName);
                if (element.Type == MountingElementType.Jaws)
                    label += "  (Garras)";
                string imageKey = element.Type == MountingElementType.Jaws
                    ? "jaws"
                    : "accessory";
                var node = new TreeNode(label)
                {
                    Tag = element,
                    ImageKey = imageKey,
                    SelectedImageKey = imageKey,
                    ToolTipText = element.FileName
                };
                parent.Nodes.Add(node);
                if (element.Type == MountingElementType.Accessory) parent = node;
            }

            if (_combine.Checked && _fixtures.Count > 1)
            {
                TreeNode combined = parent.Nodes.Add("Utillaje CAD combinado");
                combined.ImageKey = combined.SelectedImageKey = "fixture";
                combined.ToolTipText = "Utillaje CAD combinado";
                parent = combined;
            }
            else
            {
                foreach (SolidChoice fixture in _fixtures
                    .OrderBy(item => GetZBounds(item.Solid).Minimum))
                {
                    TreeNode fixtureNode = parent.Nodes.Add(fixture.DisplayName);
                    fixtureNode.ImageKey = fixtureNode.SelectedImageKey = "fixture";
                    fixtureNode.ToolTipText = fixture.DisplayName;
                    parent = fixtureNode;
                }
            }
            if (_part != null || HasExistingPart())
            {
                string partName = _part?.DisplayName ?? "Pieza existente";
                TreeNode partNode = parent.Nodes.Add(partName);
                partNode.ImageKey = partNode.SelectedImageKey = "part";
                partNode.ToolTipText = partName;
            }

            root.ExpandAll();
            TreeNode restore = FindNodeByTag(root, selected);
            _mountingTree.SelectedNode = restore;
            _mountingTree.EndUpdate();
        }

        private static TreeNode FindNodeByTag(TreeNode node, object tag)
        {
            if (tag != null && ReferenceEquals(node.Tag, tag)) return node;
            foreach (TreeNode child in node.Nodes)
            {
                TreeNode found = FindNodeByTag(child, tag);
                if (found != null) return found;
            }
            return null;
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

        private bool IsStockReady()
        {
            if (_stockMode == StockMode.Block)
                return IsStockAxisReady(0) && IsStockAxisReady(1) && IsStockAxisReady(2);
            if (_stockMode == StockMode.Bar)
                return TryGetStockValue(0, out double outerDiameter) && outerDiameter > 0 &&
                       TryGetStockValue(1, out double innerDiameter) && innerDiameter >= 0 &&
                       innerDiameter < outerDiameter && IsStockAxisReady(2);
            return (_stockMode == StockMode.Solid && _stock != null) ||
                   (_stockMode == StockMode.File && File.Exists(_stockFilePath));
        }

        private bool IsStockAxisReady(int index)
        {
            int alignment = _stockAlignments[index].SelectedIndex;
            if (alignment != 3 &&
                (!TryGetStockValue(index, out double size) || size <= 0))
                return false;
            if (alignment == 1 || alignment == 2)
                return TryGetStockMargin(index, 0, out double margin) && margin >= 0;
            if (alignment == 3)
                return TryGetStockMargin(index, 0, out double left) && left >= 0 &&
                       TryGetStockMargin(index, 1, out double right) && right >= 0;
            return true;
        }

        private bool TryGetStockValue(int index, out double value)
        {
            value = 0.0;
            string text = _stockParameterValues[index].Text.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float,
                              CultureInfo.CurrentCulture, out value) ||
                          double.TryParse(text.Replace(',', '.'), NumberStyles.Float,
                              CultureInfo.InvariantCulture, out value);
            return parsed && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private bool TryGetStockMargin(int axis, int marginIndex, out double value)
        {
            value = 0.0;
            string text = _stockMarginValues[axis, marginIndex].Text.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float,
                              CultureInfo.CurrentCulture, out value) ||
                          double.TryParse(text.Replace(',', '.'), NumberStyles.Float,
                              CultureInfo.InvariantCulture, out value);
            return parsed && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double GetStockMargin(int axis, bool positive)
        {
            int alignment = _stockAlignments[axis].SelectedIndex;
            if (alignment == 0) return 0.0;
            if (alignment == 3)
            {
                TryGetStockMargin(axis, positive ? 1 : 0, out double margin);
                return margin;
            }
            if ((alignment == 1 && !positive) || (alignment == 2 && positive))
            {
                TryGetStockMargin(axis, 0, out double margin);
                return margin;
            }
            return 0.0;
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
            EndSelectionMode(false);
            double gripDepth = 0.0;
            if ((HasMountedJaws() && !TryGetGripDepth(out gripDepth)) ||
                (_zeroManual.Checked && !_hasManualZero))
            {
                RefreshState();
                return;
            }
            string folder = _fixtures.Count > 0 ? DetermineOutputFolder() : string.Empty;
            bool reusePreviousPart = CanReusePreviousPart() && _part == null && _stock == null;
            bool stockReady = IsStockReady();
            bool definePartAndStock = _part != null && stockReady;
            bool newFile = _destinationFile.Checked;
            string newEspritPath = newFile ? GetNewEspritPath() : null;
            bool newFileReady = newFile && (_part != null || HasExistingPart()) &&
                                File.Exists(_newMachinePath) && newEspritPath != null;
            if ((newFile ? !newFileReady : !reusePreviousPart && !definePartAndStock) ||
                folder == null)
                return;
            IReadOnlyList<string> paths = Array.Empty<string>();
            double mountingStackHeight = 0.0;
            double workOffsetBaseZ = 0.0;
            try
            {
                if (_fixtures.Count > 0)
                {
                    var document = (Esprit.Document)_app.Document;
                    var exporter = new GdmlFixtureExporter(document);
                    var fixtureBounds = _fixtures
                        .Select(choice =>
                        {
                            var bounds = GetZBounds(choice.Solid);
                            return (Choice: choice, MinimumZ: bounds.Minimum,
                                MaximumZ: bounds.Maximum);
                        })
                        .OrderBy(item => item.MinimumZ)
                        .ToList();
                    var orderedFixtures = fixtureBounds.Select(x => x.Choice.Solid).ToList();
                    mountingStackHeight = _combine.Checked && fixtureBounds.Count > 1
                        ? fixtureBounds.Max(x => x.MaximumZ) - fixtureBounds.Min(x => x.MinimumZ)
                        : fixtureBounds.Sum(x => x.MaximumZ - x.MinimumZ);
                    workOffsetBaseZ = _mountTop.Checked
                        ? fixtureBounds.Max(x => x.MaximumZ)
                        : fixtureBounds.Min(x => x.MinimumZ);
                    paths = exporter.Export(
                        orderedFixtures, folder, GetAutomaticPhaseNumber(),
                        _combine.Checked, _mountTop.Checked);
                }
            }
            catch (Exception ex)
            {
                ShowStageError("crear los archivos GDML de utillaje", ex);
                return;
            }

            var pending = new PendingMount
            {
                Paths = paths,
                BaseElements = _mountingElements.ToList(),
                CreateNewConfiguration = _destinationNew.Checked,
                RotateAssembly = _mountTop.Checked,
                MountingStackHeight = mountingStackHeight,
                WorkOffsetBaseZ = workOffsetBaseZ,
                GripDepth = gripDepth,
                WorkZero = new WorkZeroSpec
                {
                    IsManual = _zeroManual.Checked,
                    X = _manualZeroX,
                    Y = _manualZeroY,
                    Z = _manualZeroZ,
                    CreateWorkPlane = _createWorkPlane.Checked
                },
                NewEspritPath = newEspritPath,
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
                else if (_stockMode == StockMode.Block)
                {
                    TryGetStockValue(0, out double length);
                    TryGetStockValue(1, out double width);
                    TryGetStockValue(2, out double height);
                    assembler.ConfigurePartAndBlockStock(
                        _part.Solid, false, length, width, height,
                        GetStockAlignment(0), GetStockAlignment(1), GetStockAlignment(2),
                        GetStockMargin(0, false), GetStockMargin(0, true),
                        GetStockMargin(1, false), GetStockMargin(1, true),
                        GetStockMargin(2, false), GetStockMargin(2, true));
                }
                else if (_stockMode == StockMode.Bar)
                {
                    TryGetStockValue(0, out double outerDiameter);
                    TryGetStockValue(1, out double innerDiameter);
                    TryGetStockValue(2, out double barLength);
                    assembler.ConfigurePartAndBarStock(
                        _part.Solid, false, outerDiameter, innerDiameter, barLength,
                        GetStockAlignment(2),
                        GetStockMargin(2, false), GetStockMargin(2, true));
                }
                else if (_stockMode == StockMode.Solid)
                    assembler.ConfigurePartAndStock(_part.Solid, _stock.Solid);
                else if (_stockMode == StockMode.File)
                    assembler.ConfigurePartAndFileStock(_part.Solid, _stockFilePath);
            }
            catch (Exception ex)
            {
                ShowStageError("actualizar la pieza y el bruto", ex);
                return;
            }

            CompleteMount(pending);
        }

        private EspritConstants.espStockAlignmentType GetStockAlignment(int index)
        {
            switch (_stockAlignments[index].SelectedIndex)
            {
                case 1:
                    return EspritConstants.espStockAlignmentType.espStockAlignmentTypeNegative;
                case 2:
                    return EspritConstants.espStockAlignmentType.espStockAlignmentTypePositive;
                case 3:
                    return EspritConstants.espStockAlignmentType.espStockAlignmentTypeMargins;
                default:
                    return EspritConstants.espStockAlignmentType.espStockAlignmentTypeCentered;
            }
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
                        pending.MountingStackHeight, pending.WorkOffsetBaseZ,
                        pending.GripDepth, pending.WorkZero, pending.BaseElements);
                else
                    assembler.MountFixturesAndWorkpiece(
                        pending.Paths, pending.CreateNewConfiguration,
                        pending.RotateAssembly, pending.MountingStackHeight,
                        pending.WorkOffsetBaseZ, pending.GripDepth,
                        pending.WorkZero, pending.BaseElements);
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
            EndSelectionMode(true);
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

        private static (double Minimum, double Maximum) GetZBounds(Esprit.Solid solid)
        {
            var bounds = GetBounds(solid);
            return (bounds.MinimumZ, bounds.MaximumZ);
        }

        private static (double MinimumX, double MaximumX, double MinimumY,
            double MaximumY, double MinimumZ, double MaximumZ) GetBounds(Esprit.Solid solid)
        {
            EspritSolids.ISolidBody body = (EspritSolids.ISolidBody)solid.SolidBody;
            double minimumX = double.PositiveInfinity, minimumY = double.PositiveInfinity,
                minimumZ = double.PositiveInfinity;
            double maximumX = double.NegativeInfinity, maximumY = double.NegativeInfinity,
                maximumZ = double.NegativeInfinity;
            foreach (EspritSolids.ISolidVertex vertex in body.SolidVertices)
            {
                minimumX = Math.Min(minimumX, vertex.Geometry.X);
                maximumX = Math.Max(maximumX, vertex.Geometry.X);
                minimumY = Math.Min(minimumY, vertex.Geometry.Y);
                maximumY = Math.Max(maximumY, vertex.Geometry.Y);
                minimumZ = Math.Min(minimumZ, vertex.Geometry.Z);
                maximumZ = Math.Max(maximumZ, vertex.Geometry.Z);
            }

            if (double.IsPositiveInfinity(minimumZ) || double.IsNegativeInfinity(maximumZ))
                throw new InvalidDataException("No se pudieron calcular los límites del sólido " + solid.Number + ".");
            return (minimumX, maximumX, minimumY, maximumY, minimumZ, maximumZ);
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
                BackColor = Color.FromArgb(243, 247, 249),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 8, 10, 7)
            };
            panel.Controls.Add(new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = EspritBlue
            });
            panel.Controls.Add(new Label
            {
                Text = "Prepare el conjunto para el montaje",
                Dock = DockStyle.Top,
                Height = 23,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 53, 68)
            });
            panel.Controls.Add(new Label
            {
                Text = "Seleccione la pieza y el bruto, y añada los elementos que necesite al montaje.",
                Dock = DockStyle.Bottom,
                Height = 21,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Color.FromArgb(75, 91, 101)
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
                    new Point(0, 2), new Point(6, 2), new Point(8, 4),
                    new Point(16, 4), new Point(16, 13), new Point(0, 13)
                });
                graphics.DrawPolygon(edge, new[]
                {
                    new Point(0, 2), new Point(6, 2), new Point(8, 4),
                    new Point(16, 4), new Point(16, 13), new Point(0, 13)
                });
                graphics.FillPolygon(front, new[]
                {
                    new Point(1, 6), new Point(16, 6), new Point(13, 14),
                    new Point(0, 14)
                });
                graphics.DrawPolygon(edge, new[]
                {
                    new Point(1, 6), new Point(16, 6), new Point(13, 14),
                    new Point(0, 14)
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
                if (mode == StockMode.Block)
                {
                    DrawNativeCube(graphics, dark, top, front, side, 3, 2, 16, 15);
                }
                else if (mode == StockMode.Bar)
                {
                    graphics.FillRectangle(front, 6, 4, 14, 14);
                    graphics.FillEllipse(top, 6, 1, 14, 7);
                    graphics.FillEllipse(side, 6, 14, 14, 7);
                    graphics.DrawEllipse(dark, 6, 1, 14, 7);
                    graphics.DrawLine(dark, 6, 4, 6, 17);
                    graphics.DrawLine(dark, 20, 4, 20, 17);
                    graphics.DrawArc(dark, 6, 14, 14, 7, 0, 180);
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

        private static Bitmap CreateAutoFitIcon()
        {
            var bitmap = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(91, 100, 106), 1.4F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 2, 7, 2, 3);
                graphics.DrawLine(pen, 2, 3, 6, 3);
                graphics.DrawLine(pen, 12, 3, 16, 3);
                graphics.DrawLine(pen, 16, 3, 16, 7);
                graphics.DrawLine(pen, 2, 11, 2, 15);
                graphics.DrawLine(pen, 2, 15, 6, 15);
                graphics.DrawLine(pen, 12, 15, 16, 15);
                graphics.DrawLine(pen, 16, 15, 16, 11);
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
            Height = 24,
            BackColor = HeaderGray,
            ForeColor = Color.FromArgb(30, 53, 68),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(9, 0, 0, 0),
            Margin = new Padding(0, 8, 0, 5),
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
            choice.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 225, 245);
            choice.FlatAppearance.MouseDownBackColor = Color.FromArgb(155, 207, 237);
            return choice;
        }

        private static RadioButton CompactChoice(string text, bool isChecked)
        {
            RadioButton choice = MountingChoice(text, isChecked);
            choice.Margin = new Padding(0, 0, 4, 0);
            choice.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular,
                GraphicsUnit.Point);
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
            Block,
            Bar,
            Solid,
            File
        }

        private struct JawMountPoint
        {
            public JawMountPoint(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X;
            public double Y;
            public double Z;
        }

        private sealed class PendingMount
        {
            public IReadOnlyList<string> Paths { get; set; }
            public IReadOnlyList<MountingElementSpec> BaseElements { get; set; }
            public bool CreateNewConfiguration { get; set; }
            public bool RotateAssembly { get; set; }
            public double MountingStackHeight { get; set; }
            public double WorkOffsetBaseZ { get; set; }
            public double GripDepth { get; set; }
            public WorkZeroSpec WorkZero { get; set; }
            public string NewEspritPath { get; set; }
            public string NewMachinePath { get; set; }
            public Esprit.Solid SelectedPart { get; set; }
        }

        private void WireStockTabNavigation(Control control)
        {
            control.PreviewKeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Tab) e.IsInputKey = true;
            };
            control.KeyDown += (sender, e) =>
            {
                if (e.KeyCode != Keys.Tab) return;
                if (MoveStockFocus((Control)sender, !e.Shift))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        private bool MoveStockFocus(Control focused, bool forward)
        {
            var stockFields = new List<Control>();
            for (int axis = 0; axis < 3; axis++)
            {
                if (_stockParameterValues[axis].Visible)
                    stockFields.Add(_stockParameterValues[axis]);
                for (int margin = 0; margin < 2; margin++)
                {
                    if (_stockMarginValues[axis, margin].Visible)
                        stockFields.Add(_stockMarginValues[axis, margin]);
                }
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if (_stockAlignments[axis].Visible)
                    stockFields.Add(_stockAlignments[axis]);
            }

            int current = stockFields.IndexOf(focused);
            if (current < 0 || stockFields.Count < 2) return false;
            int next = (current + (forward ? 1 : -1) + stockFields.Count) %
                       stockFields.Count;
            Control target = stockFields[next];
            target.Focus();
            if (target is TextBox textBox) textBox.SelectAll();
            return true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            EndSelectionMode(true);
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
            base.OnFormClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        private const int EmSetMargins = 0x00D3;
        private const int EcLeftMargin = 0x0001;
        private const int EcRightMargin = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message,
            IntPtr wParam, IntPtr lParam);

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
