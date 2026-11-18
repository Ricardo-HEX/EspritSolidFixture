namespace EspritSolidFixture
{
    internal enum MountingElementType
    {
        Accessory,
        Jaws
    }

    internal sealed class MountingElementSpec
    {
        public MountingElementSpec(MountingElementType type, string fileName)
        {
            Type = type;
            FileName = fileName;
        }

        public MountingElementType Type { get; }
        public string FileName { get; }
    }
}
