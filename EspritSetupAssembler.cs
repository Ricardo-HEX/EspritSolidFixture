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

        public string ConfigurePartAndStock(Esprit.Solid partSolid, Esprit.Solid stockSolid)
        {
            if (partSolid == null) throw new ArgumentNullException(nameof(partSolid));
            if (stockSolid == null) throw new ArgumentNullException(nameof(stockSolid));

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

                Esprit.SolidStock stock = workpiece.Stocks.AddSolidStock();
                stock.Name = "Material1";
                stock.Tolerance = 0.1;
                stock.Solid = (EspritSolids.SolidBody)stockSolid.SolidBody;

                string workpieceKey = workpiece.Key;
                setup.EndEdit();
                return workpieceKey;
            }
            catch
            {
                try { setup.CancelEdit(); } catch { }
                throw;
            }
        }

        public string GetExistingWorkpieceKey()
        {
            if (_document.Workpieces.Count == 0)
                throw new InvalidOperationException(
                    "El documento no contiene una pieza de trabajo de una fase anterior.");

            Esprit.Workpiece workpiece = _document.Workpieces[1];
            if (workpiece.Parts.Count == 0)
                throw new InvalidOperationException(
                    "La pieza de trabajo existente no tiene una pieza definida.");
            return workpiece.Key;
        }

        public void MountFixturesAndWorkpiece(IEnumerable<string> fixturePaths, string workpieceKey,
            bool createNewConfiguration, bool rotateAssembly, double mountingStackHeight,
            double workOffsetBaseZ)
        {
            var paths = fixturePaths.Where(File.Exists).ToList();
            if (paths.Count == 0) throw new InvalidOperationException("No hay archivos GDML para montar.");
            if (string.IsNullOrWhiteSpace(workpieceKey))
                throw new ArgumentException("La pieza de trabajo no tiene una clave válida.", nameof(workpieceKey));

            // EndEdit invalida algunos wrappers COM cuando la pieza se crea desde cero.
            // Se obtiene una referencia nueva por clave justo antes de editar la máquina.
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
