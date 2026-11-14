using System.ComponentModel.Composition;
using ESPRIT.NetApi.Extensions;

namespace EspritSolidFixture
{
    [Export(typeof(IExtension))]
    [ExportMetadata("SupportBuild", 20)]
    public sealed class Main : IExtension
    {
        private ExtensionController _controller;

        public string Name => "Utillaje CAD";
        public string Description => "Crea un accesorio de ESPRIT a partir de un sólido CAD de utillaje.";
        public string Publisher => "Hexagon Custom Solutions";
        public string Url => string.Empty;

        public void Connect(object app)
        {
            _controller = new ExtensionController((Esprit.Application)app);
        }

        public void Disconnect()
        {
            _controller?.Dispose();
            _controller = null;
        }
    }
}
