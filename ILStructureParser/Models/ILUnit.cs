namespace ObfuscatorService.Models
{
    public class ILUnit
    {
        public string Name { get; internal set; }

        public string ShortName
        {
            get
            {
                int dotIndex = Name.LastIndexOf('.');
                return Name.Substring(dotIndex + 1);
            }
        }

        public int NameStartIndex { get; internal set; }

        public Assembly ParentAssembly { get; internal set; }

        public ILClass ParentClass { get; internal set; }
    }
}
