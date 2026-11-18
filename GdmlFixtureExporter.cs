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
            try
            {
                if (combine && sourceSolids.Count > 1)
                {
                    var bounds = sourceSolids.Select(GetBounds).ToList();
                    double minimumZ = bounds.Min(item => item.MinZ);
                    double maximumZ = bounds.Max(item => item.MaxZ);
                    string cadName = GetCadName(sourceSolids[0]);
                    string fileName = string.Format(CultureInfo.InvariantCulture,
                        "Utillaje_{0}_Fase_{1}_Combinado{2}.gdml", cadName, phase,
                        mountFromTop ? "_Superior" : string.Empty);
                    string path = PrepareOutputPath(Path.Combine(folder, fileName));

                    _document.Group.Clear();
                    foreach (Esprit.Solid solid in sourceSolids) _document.Group.Add(solid);
                    _document.SaveAsGDML(path, EspritConstants.espFileType.espFileTypeFixtures);

                    NormalizeFixture(path, cadName, minimumZ, maximumZ);
                    paths.Add(path);
                }
                else
                {
                    for (int index = 0; index < sourceSolids.Count; index++)
                    {
                        Esprit.Solid solid = sourceSolids[index];
                        Bounds bounds = GetBounds(solid);
                        string cadName = GetCadName(solid);
                        string fileName = string.Format(CultureInfo.InvariantCulture,
                            "Utillaje_{0}_Fase_{1}_Solido_{2}{3}.gdml",
                            cadName, phase, solid.Number,
                            mountFromTop ? "_Superior" : string.Empty);
                        string path = PrepareOutputPath(Path.Combine(folder, fileName));

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
                _document.Group.Clear();
                foreach (Esprit.Solid solid in sourceSolids) _document.Group.Add(solid);
                _document.Refresh();
            }

            return paths;
        }

        private static Bounds GetBounds(Esprit.Solid solid)
        {
            EspritSolids.ISolidBody body = (EspritSolids.ISolidBody)solid.SolidBody;
            double minZ = double.PositiveInfinity;
            double maxZ = double.NegativeInfinity;
            int vertexCount = 0;

            foreach (EspritSolids.ISolidVertex vertex in body.SolidVertices)
            {
                EspritGeometryBase.IComPoint point = vertex.Geometry;
                double z = point.Z;

                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
                vertexCount++;
            }

            if (vertexCount == 0)
                throw new InvalidDataException("No se pudieron obtener los vértices del sólido.");

            return new Bounds(minZ, maxZ);
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

        private static string PrepareOutputPath(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory))
            {
                foreach (string candidate in Directory.GetFiles(
                    directory, name + "_*" + extension, SearchOption.TopDirectoryOnly))
                {
                    string candidateName = Path.GetFileNameWithoutExtension(candidate);
                    string suffix = candidateName.Length > name.Length + 1
                        ? candidateName.Substring(name.Length + 1)
                        : string.Empty;
                    if (int.TryParse(suffix, out _)) File.Delete(candidate);
                }
            }
            return path;
        }

        private sealed class Bounds
        {
            public Bounds(double minZ, double maxZ)
            {
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public double MinZ { get; }
            public double MaxZ { get; }
        }

    }
}
