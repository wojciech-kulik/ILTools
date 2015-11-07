using System.Collections.Generic;

namespace ObfuscatorService.Models
{
    public class ILClass : ILUnit, IClassContainer
    {
        public ILClass()
        {
            Classes = new List<ILClass>();
            Methods = new List<ILUnit>();
            Fields = new List<ILUnit>();
            Properties = new List<ILUnit>();
        }

        public int StartIndex { get; internal set; }

        public int EndIndex { get; internal set; }

        public List<ILClass> Classes { get; internal set; }

        public List<ILUnit> Methods { get; internal set; }

        public List<ILUnit> Fields { get; internal set; }

        public List<ILUnit> Properties { get; internal set; }
    }
}
