using System.Collections.Generic;

namespace ILStructureParser.Models
{
    public interface IClassContainer
    {
        LinkedList<ILClass> Classes { get; }
    }
}
