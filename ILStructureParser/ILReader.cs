using ObfuscatorService.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ObfuscatorService
{
    public class ILReader
    {
        private class ParallelJob
        {
            public int Offset { get; set; }
            public string ILCode { get; set; }
        }

        public const string ILDirectory = "ILFiles";

        private const string FieldIdentifier = ".field";
        private const string PropertyIdentifier = ".property";
        private const string MethodIdentifier = ".method";
        private const string ClassIdentifier = ".class";
        private const string ClassEndIdentifierFormat = "// end of class {0}";
        private const string MethodNameEndToken = "(";
        private const string PropertyNameEndToken = "()\r\n";

        private readonly string ildasmPath = 
            Microsoft.Build.Utilities.ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("ildasm.exe", Microsoft.Build.Utilities.TargetDotNetFrameworkVersion.VersionLatest);

        public List<Assembly> Assemblies { get; private set; }

        public ILReader()
        {
            Assemblies = new List<Assembly>();
        }

        public static string GetILPath(string assemblyFilePath)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath).Replace(' ', '_');
            return String.Format("{0}\\{1}\\{1}.il", ILDirectory, assemblyName); ;
        }

        public void AddAssembly(string filePath)
        {
            var ilFileName = GetILPath(filePath);
            var dir = Path.GetDirectoryName(ilFileName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = String.Format("\"{0}\" /out:{1}", filePath, ilFileName),
                FileName = ildasmPath
            });

            while (Process.GetProcessesByName("ildasm").Any())
            {
                Task.Delay(100).Wait();
            }

            Assemblies.Add(new Assembly(filePath) { ILCode = File.ReadAllText(ilFileName) });
        }

        public void ParseAssemblies()
        {
            foreach (var assembly in Assemblies)
            {
                var tasks = new List<Task>();
                var results = new List<ILClass>();

                // split work
                var parallelJobs = DivideWorkForParallelProcessing(assembly.ILCode);

                // parallel processing
                foreach (var job in parallelJobs)
                {
                    results.Add(new ILClass());
                    var result = results.Last();
                    tasks.Add(Task.Run(() => ParseClasses(assembly, result, job.ILCode, job.Offset)));
                }
                Task.WaitAll(tasks.ToArray());

                // merge
                foreach (var result in results)
                {
                    assembly.Classes.AddRange(result.Classes);
                }
            }
        }

        public void RefreshAssemblies()
        {
            foreach (var assembly in Assemblies)
            {
                assembly.Classes = new List<ILClass>();
            }
            ParseAssemblies();
        }

        private IList<ParallelJob> DivideWorkForParallelProcessing(string ilCode)
        {
            var result = new List<ParallelJob>();
            int offset = 0;
            int partLength = ilCode.Length / Environment.ProcessorCount;

            for (int i = 0; i < Environment.ProcessorCount - 1 && partLength < ilCode.Length; i++)
            {
                // look for the nearest not nested class to split in this place
                int endIndex = partLength;
                while ((endIndex = ilCode.IndexOf(ClassIdentifier, endIndex)) != -1)
                {
                    if (!IsNestedClass(ilCode, endIndex))
                    {
                        break;
                    }
                    endIndex += ClassIdentifier.Length;
                }

                if (endIndex == -1)
                {
                    break;
                }

                result.Add(new ParallelJob() { ILCode = ilCode.Substring(0, endIndex), Offset = offset });
                ilCode = ilCode.Substring(endIndex);
                offset += endIndex;
            }
            result.Add(new ParallelJob() { ILCode = ilCode, Offset = offset });

            return result;
        }

        private int LastIndexOf(string source, char value, int startIndex, int endIndex)
        {
            for (int i = endIndex; i >= startIndex; i--)
            {
                if (source[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsInNestedClass(IClassContainer classContainer, int index)
        {
            return classContainer.Classes.Any(x => x.NameStartIndex < index && x.EndIndex > index);
        }

        private bool IsNestedClass(string ilCode, int startIndex)
        {
            int newLineIndex = ilCode.IndexOf(Environment.NewLine, startIndex);
            return ilCode.IndexOf("nested", startIndex, newLineIndex - startIndex) != -1;
        }

        private Tuple<int, string> ExtractClassName(string ilCode, int index, int lastIndex)
        {
            var genericCharIndex = ilCode.IndexOf('`', index, lastIndex - index); 
            if (genericCharIndex != -1)
            {
                lastIndex = genericCharIndex + 2;
                if (ilCode[lastIndex] == '\'')
                {
                    ++lastIndex;
                }
            }
            var nameIndex = LastIndexOf(ilCode, ' ', index, lastIndex);

            if (nameIndex != -1)
            {
                return new Tuple<int, string>(nameIndex + 1, ilCode.Substring(nameIndex, lastIndex - nameIndex).Trim());
            }

            return new Tuple<int, string>(-1, String.Empty);
        }

        private void ParseClasses(Assembly assembly, IClassContainer classContainer, string ilCode, int offset = 0)
        {
            int index, startIndex = 0;

            while((index = ilCode.IndexOf(ClassIdentifier, startIndex)) != -1)
            {
                if (!IsInNestedClass(classContainer, index + offset))
                {
                    int newLineIndex = ilCode.IndexOf(Environment.NewLine, index);
                    var classNameTuple = ExtractClassName(ilCode, index, newLineIndex);

                    int classEndIndex = ilCode.IndexOf(String.Format(ClassEndIdentifierFormat, classNameTuple.Item2), index);
                    if (classEndIndex == -1)
                    {
                        startIndex = index + ClassIdentifier.Length;
                        continue;
                    }

                    var ilClass = new ILClass()
                    {
                        Name = classNameTuple.Item2,
                        NameStartIndex = classNameTuple.Item1 + offset,
                        StartIndex = index + offset,
                        EndIndex = classEndIndex + offset,
                        ParentAssembly = assembly,
                        ParentClass = classContainer as ILClass
                    };
                    classContainer.Classes.Add(ilClass);

                    var classCode = ilCode.Substring(index, classEndIndex - index);
                    ParseClasses(assembly, ilClass, classCode.Substring(ClassIdentifier.Length), offset + index + ClassIdentifier.Length);
                    ParseFields(assembly, ilClass, classCode, offset + index);
                    ParseUnits(assembly, ilClass, classCode, offset + index, PropertyIdentifier, PropertyNameEndToken, ilClass.Properties);
                    ParseUnits(assembly, ilClass, classCode, offset + index, MethodIdentifier, MethodNameEndToken, ilClass.Methods);
                }

                startIndex = index + ClassIdentifier.Length;
            }
        }

        private void ParseFields(Assembly assembly, ILClass ilClass, string ilCode, int offset)
        {
            int index, startIndex = 0;

            while ((index = ilCode.IndexOf(FieldIdentifier, startIndex)) != -1)
            {
                if (!IsInNestedClass(ilClass, index + offset))
                {
                    int fieldNameIndex;
                    string fieldName;

                    int newLineIndex = ilCode.IndexOf(Environment.NewLine, index);
                    var line = ilCode.Substring(index, newLineIndex - index);

                    int initializationIndex = line.IndexOf('=');
                    if (initializationIndex != -1)
                    {
                        fieldNameIndex = LastIndexOf(line, ' ', 0, initializationIndex - 2) + 1;
                        fieldName = line.Substring(fieldNameIndex, initializationIndex - fieldNameIndex - 1).Trim();
                    }
                    else
                    {
                        fieldNameIndex = LastIndexOf(line, ' ', 0, line.Length - 1) + 1;
                        fieldName = line.Substring(fieldNameIndex).Trim();
                    }

                    ilClass.Fields.Add(new ILUnit()
                    {
                        Name = fieldName,
                        NameStartIndex = offset + index + fieldNameIndex,
                        ParentAssembly = assembly,
                        ParentClass = ilClass
                    });
                }

                startIndex = index + FieldIdentifier.Length;
            }
        }

        private int FindStartOfGenericName(string ilCode, int startIndex, int endIndex)
        {
            bool start = false;
            int counter = 0, i = endIndex;

            while (i > startIndex && (counter > 0 || !start))
            {
                if (ilCode[i] == '>')
                {
                    start = true;
                    counter++;
                }
                else if (ilCode[i] == '<')
                {
                    counter--;
                }

                --i;
            }

            while (i > startIndex && ilCode[i] != ' ')
            {
                --i;
            }

            return i + 1;
        }

        private int FindNameEndIndex(string ilCode, string endPhrase, int startIndex)
        {
            int nameEndIndex = ilCode.IndexOf(endPhrase, startIndex);
            if (nameEndIndex == -1)
            {
                return -1;
            }

            while ((ilCode[nameEndIndex - 1] == ' ' || ilCode[nameEndIndex - 1] == '<') && nameEndIndex != -1)
            {
                nameEndIndex = ilCode.IndexOf(endPhrase, nameEndIndex + 1);
            }

            return nameEndIndex;
        }

        private int FindNameStartIndex(string ilCode, int startIndex, int endIndex)
        {
            if (ilCode[endIndex - 1] == '>')
            {
                return FindStartOfGenericName(ilCode, startIndex, endIndex);
            }
            else
            {
                return LastIndexOf(ilCode, ' ', startIndex, endIndex) + 1;
            }
        }

        private void ParseUnits(Assembly assembly, ILClass ilClass, string ilCode, int offset, string startPhrase, string endPhrase, List<ILUnit> destination)
        {
            int index, startIndex = 0;

            while ((index = ilCode.IndexOf(startPhrase, startIndex)) != -1)
            {
                if (!IsInNestedClass(ilClass, index + offset))
                {
                    int nameEndIndex = FindNameEndIndex(ilCode, endPhrase, index);
                    if (nameEndIndex == -1)
                    {
                        startIndex = index + startPhrase.Length;
                        continue;
                    }

                    int nameStartIndex = FindNameStartIndex(ilCode, index, nameEndIndex);
                    string name = ilCode.Substring(nameStartIndex, nameEndIndex - nameStartIndex);

                    destination.Add(new ILUnit()
                    {
                        Name = name,
                        NameStartIndex = offset + nameStartIndex,
                        ParentAssembly = assembly,
                        ParentClass = ilClass
                    });
                }

                startIndex = index + startPhrase.Length;
            }
        }
    }
}
