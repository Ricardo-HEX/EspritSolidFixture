namespace EspritSolidFixture
{
    internal enum MountingElementType
    {
        Accessory,
        Jaws
    }

    internal sealed class MountingElementSpec
    {
        public MountingElementSpec(MountingElementType type, string fileName,
            double jawOpening = 0.0)
        {
            Type = type;
            FileName = fileName;
            JawOpening = jawOpening;
        }

        public MountingElementType Type { get; }
        public string FileName { get; }
        public double JawOpening { get; }
    }
}
