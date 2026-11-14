using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace EspritSolidFixture
{
    internal sealed class GdmlFixtureExporter
    {
        private readonly Esprit.Document _document;

        public GdmlFixtureExporter(Esprit.Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public IReadOnlyList<string> Export(IEnumerable<Esprit.Solid> solids, string folder, int phase,
            bool combine, bool mountFromTop)
        {
            Directory.CreateDirectory(folder);
            var paths = new List<string>();
            var sourceSolids = solids.ToList();
            MirrorResources mirror = null;

            try
            {
                List<Esprit.Solid> exportSolids = sourceSolids;
                if (mountFromTop)
                {
                    mirror = CreateMirroredCopies(sourceSolids);
                    exportSolids = mirror.Copies;
                    if (exportSolids.Count != sourceSolids.Count)
                        throw new InvalidOperationException("ESPRIT no creó una copia reflejada para cada sólido.");
                }

                if (combine && exportSolids.Count > 1)
                {
                    var items = exportSolids.Select(solid => new SolidBounds(solid, GetBounds(solid))).ToList();
                    SolidBounds lowest = items.OrderBy(x => x.Bounds.MinZ).First();
                    SolidBounds highest = items.OrderByDescending(x => x.Bounds.MaxZ).First();
                    string cadName = GetCadName(sourceSolids[0]);
                    string fileName = string.Format(CultureInfo.InvariantCulture,
                        "Utillaje_{0}_Fase_{1}_Combinado{2}.gdml", cadName, phase,
                        mountFromTop ? "_Superior" : string.Empty);
                    string path = MakeUniquePath(Path.Combine(folder, fileName));

                    _document.Group.Clear();
                    foreach (Esprit.Solid solid in exportSolids) _document.Group.Add(solid);
                    _document.SaveAsGDML(path, EspritConstants.espFileType.espFileTypeFixtures);

                    NormalizeFixture(path, cadName, lowest.Bounds.MinZ, highest.Bounds.MaxZ);
                    paths.Add(path);
                }
                else
                {
                    for (int index = 0; index < exportSolids.Count; index++)
                    {
                        Esprit.Solid solid = exportSolids[index];
                        Esprit.Solid sourceSolid = sourceSolids[index];
                        Bounds bounds = GetBounds(solid);
                        string cadName = GetCadName(sourceSolid);
                        string fileName = string.Format(CultureInfo.InvariantCulture,
                            "Utillaje_{0}_Fase_{1}_Solido_{2}{3}.gdml",
                            cadName, phase, sourceSolid.Number,
                            mountFromTop ? "_Superior" : string.Empty);
                        string path = MakeUniquePath(Path.Combine(folder, fileName));

                        _document.Group.Clear();
                        _document.Group.Add(solid);
                        _document.SaveAsGDML(path, EspritConstants.espFileType.espFileTypeFixtures);

                        NormalizeFixture(path, cadName, bounds.MinZ, bounds.MaxZ);
                        paths.Add(path);
                    }
                }
            }
            finally
            {
                if (mirror != null) DeleteMirroredCopies(mirror);
                _document.Group.Clear();
                foreach (Esprit.Solid solid in sourceSolids) _document.Group.Add(solid);
                _document.Refresh();
            }

            return paths;
        }

        private MirrorResources CreateMirroredCopies(List<Esprit.Solid> sourceSolids)
        {
            var existingKeys = new HashSet<string>(
                _document.Solids.Cast<Esprit.Solid>().Select(x => Convert.ToString(x.Key)));
            string suffix = Guid.NewGuid().ToString("N");
            string planeName = "EspritSolidFixture_MirrorPlane_" + suffix;
            string selectionName = "EspritSolidFixture_MirrorSelection_" + suffix;

            Esprit.Plane plane = _document.Planes.Add(planeName);
            plane.X = 0; plane.Y = 0; plane.Z = 0;
            plane.Ux = 1; plane.Uy = 0; plane.Uz = 0;
            plane.Vx = 0; plane.Vy = 1; plane.Vz = 0;
            plane.Wx = 0; plane.Wy = 0; plane.Wz = 1;

            Esprit.SelectionSet selection = _document.SelectionSets.Add(selectionName);
            foreach (Esprit.Solid solid in sourceSolids) selection.Add(solid);
            selection.AddCopiesToSelectionSet = true;
            selection.Mirror(plane, true, true);

            var copies = _document.Solids.Cast<Esprit.Solid>()
                .Where(x => !existingKeys.Contains(Convert.ToString(x.Key)))
                .OrderBy(x => x.Number)
                .ToList();
            return new MirrorResources(planeName, selectionName, copies);
        }

        private void DeleteMirroredCopies(MirrorResources mirror)
        {
            try
            {
                string deleteName = "EspritSolidFixture_Delete_" + Guid.NewGuid().ToString("N");
                Esprit.SelectionSet deleteSet = _document.SelectionSets.Add(deleteName);
                foreach (Esprit.Solid solid in mirror.Copies) deleteSet.Add(solid);
                deleteSet.DeleteAll();
                _document.SelectionSets.Remove(deleteName);
            }
            catch { }
            try { _document.SelectionSets.Remove(mirror.SelectionName); } catch { }
            try { _document.Planes.Remove(mirror.PlaneName); } catch { }
        }

        private static Bounds GetBounds(Esprit.Solid solid)
        {
            EspritSolids.ISolidBody body = (EspritSolids.ISolidBody)solid.SolidBody;
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double maxZ = double.NegativeInfinity;
            int vertexCount = 0;

            foreach (EspritSolids.ISolidVertex vertex in body.SolidVertices)
            {
                EspritGeometryBase.IComPoint point = vertex.Geometry;
                double x = point.X;
                double y = point.Y;
                double z = point.Z;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
                vertexCount++;
            }

            if (vertexCount == 0)
                throw new InvalidDataException("No se pudieron obtener los vértices del sólido.");

            return new Bounds(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static void NormalizeFixture(string path, string cadName,
            double baseZ, double topZ)
        {
            XDocument gdml = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement fixture = gdml.Descendants("Fixture").FirstOrDefault();
            if (fixture == null)
                throw new InvalidDataException("El GDML generado no contiene un nodo Fixture.");

            fixture.SetAttributeValue("nodeName", "Utillaje " + cadName);
            fixture.SetAttributeValue("clampCheckType", "NEAREST_FIXTURE");

            // ESPRIT exporta el sólido en las coordenadas del documento. Se desplaza
            // la geometría para que el origen quede en la cara de montaje elegida.
            List<XElement> geometryNodes = fixture.Elements()
                .Where(x => x.DescendantsAndSelf("Solid").Any()).ToList();
            if (geometryNodes.Count == 0)
                throw new InvalidDataException("El GDML generado no contiene geometría sólida.");

            XElement geometryNode;
            if (geometryNodes.Count == 1)
            {
                geometryNode = geometryNodes[0];
            }
            else
            {
                geometryNode = new XElement("Group", new XAttribute("nodeName", "Geometría combinada"));
                geometryNodes[0].AddBeforeSelf(geometryNode);
                foreach (XElement node in geometryNodes) geometryNode.Add(node);
            }
            geometryNode.Elements("Transformation").Remove();
            geometryNode.Add(new XElement("Transformation",
                new XElement("Translation", new XAttribute("trans", Vector(
                    0, 0, -baseZ)))));

            fixture.Elements("FixtureAdapter").Remove();
            fixture.Elements("WorkpieceAdapter").Remove();

            // FA_1 se desplaza solo en Z hasta la parte superior. WA_1 recibe el
            // mismo desplazamiento Z que la geometría para conservar la posición
            // del plano original del conjunto después de normalizar el origen.
            fixture.Add(new XElement("FixtureAdapter",
                new XAttribute("nodeName", "FA_1"),
                new XElement("Transformation",
                    new XElement("Translation", new XAttribute("trans", Vector(0, 0, topZ - baseZ))))));
            fixture.Add(new XElement("WorkpieceAdapter",
                new XAttribute("nodeName", "WA_1"),
                new XElement("Transformation",
                    new XElement("Translation", new XAttribute("trans", Vector(0, 0, -baseZ))))));

            gdml.Save(path);
        }

        private static string Vector(double x, double y, double z)
        {
            return string.Join(" ", new[] { Number(x), Number(y), Number(z) });
        }

        private static string Number(double value) =>
            value.ToString("0.###############", CultureInfo.InvariantCulture);

        private static string GetCadName(Esprit.Solid solid)
        {
            string path = string.Empty;
            try { path = solid.CadFile == null ? string.Empty : solid.CadFile.FileName; } catch { }
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name)) name = "CAD";
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.Trim().TrimEnd('.');
        }

        private static string MakeUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int index = 2; ; index++)
            {
                string candidate = Path.Combine(directory, name + "_" + index + extension);
                if (!File.Exists(candidate)) return candidate;
            }
        }

        private sealed class Bounds
        {
            public Bounds(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
            {
                CenterX = (minX + maxX) / 2.0;
                CenterY = (minY + maxY) / 2.0;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public double CenterX { get; }
            public double CenterY { get; }
            public double MinZ { get; }
            public double MaxZ { get; }
        }

        private sealed class SolidBounds
        {
            public SolidBounds(Esprit.Solid solid, Bounds bounds)
            {
                Solid = solid;
                Bounds = bounds;
            }

            public Esprit.Solid Solid { get; }
            public Bounds Bounds { get; }
        }

        private sealed class MirrorResources
        {
            public MirrorResources(string planeName, string selectionName, List<Esprit.Solid> copies)
            {
                PlaneName = planeName;
                SelectionName = selectionName;
                Copies = copies;
            }

            public string PlaneName { get; }
            public string SelectionName { get; }
            public List<Esprit.Solid> Copies { get; }
        }
    }
}
