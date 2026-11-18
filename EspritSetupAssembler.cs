using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public void ConfigurePartOnly(Esprit.Solid partSolid)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            ConfigurePartCore(partSolid, null);
        }

        public void ConfigurePartAndFileStock(Esprit.Solid partSolid, string fileName)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                throw new FileNotFoundException("No se encontró el archivo de bruto seleccionado.", fileName);

            ConfigurePartCore(partSolid, workpiece =>
            {
                Esprit.FileStock stock = workpiece.Stocks.AddFileStock();
                stock.Name = "Material1";
                stock.FileName = fileName;
            });
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
                Esprit.FileStock stock = workpiece.Stocks.AddFileStock();
                stock.Name = "Material_Fase_Anterior";
                stock.FileName = fileName;
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
            double mountingStackHeight, double workOffsetBaseZ)
        {
            var paths = fixturePaths.Where(File.Exists).ToList();
            if (paths.Count == 0)
                throw new InvalidOperationException("No hay archivos GDML para montar.");
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
                    Path.GetFileNameWithoutExtension(newEspritPath) + "_Material_Anterior.stl");
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
                for (int index = _document.MachineSetupSequences.Count; index > 0; index--)
                    _document.MachineSetupSequences.Remove(index);

                step = "vaciar el montaje y cargar la nueva máquina";
                Esprit.MachineSetup setup = _document.InitialMachineSetup;
                setup.BeginEdit();
                try
                {
                    for (int index = setup.MachineItems.Count; index > 0; index--)
                        setup.MachineItems.Remove(index);
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
                    mountingStackHeight, workOffsetBaseZ);
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

        private static void RemoveCollectionItems(object collection)
        {
            if (collection == null) return;
            dynamic items = collection;
            for (int index = Convert.ToInt32(items.Count); index > 0; index--)
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
            double workOffsetBaseZ)
        {
            var paths = fixturePaths.Where(File.Exists).ToList();
            if (paths.Count == 0) throw new InvalidOperationException("No hay archivos GDML para montar.");
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
                    for (int index = setup.MachineItems.Count; index > 0; index--)
                        setup.MachineItems.Remove(index);
                }

                Esprit.MachineItems mountingItems = setup.MachineItems;
                Esprit.Fixture rootFixture = null;
                Esprit.Fixture topFixture = null;
                foreach (string path in paths)
                {
                    step = "cargar el accesorio " + Path.GetFileName(path);
                    Esprit.Fixture fixture = mountingItems.AddFixture(path);
                    fixture.Name = Path.GetFileNameWithoutExtension(path);
                    if (rootFixture == null) rootFixture = fixture;
                    topFixture = fixture;
                    mountingItems = fixture.MachineItems;
                }

                if (rotateAssembly)
                {
                    step = "girar el montaje 180 grados sobre Y y después sobre Z";
                    rootFixture.YRotation = 180.0;
                    rootFixture.ZRotation = 180.0;
                    step = "trasladar el montaje girado sobre la mesa";
                    rootFixture.ZTranslation = rootFixture.ZTranslation + mountingStackHeight;
                }

                step = "crear la instancia de pieza sobre el accesorio";
                Esprit.WorkpieceInstance instance = topFixture.MachineItems.AddWorkpieceInstance();
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

                step = "colocar el cero en la base del primer accesorio";
                // El usuario define el cero desde el plano XYZ absoluto del CAD.
                // No se desplaza X/Y. En montaje normal se usa la Z mínima de los
                // accesorios; al girar el conjunto se usa su Z máxima original.
                workOffset.PartShiftX = 0.0;
                workOffset.PartShiftY = 0.0;
                workOffset.PartShiftZ = workOffsetBaseZ;

                step = "finalizar la edición de máquina";
                setup.EndEdit();
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
