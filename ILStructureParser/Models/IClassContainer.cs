using System.Collections.Generic;

namespace ObfuscatorService.Models
{
    public interface IClassContainer
    {
        List<ILClass> Classes { get; }
    }
}
