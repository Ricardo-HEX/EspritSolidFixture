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

        public void MountFixturesAndWorkpiece(IEnumerable<string> fixturePaths, string workpieceKey)
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

            Esprit.MachineSetup setup = _document.InitialMachineSetup;
            string step = "iniciar la edición de máquina";
            setup.BeginEdit();
            try
            {
                Esprit.MachineItems mountingItems = setup.MachineItems;
                Esprit.Fixture topFixture = null;
                foreach (string path in paths)
                {
                    step = "cargar el accesorio " + Path.GetFileName(path);
                    Esprit.Fixture fixture = mountingItems.AddFixture(path);
                    fixture.Name = Path.GetFileNameWithoutExtension(path);
                    topFixture = fixture;
                    mountingItems = fixture.MachineItems;
                }

                step = "crear la instancia de pieza sobre el accesorio";
                Esprit.WorkpieceInstance instance = topFixture.MachineItems.AddWorkpieceInstance();
                // AddWorkpieceInstance enlaza automáticamente la primera pieza de
                // trabajo. Reasignar PartWorkpiece después de crearla desde cero
                // provoca "objeto gráfico no válido" en ESPRIT.
                step = "asignar el nombre a la instancia de pieza";
                instance.Name = workpiece.Name;

                step = "finalizar la edición de máquina";
                setup.EndEdit();
                _document.Refresh();
            }
            catch (Exception ex)
            {
                try { setup.CancelEdit(); } catch { }
                throw new InvalidOperationException("Error al " + step + ": " + ex.Message, ex);
            }
        }
    }
}
