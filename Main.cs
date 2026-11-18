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
        public string Description =>
            "Prepara la pieza, el bruto y el árbol de utillajes, y crea el montaje completo en ESPRIT.";
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
