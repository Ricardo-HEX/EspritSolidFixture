using System.Collections.Generic;

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
            string parentAccessoryFile = null)
        {
            Type = type;
            FileName = fileName;
            ParentAccessoryFile = parentAccessoryFile;
            JawAdapterIndices = new List<int> { 1, 2 };
        }

        public MountingElementType Type { get; }
        public string FileName { get; }
        public string ParentAccessoryFile { get; }
        public List<int> JawAdapterIndices { get; }
        public bool ReverseJaws { get; set; }
    }
}
