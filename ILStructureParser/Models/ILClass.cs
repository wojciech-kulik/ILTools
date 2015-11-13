using System.Collections.Generic;

namespace ILStructureParser.Models
{
    public class ILClass : ILUnit, IClassContainer
    {
        public ILClass()
        {
            Classes = new LinkedList<ILClass>();
            Methods = new LinkedList<ILUnit>();
            Fields = new LinkedList<ILUnit>();
            Properties = new LinkedList<ILUnit>();
            Events = new LinkedList<ILUnit>();
        }

        public int StartIndex { get; internal set; }

        public int EndIndex { get; internal set; }

        public LinkedList<ILClass> Classes { get; internal set; }

        public LinkedList<ILUnit> Methods { get; internal set; }

        public LinkedList<ILUnit> Fields { get; internal set; }

        public LinkedList<ILUnit> Properties { get; internal set; }

        public LinkedList<ILUnit> Events { get; internal set; }

        internal int BracketsCounter;
    }
}
