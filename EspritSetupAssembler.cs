using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace EspritSolidFixture
{
    internal sealed class EspritSetupAssembler
    {
        private readonly Esprit.Document _document;

        public EspritSetupAssembler(Esprit.Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public static bool HasMachine(Esprit.Document document)
        {
            if (document == null) return false;
            try
            {
                Esprit.MachineSetup setup = document.InitialMachineSetup;
                return setup != null &&
                       setup.MachineDefinition != null &&
                       !string.IsNullOrWhiteSpace(setup.MachineFileName);
            }
            catch
            {
                return false;
            }
        }

        public void ConfigurePartAndStock(Esprit.Solid partSolid, Esprit.Solid stockSolid)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            if (stockSolid == null) throw new ArgumentNullException(nameof(stockSolid));
            ConfigurePartCore(partSolid, workpiece =>
            {
                Esprit.SolidStock stock = workpiece.Stocks.AddSolidStock();
                stock.Name = "Material1";
                stock.Tolerance = 0.1;
                stock.Solid = (EspritSolids.SolidBody)stockSolid.SolidBody;
            });
        }

        public void ConfigurePartAndBlockStock(Esprit.Solid partSolid, bool autoSize,
            double length, double width, double height,
            EspritConstants.espStockAlignmentType lengthAlignment,
            EspritConstants.espStockAlignmentType widthAlignment,
            EspritConstants.espStockAlignmentType heightAlignment,
            double lengthNegativeMargin, double lengthPositiveMargin,
            double widthNegativeMargin, double widthPositiveMargin,
            double heightNegativeMargin, double heightPositiveMargin)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            ConfigurePartCore(partSolid, workpiece =>
            {
                Esprit.BlockStock stock = workpiece.Stocks.AddBlockStock();
                stock.Name = "Material1";
                if (autoSize) stock.AutoSize();
                SetBlockAlignment(stock, lengthAlignment, widthAlignment, heightAlignment,
                    autoSize, length, width, height,
                    lengthNegativeMargin, lengthPositiveMargin,
                    widthNegativeMargin, widthPositiveMargin,
                    heightNegativeMargin, heightPositiveMargin);
            });
        }

        public void ConfigurePartAndBarStock(Esprit.Solid partSolid, bool autoSize,
            double outsideDiameter, double insideDiameter, double length,
            EspritConstants.espStockAlignmentType lengthAlignment,
            double lengthNegativeMargin, double lengthPositiveMargin)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            ConfigurePartCore(partSolid, workpiece =>
            {
                Esprit.BarStock stock = workpiece.Stocks.AddBarStock();
                stock.Name = "Material1";
                stock.Tolerance = 0.1;
                if (autoSize)
                    stock.AutoSize();
                else
                    stock.OutsideDiameter = outsideDiameter;
                stock.InsideDiameter = autoSize ? 0.0 : insideDiameter;
                EspritConstants.STOCKALIGNMENT alignment = stock.get_LengthAlignment();
                alignment.AlignmentType = lengthAlignment;
                if (!autoSize && lengthAlignment !=
                    EspritConstants.espStockAlignmentType.espStockAlignmentTypeMargins)
                    alignment.Size = length;
                alignment.NegativeMargin = lengthNegativeMargin;
                alignment.Margin = lengthPositiveMargin;
                stock.set_LengthAlignment(alignment);
            });
        }

        private static void SetBlockAlignment(Esprit.BlockStock stock,
            EspritConstants.espStockAlignmentType lengthType,
            EspritConstants.espStockAlignmentType widthType,
            EspritConstants.espStockAlignmentType heightType,
            bool autoSize, double length, double width, double height,
            double lengthNegativeMargin, double lengthPositiveMargin,
            double widthNegativeMargin, double widthPositiveMargin,
            double heightNegativeMargin, double heightPositiveMargin)
        {
            EspritConstants.STOCKALIGNMENT lengthAlignment = stock.get_LengthAlignment();
            lengthAlignment.AlignmentType = lengthType;
            if (!autoSize && lengthType !=
                EspritConstants.espStockAlignmentType.espStockAlignmentTypeMargins)
                lengthAlignment.Size = length;
            lengthAlignment.NegativeMargin = lengthNegativeMargin;
            lengthAlignment.Margin = lengthPositiveMargin;
            stock.set_LengthAlignment(lengthAlignment);

            EspritConstants.STOCKALIGNMENT widthAlignment = stock.get_WidthAlignment();
            widthAlignment.AlignmentType = widthType;
            if (!autoSize && widthType !=
                EspritConstants.espStockAlignmentType.espStockAlignmentTypeMargins)
                widthAlignment.Size = width;
            widthAlignment.NegativeMargin = widthNegativeMargin;
            widthAlignment.Margin = widthPositiveMargin;
            stock.set_WidthAlignment(widthAlignment);

            EspritConstants.STOCKALIGNMENT heightAlignment = stock.get_HeightAlignment();
            heightAlignment.AlignmentType = heightType;
            if (!autoSize && heightType !=
                EspritConstants.espStockAlignmentType.espStockAlignmentTypeMargins)
                heightAlignment.Size = height;
            heightAlignment.NegativeMargin = heightNegativeMargin;
            heightAlignment.Margin = heightPositiveMargin;
            stock.set_HeightAlignment(heightAlignment);
        }

        public void ConfigurePartAndFileStock(Esprit.Solid partSolid, string fileName)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                throw new FileNotFoundException("No se encontró el archivo de bruto seleccionado.", fileName);

            ConfigurePartCore(partSolid, workpiece =>
                AddFileStock(workpiece, "Material1", fileName));
        }

        public void ConfigureExistingPartAndFileStock(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                throw new FileNotFoundException("No se encontró el material restante exportado.", fileName);
            if (_document.Workpieces.Count == 0 || _document.Workpieces[1].Parts.Count == 0)
                throw new InvalidOperationException("El documento no contiene una pieza que se pueda reutilizar.");

            Esprit.PartSetup setup = _document.PartSetup;
            setup.BeginEdit();
            try
            {
                Esprit.Workpiece workpiece = _document.Workpieces[1];
                for (int index = workpiece.Stocks.Count; index > 0; index--)
                    workpiece.Stocks.Remove(index);
                AddFileStock(workpiece, "Material_Fase_Anterior", fileName);
                setup.EndEdit();
            }
            catch
            {
                try { setup.CancelEdit(); } catch { }
                throw;
            }
        }

        public void CreateNewPhaseFile(IEnumerable<string> fixturePaths, string newEspritPath,
            string machinePath, Esprit.Solid selectedPart, bool rotateAssembly,
            double mountingStackHeight, double workOffsetBaseZ,
            double gripDepth, WorkZeroSpec workZero,
            IEnumerable<MountingElementSpec> baseElements = null)
        {
            var paths = fixturePaths.Where(File.Exists).ToList();
            var bases = (baseElements ?? Enumerable.Empty<MountingElementSpec>())
                .Where(item => item != null && File.Exists(item.FileName)).ToList();
            if (string.IsNullOrWhiteSpace(machinePath) || !File.Exists(machinePath))
                throw new FileNotFoundException("No se encontró la máquina seleccionada.", machinePath);
            if (string.IsNullOrWhiteSpace(newEspritPath))
                throw new ArgumentException("El nombre del nuevo archivo ESPRIT no es válido.", nameof(newEspritPath));
            string currentPath = _document.FileName;
            if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
                throw new InvalidOperationException(
                    "Guarde primero el documento ESPRIT actual para poder crear la siguiente fase.");
            if (string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(newEspritPath),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El nuevo archivo debe tener un nombre diferente al actual.");
            if (File.Exists(newEspritPath))
                throw new IOException("Ya existe el archivo " + Path.GetFileName(newEspritPath) +
                                      ". Indique otro nombre.");

            string step = "guardar el material restante";
            try
            {
                string directory = Path.GetDirectoryName(newEspritPath);
                Directory.CreateDirectory(directory);
                string stockPath = Path.Combine(directory,
                    Path.GetFileNameWithoutExtension(newEspritPath) + "_Material_Anterior.wp");
                ExportRemainingStock(stockPath);

                step = "guardar el documento original";
                _document.Save();
                step = "crear el nuevo archivo ESPRIT";
                _document.SaveAs(newEspritPath);

                step = "eliminar las operaciones de las fases anteriores";
                for (int index = _document.PartOperations.Count; index > 0; index--)
                    _document.PartOperations.Remove(index);

                step = "eliminar las características de las fases anteriores";
                RemoveAllFeatures();

                step = "eliminar las configuraciones adicionales anteriores";
                ClearAdditionalMachineSetups();

                step = "vaciar el montaje y cargar la nueva máquina";
                Esprit.MachineSetup setup = _document.InitialMachineSetup;
                setup.BeginEdit();
                try
                {
                    ClearMachineItems(setup.MachineItems);
                    setup.MachineFileName = machinePath;
                    setup.EndEdit();
                }
                catch
                {
                    try { setup.CancelEdit(); } catch { }
                    throw;
                }

                step = "configurar la pieza y el material restante";
                if (selectedPart != null)
                    ConfigurePartAndFileStock(selectedPart, stockPath);
                else
                    ConfigureExistingPartAndFileStock(stockPath);

                step = "crear el nuevo montaje";
                MountFixturesAndWorkpiece(paths, false, rotateAssembly,
                    mountingStackHeight, workOffsetBaseZ, gripDepth, workZero, bases);
                step = "guardar el nuevo archivo ESPRIT";
                _document.Save();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error al " + step + ": " + ex.Message, ex);
            }
        }

        private void ExportRemainingStock(string targetPath)
        {
            if (_document.PartOperations.Count == 0)
                throw new InvalidOperationException(
                    "No hay operaciones desde las que calcular el material restante.");

            Esprit.PartOperation lastOperation = _document.PartOperations[
                _document.PartOperations.Count];
            Array calculatedStocks;
            _document.StockAutomation.GetCalculatedStocks(
                lastOperation, false, out calculatedStocks);
            if (calculatedStocks == null || calculatedStocks.Length == 0)
                throw new InvalidOperationException(
                    "ESPRIT no devolvió material restante para la última operación.");

            string sourcePath = null;
            double largestVolume = double.NegativeInfinity;
            foreach (object item in calculatedStocks)
            {
                if (!(item is Esprit.CalculatedStockData stock)) continue;
                if (string.IsNullOrWhiteSpace(stock.FileName) || !File.Exists(stock.FileName)) continue;
                if (stock.Volume <= largestVolume) continue;
                largestVolume = stock.Volume;
                sourcePath = stock.FileName;
            }
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new FileNotFoundException(
                    "No se pudo localizar el archivo temporal del material restante generado por ESPRIT.");
            File.Copy(sourcePath, targetPath, true);
        }

        private void RemoveAllFeatures()
        {
            RemoveCollectionItems(_document.FeatureSets);
            RemoveCollectionItems(_document.HolesFeatures);
            RemoveCollectionItems(_document.FreeFormFeatures);
            RemoveCollectionItems(_document.RuledFeatures);
            RemoveCollectionItems(_document.DraftConicFeatures);
            RemoveCollectionItems(_document.TurningFeatureChains);
            RemoveCollectionItems(_document.FeaturePtops);
            RemoveCollectionItems(_document.FeatureChains);
            RemoveCollectionItems(_document.AdditiveFeatures);
            _document.CadFeatures.RemoveAll();
        }

        private void ClearAdditionalMachineSetups()
        {
            for (int index = _document.MachineSetupSequences.Count; index > 0; index--)
            {
                Esprit.MachineSetupSequence sequence = _document.MachineSetupSequences[index];
                try
                {
                    // Algunas instalaciones implementan este miembro de la
                    // typelib y permiten eliminar realmente la secuencia.
                    _document.MachineSetupSequences.Remove(index);
                    continue;
                }
                catch (COMException)
                {
                    // ESPRIT 2026.2 puede exponer Remove pero responder
                    // "Sub o Function no definida". En ese caso se deja la
                    // secuencia completamente vacía e inactiva.
                }

                Esprit.MachineSetup setup = sequence.MachineSetup;
                setup.BeginEdit();
                try
                {
                    ClearMachineItems(setup.MachineItems);
                    setup.EndEdit();
                }
                catch
                {
                    try { setup.CancelEdit(); } catch { }
                    throw;
                }
                sequence.Enabled = false;
            }
        }

        private static void RemoveCollectionItems(object collection)
        {
            if (collection == null) return;
            dynamic items = collection;
            for (int index = Convert.ToInt32(items.Count); index > 0; index--)
                items.Remove(index);
        }

        private static void AddFileStock(Esprit.Workpiece workpiece, string name,
            string fileName)
        {
            Esprit.FileStock stock = workpiece.Stocks.AddFileStock();
            stock.Name = name;
            stock.FileName = fileName;
        }

        private static void ClearMachineItems(Esprit.MachineItems items)
        {
            for (int index = items.Count; index > 0; index--)
                items.Remove(index);
        }

        private void ConfigurePartCore(Esprit.Solid partSolid, Action<Esprit.Workpiece> addStock)
        {
            Esprit.PartSetup setup = _document.PartSetup;
            setup.BeginEdit();
            try
            {
                Esprit.Workpiece workpiece = _document.Workpieces.Count > 0
                    ? _document.Workpieces[1]
                    : _document.Workpieces.Add();
                if (string.IsNullOrWhiteSpace(workpiece.Name)) workpiece.Name = "Pieza1";

                Esprit.Part part = workpiece.Parts.Count > 0
                    ? workpiece.Parts[1]
                    : workpiece.Parts.Add();
                if (string.IsNullOrWhiteSpace(part.Name)) part.Name = "Pieza1";
                for (int index = part.Geometry.Count; index > 0; index--)
                    part.Geometry.Remove(index);
                part.Geometry.Add(partSolid);

                for (int index = workpiece.Stocks.Count; index > 0; index--)
                    workpiece.Stocks.Remove(index);
                addStock?.Invoke(workpiece);

                setup.EndEdit();
            }
            catch
            {
                try { setup.CancelEdit(); } catch { }
                throw;
            }
        }

        public void ValidateExistingWorkpiece()
        {
            if (_document.Workpieces.Count == 0)
                throw new InvalidOperationException(
                    "El documento no contiene una pieza de trabajo de una fase anterior.");

            Esprit.Workpiece workpiece = _document.Workpieces[1];
            if (workpiece.Parts.Count == 0)
                throw new InvalidOperationException(
                    "La pieza de trabajo existente no tiene una pieza definida.");
        }

        public void MountFixturesAndWorkpiece(IEnumerable<string> fixturePaths,
            bool createNewConfiguration, bool rotateAssembly, double mountingStackHeight,
            double workOffsetBaseZ, double gripDepth, WorkZeroSpec workZero,
            IEnumerable<MountingElementSpec> baseElements = null)
        {
            var paths = fixturePaths.Where(File.Exists).ToList();
            var bases = (baseElements ?? Enumerable.Empty<MountingElementSpec>())
                .Where(item => item != null && File.Exists(item.FileName)).ToList();
            // EndEdit invalida algunos wrappers COM cuando la pieza se crea desde cero.
            // Se obtiene una referencia nueva justo antes de editar la máquina.
            Esprit.Workpiece workpiece = _document.Workpieces[1];
            if (workpiece == null)
                throw new InvalidOperationException("No se pudo recuperar la pieza de trabajo configurada.");

            int workOffsetNumber = FindNextAvailableWorkOffsetNumber();

            string step = createNewConfiguration
                ? "crear la secuencia de la nueva configuración"
                : "iniciar la edición de máquina";
            Esprit.MachineSetup setup = null;
            try
            {
                if (createNewConfiguration)
                {
                    Esprit.MachineSetupSequence sequence = _document.MachineSetupSequences.Add();
                    step = "obtener la nueva configuración de máquina";
                    setup = sequence.MachineSetup;
                    step = "iniciar la edición de la nueva configuración";
                    setup.BeginEdit();
                    step = "copiar la configuración anterior";
                    setup.CopyPreviousSetup();
                }
                else
                {
                    setup = _document.InitialMachineSetup;
                    setup.BeginEdit();
                }

                if (createNewConfiguration)
                {
                    step = "vaciar el montaje copiado en la nueva configuración";
                    ClearMachineItems(setup.MachineItems);
                }

                Esprit.MachineItems mountingItems = setup.MachineItems;
                Esprit.Fixture rootFixture = null;
                Esprit.Fixture topFixture = null;
                string topFixtureSourceFile = null;
                double? pendingJawGripTranslation = null;
                foreach (MountingElementSpec element in bases)
                {
                    if (element.Type == MountingElementType.Accessory)
                    {
                        step = "cargar el accesorio " + Path.GetFileName(element.FileName);
                        Esprit.Fixture accessory = mountingItems.AddFixture(element.FileName);
                        accessory.Name = Path.GetFileNameWithoutExtension(element.FileName);
                        if (pendingJawGripTranslation.HasValue)
                        {
                            accessory.ZTranslation = accessory.ZTranslation +
                                pendingJawGripTranslation.Value;
                            pendingJawGripTranslation = null;
                        }
                        if (rootFixture == null) rootFixture = accessory;
                        topFixture = accessory;
                        topFixtureSourceFile = element.FileName;
                        mountingItems = accessory.MachineItems;
                    }
                    else
                    {
                        step = "cargar las garras " + Path.GetFileName(element.FileName);
                        if (topFixture == null)
                            throw new InvalidOperationException(
                                "Las garras necesitan una mordaza o accesorio padre.");
                        if (!mountingItems.CanAddMachineItem(
                            EspritConstants.espMachineItemType.espMachineItemTypeJaw))
                            throw new InvalidOperationException(
                                "El accesorio seleccionado no admite garras en este punto del montaje.");
                        List<int> selectedAdaptors = element.JawAdapterIndices
                            .Distinct().OrderBy(index => index).ToList();
                        if (selectedAdaptors.Count == 0)
                            throw new InvalidOperationException(
                                "Seleccione al menos un punto de montaje para las garras.");
                        topFixture.ReverseJawsDirection = element.ReverseJaws;
                        // ESPRIT carga el GDML de garras una sola vez y devuelve una
                        // instancia por cada punto de montaje del accesorio. Crear la
                        // misma garra repetidamente con AddJaw deja referencias COM no
                        // válidas en algunos conjuntos. Cargamos el conjunto nativo y
                        // retiramos las posiciones que el usuario no ha seleccionado.
                        Array addedJaws = mountingItems.AddJaws(element.FileName, 0.0);
                        if (addedJaws == null || addedJaws.Length == 0)
                            throw new InvalidOperationException(
                                "ESPRIT no encontró puntos de montaje compatibles para las garras.");
                        if (selectedAdaptors.Any(index => index < 1 || index > addedJaws.Length))
                            throw new InvalidOperationException(
                                "El accesorio no dispone de todos los puntos de montaje seleccionados.");
                        for (int index = addedJaws.Length; index >= 1; index--)
                        {
                            if (!selectedAdaptors.Contains(index))
                            {
                                Esprit.Jaw jaw = addedJaws.GetValue(index - 1) as Esprit.Jaw;
                                if (jaw == null || string.IsNullOrWhiteSpace(jaw.Key))
                                    throw new InvalidOperationException(
                                        "ESPRIT no devolvió una clave válida para la garra " + index + ".");
                                mountingItems.Remove(jaw.Key);
                            }
                        }
                        pendingJawGripTranslation =
                            ReadJawTopZ(element.FileName) - Math.Abs(gripDepth) -
                            ReadWorkpieceToJawAdapterOffset(topFixtureSourceFile,
                                selectedAdaptors[0]);
                    }
                }
                foreach (string path in paths)
                {
                    step = "cargar el accesorio " + Path.GetFileName(path);
                    Esprit.Fixture fixture = mountingItems.AddFixture(path);
                    fixture.Name = Path.GetFileNameWithoutExtension(path);
                    if (pendingJawGripTranslation.HasValue)
                    {
                        fixture.ZTranslation = fixture.ZTranslation +
                            pendingJawGripTranslation.Value;
                        pendingJawGripTranslation = null;
                    }
                    if (rootFixture == null) rootFixture = fixture;
                    topFixture = fixture;
                    topFixtureSourceFile = path;
                    mountingItems = fixture.MachineItems;
                }

                if (rotateAssembly && rootFixture != null)
                {
                    step = "girar el montaje 180 grados sobre Y y después sobre Z";
                    rootFixture.YRotation = 180.0;
                    rootFixture.ZRotation = 180.0;
                    step = "trasladar el montaje girado sobre la mesa";
                    rootFixture.ZTranslation = rootFixture.ZTranslation + mountingStackHeight;
                }

                step = "crear la instancia de pieza sobre el accesorio";
                Esprit.WorkpieceInstance instance = mountingItems.AddWorkpieceInstance();
                if (pendingJawGripTranslation.HasValue)
                    instance.ZTranslation = instance.ZTranslation +
                        pendingJawGripTranslation.Value;
                if (rotateAssembly && rootFixture == null)
                {
                    step = "girar la pieza 180 grados sobre Y y después sobre Z";
                    instance.YRotation = 180.0;
                    instance.ZRotation = 180.0;
                    instance.ZTranslation = instance.ZTranslation + mountingStackHeight;
                }
                // AddWorkpieceInstance enlaza automáticamente la primera pieza de
                // trabajo. Reasignar PartWorkpiece después de crearla desde cero
                // provoca "objeto gráfico no válido" en ESPRIT.
                step = "asignar el nombre a la instancia de pieza";
                instance.Name = workpiece.Name;

                step = "asignar el siguiente cero de trabajo disponible";
                Esprit.WorkOffset workOffset = instance.WorkOffsets.Count > 0
                    ? instance.WorkOffsets[1]
                    : instance.WorkOffsets.Add();
                workOffset.WorkOffsetType =
                    EspritConstants.espWorkOffsetType.espWorkOffsetTypeStandard;
                workOffset.PrimaryNumber = workOffsetNumber;
                workOffset.SecondaryNumber = 0;
                workOffset.Name = "G" + workOffsetNumber;

                bool manualZero = workZero != null && workZero.IsManual;
                double workZeroX = manualZero ? workZero.X : 0.0;
                double workZeroY = manualZero ? workZero.Y : 0.0;
                double workZeroZ = manualZero ? workZero.Z : workOffsetBaseZ;
                step = manualZero
                    ? "colocar el cero en el punto seleccionado"
                    : "colocar el cero en la base del primer accesorio";
                workOffset.PartShiftX = workZeroX;
                workOffset.PartShiftY = workZeroY;
                workOffset.PartShiftZ = workZeroZ;

                step = "finalizar la edición de máquina";
                setup.EndEdit();
                if (workZero == null || workZero.CreateWorkPlane)
                {
                    step = "crear el plano de trabajo " + workOffset.Name;
                    CreateOrUpdateWorkPlane(workOffset.Name, workZeroX, workZeroY,
                        workZeroZ, rotateAssembly);
                }
                _document.Refresh();
            }
            catch (Exception ex)
            {
                if (setup != null)
                {
                    try { setup.CancelEdit(); } catch { }
                }
                throw new InvalidOperationException("Error al " + step + ": " + ex.Message, ex);
            }
        }

        private static double ReadJawTopZ(string jawFile)
        {
            if (string.IsNullOrWhiteSpace(jawFile) || !File.Exists(jawFile))
                throw new FileNotFoundException(
                    "No se encontró el archivo GDML de las garras.", jawFile);
            try
            {
                XDocument document = XDocument.Load(jawFile);
                Assembly gdmlAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(
                        assembly.GetName().Name, "gdml",
                        StringComparison.OrdinalIgnoreCase));
                if (gdmlAssembly == null)
                {
                    string executable = Process.GetCurrentProcess().MainModule?.FileName;
                    string programFolder = Path.GetDirectoryName(executable);
                    string gdmlPath = Path.Combine(programFolder ?? string.Empty, "gdml.dll");
                    if (!File.Exists(gdmlPath))
                        throw new FileNotFoundException(
                            "No se encontró el lector GDML en la carpeta de ESPRIT.",
                            gdmlPath);
                    gdmlAssembly = Assembly.LoadFrom(gdmlPath);
                }
                MethodInfo readFaceMesh = gdmlAssembly
                    .GetType("gdml.FaceMeshExtensions", true)
                    .GetMethod("ReadFromMemory", BindingFlags.Public | BindingFlags.Static);
                if (readFaceMesh == null)
                    throw new MissingMethodException(
                        "No se encontró el lector de mallas GDML de ESPRIT.");
                double maximumZ = double.NegativeInfinity;
                foreach (XElement binary in document.Descendants()
                    .Where(node => node.Name.LocalName == "Binary"))
                {
                    if (string.IsNullOrWhiteSpace(binary.Value)) continue;
                    byte[] data = Convert.FromBase64String(binary.Value);
                    object mesh = readFaceMesh.Invoke(null, new object[] { data });
                    if (mesh == null) continue;
                    Type meshType = mesh.GetType();
                    if (!(bool)meshType.GetProperty("IsValid").GetValue(mesh)) continue;
                    var vertices = meshType.GetProperty("Vertices").GetValue(mesh)
                        as System.Collections.IEnumerable;
                    if (vertices == null) continue;
                    foreach (object vertex in vertices)
                    {
                        Type vertexType = vertex.GetType();
                        double x = Convert.ToDouble(vertexType.GetProperty("X")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        double y = Convert.ToDouble(vertexType.GetProperty("Y")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        double z = Convert.ToDouble(vertexType.GetProperty("Z")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        GdmlPoint transformed = ApplyGdmlTransformations(binary, x, y, z);
                        maximumZ = Math.Max(maximumZ, transformed.Z);
                    }
                }
                if (double.IsNegativeInfinity(maximumZ))
                    throw new InvalidDataException(
                        "El GDML no contiene una malla de sólidos válida.");
                return maximumZ;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "No se pudo medir la altura de las garras desde su plano de montaje: " +
                    ex.Message, ex);
            }
        }

        internal static List<PointF> ReadGdmlTopView(string gdmlFile)
        {
            var result = new List<PointF>();
            if (string.IsNullOrWhiteSpace(gdmlFile) || !File.Exists(gdmlFile))
                return result;
            try
            {
                XDocument document = XDocument.Load(gdmlFile);
                Assembly gdmlAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(
                        assembly.GetName().Name, "gdml",
                        StringComparison.OrdinalIgnoreCase));
                if (gdmlAssembly == null)
                {
                    string executable = Process.GetCurrentProcess().MainModule?.FileName;
                    string programFolder = Path.GetDirectoryName(executable);
                    gdmlAssembly = Assembly.LoadFrom(Path.Combine(
                        programFolder ?? string.Empty, "gdml.dll"));
                }
                MethodInfo readFaceMesh = gdmlAssembly
                    .GetType("gdml.FaceMeshExtensions", true)
                    .GetMethod("ReadFromMemory", BindingFlags.Public | BindingFlags.Static);
                foreach (XElement binary in document.Descendants()
                    .Where(node => node.Name.LocalName == "Binary"))
                {
                    if (string.IsNullOrWhiteSpace(binary.Value)) continue;
                    object mesh = readFaceMesh?.Invoke(null,
                        new object[] { Convert.FromBase64String(binary.Value) });
                    if (mesh == null) continue;
                    Type meshType = mesh.GetType();
                    var vertices = meshType.GetProperty("Vertices")?.GetValue(mesh)
                        as System.Collections.IEnumerable;
                    if (vertices == null) continue;
                    int sample = 0;
                    foreach (object vertex in vertices)
                    {
                        // Un muestreo moderado conserva la silueta y evita que el
                        // repintado de WinForms se vuelva pesado con mallas densas.
                        if ((sample++ & 3) != 0) continue;
                        Type vertexType = vertex.GetType();
                        double x = Convert.ToDouble(vertexType.GetProperty("X")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        double y = Convert.ToDouble(vertexType.GetProperty("Y")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        double z = Convert.ToDouble(vertexType.GetProperty("Z")
                            .GetValue(vertex), CultureInfo.InvariantCulture);
                        GdmlPoint transformed = ApplyGdmlTransformations(binary, x, y, z);
                        result.Add(new PointF((float)transformed.X, (float)transformed.Y));
                    }
                }
            }
            catch { }
            return result;
        }

        private static GdmlPoint ApplyGdmlTransformations(
            XElement geometryNode, double x, double y, double z)
        {
            var point = new GdmlPoint(x, y, z);
            // Ancestors() devuelve primero el nodo más cercano. Ese es justamente
            // el orden necesario: sólido local, grupo padre y grupos exteriores.
            foreach (XElement owner in geometryNode.Ancestors())
            {
                XElement transformation = owner.Elements()
                    .FirstOrDefault(node => node.Name.LocalName == "Transformation");
                if (transformation == null) continue;

                XElement rotation = transformation.Elements()
                    .FirstOrDefault(node => node.Name.LocalName == "Rotation");
                if (rotation != null && TryReadVector(
                        rotation.Attribute("rotAxis")?.Value, out GdmlPoint axis) &&
                    double.TryParse(rotation.Attribute("rotAngle")?.Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double angle))
                {
                    GdmlPoint center = TryReadVector(
                        rotation.Attribute("rotCenter")?.Value, out GdmlPoint parsedCenter)
                        ? parsedCenter
                        : new GdmlPoint(0.0, 0.0, 0.0);
                    point = RotateAroundAxis(point, center, axis, angle);
                }

                XElement translation = transformation.Elements()
                    .FirstOrDefault(node => node.Name.LocalName == "Translation");
                if (translation != null && TryReadVector(
                        translation.Attribute("trans")?.Value, out GdmlPoint offset))
                    point = new GdmlPoint(
                        point.X + offset.X, point.Y + offset.Y, point.Z + offset.Z);
            }
            return point;
        }

        private static GdmlPoint RotateAroundAxis(GdmlPoint point, GdmlPoint center,
            GdmlPoint axis, double angleDegrees)
        {
            double length = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
            if (length < 1e-12) return point;
            double ux = axis.X / length, uy = axis.Y / length, uz = axis.Z / length;
            double px = point.X - center.X, py = point.Y - center.Y,
                pz = point.Z - center.Z;
            double radians = angleDegrees * Math.PI / 180.0;
            double cosine = Math.Cos(radians), sine = Math.Sin(radians);
            double dot = ux * px + uy * py + uz * pz;
            return new GdmlPoint(
                center.X + px * cosine + (uy * pz - uz * py) * sine + ux * dot * (1 - cosine),
                center.Y + py * cosine + (uz * px - ux * pz) * sine + uy * dot * (1 - cosine),
                center.Z + pz * cosine + (ux * py - uy * px) * sine + uz * dot * (1 - cosine));
        }

        private static bool TryReadVector(string value, out GdmlPoint point)
        {
            point = new GdmlPoint();
            string[] coordinates = value?.Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            if (coordinates == null || coordinates.Length < 3) return false;
            if (!double.TryParse(coordinates[0], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(coordinates[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double y) ||
                !double.TryParse(coordinates[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double z)) return false;
            point = new GdmlPoint(x, y, z);
            return true;
        }

        private struct GdmlPoint
        {
            public GdmlPoint(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X;
            public double Y;
            public double Z;
        }

        private static double ReadWorkpieceToJawAdapterOffset(string fixtureFile,
            int jawAdapterIndex)
        {
            if (string.IsNullOrWhiteSpace(fixtureFile) || !File.Exists(fixtureFile))
                throw new FileNotFoundException(
                    "No se encontró el GDML del accesorio que contiene las garras.",
                    fixtureFile);
            try
            {
                XDocument document = XDocument.Load(fixtureFile);
                XElement workpieceAdapter = document.Descendants()
                    .FirstOrDefault(node => node.Name.LocalName == "WorkpieceAdapter");
                XElement jawAdapter = document.Descendants()
                    .Where(node => node.Name.LocalName == "Adapter")
                    .Skip(Math.Max(0, jawAdapterIndex - 1)).FirstOrDefault();
                if (workpieceAdapter == null || jawAdapter == null)
                    throw new InvalidDataException(
                        "El accesorio no contiene un WorkpieceAdapter y un adaptador de garra.");

                double workpieceZ = ReadTranslationZ(workpieceAdapter, "WorkpieceAdapter");
                double adapterZ = ReadTranslationZ(jawAdapter, "adaptador de garra");
                return workpieceZ - adapterZ;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                throw new InvalidDataException(
                    "No se pudo calcular la diferencia entre el WorkpieceAdapter y " +
                    "el adaptador de las garras: " + ex.Message, ex);
            }
        }

        private static double ReadTranslationZ(XElement node, string description)
        {
            XElement translation = node.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Translation");
            string value = translation?.Attribute("trans")?.Value;
            string[] coordinates = value?.Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            if (coordinates == null || coordinates.Length < 3 ||
                !double.TryParse(coordinates[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double z))
                throw new InvalidDataException(
                    "No se encontró una coordenada Z válida en el " + description + ".");
            return z;
        }

        private void CreateOrUpdateWorkPlane(string name, double x, double y, double z,
            bool rotateAssembly)
        {
            Esprit.Plane plane = null;
            foreach (Esprit.Plane candidate in _document.Planes)
            {
                if (!string.Equals(candidate.Name, name,
                    StringComparison.OrdinalIgnoreCase)) continue;
                plane = candidate;
                break;
            }
            if (plane == null) plane = _document.Planes.Add(name);

            plane.IsWork = true;
            plane.X = x;
            plane.Y = y;
            plane.Z = z;
            plane.Ux = 1.0;
            plane.Uy = 0.0;
            plane.Uz = 0.0;
            plane.Vx = 0.0;
            plane.Vy = rotateAssembly ? -1.0 : 1.0;
            plane.Vz = 0.0;
            plane.Wx = 0.0;
            plane.Wy = 0.0;
            plane.Wz = rotateAssembly ? -1.0 : 1.0;
            plane.Activate();
        }

        private int FindNextAvailableWorkOffsetNumber()
        {
            var used = new HashSet<int>();
            CollectWorkOffsetNumbers(_document.InitialMachineSetup.MachineItems, used);
            foreach (Esprit.MachineSetupSequence sequence in _document.MachineSetupSequences)
                CollectWorkOffsetNumbers(sequence.MachineSetup.MachineItems, used);

            int candidate = 54;
            while (used.Contains(candidate)) candidate++;
            return candidate;
        }

        private static void CollectWorkOffsetNumbers(Esprit.MachineItems items, HashSet<int> used)
        {
            if (items == null) return;
            for (int index = 1; index <= items.Count; index++)
            {
                Esprit.MachineItem item = items[index];
                if (item is Esprit.WorkpieceInstance instance)
                {
                    foreach (Esprit.WorkOffset offset in instance.WorkOffsets)
                    {
                        if (offset.PrimaryNumber >= 54) used.Add(offset.PrimaryNumber);
                        if (!string.IsNullOrWhiteSpace(offset.Name) &&
                            offset.Name.StartsWith("G", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(offset.Name.Substring(1), out int number))
                            used.Add(number);
                    }
                }
                CollectWorkOffsetNumbers(item.MachineItems, used);
            }
        }
    }
}
