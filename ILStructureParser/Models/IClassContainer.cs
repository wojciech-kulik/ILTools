using System.Collections.Generic;

namespace ILStructureParser.Models
{
    public interface IClassContainer
    {
        List<ILClass> Classes { get; }
    }
}
