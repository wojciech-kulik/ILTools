using System.Collections.Generic;
using System.IO;

namespace ObfuscatorService.Models
{
    public class Assembly : IClassContainer
    {
        public Assembly(string filePath)
        {
            FilePath = filePath;
            Classes = new List<ILClass>();
        }

        public string FilePath { get; private set; }

        public string FileNameWithoutExt
        {
            get
            {
                return Path.GetFileNameWithoutExtension(FilePath);
            }
        }

        public string FileName
        {
            get
            {
                return Path.GetFileName(FilePath);
            }
        }

        public List<ILClass> Classes { get; internal set; }

        public string ILCode { get; internal set; }
    }
}
