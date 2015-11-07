namespace ObfuscatorService.Models
{
    public class ILUnit
    {
        public string Name { get; internal set; }

        public int NameStartIndex { get; internal set; }

        public Assembly ParentAssembly { get; internal set; }
    }
}
