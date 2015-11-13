using ILStructureParser.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ILStructureParser
{
    public class ILReader
    {
        private class ParallelJob
        {
            public int Offset { get; set; }
            public string ILCode { get; set; }
        }

        public const string ILDirectory = "ILFiles";
        private const string ClassIdentifier = ".class";
        private const string MethodNameEndToken = "(";
        private const string PropertyNameEndToken = "(";
        private const string FieldInitializerToken = " = ";
        private const string FieldInitializerToken2 = " at ";
        private const string PInvokeToken = "pinvokeimpl";

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
                Task.Delay(200).Wait();
            }
            Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = String.Format("\"{0}\" /out:{1}", filePath, ilFileName),
                FileName = ildasmPath
            }).WaitForExit();

            Assemblies.Add(new Assembly(filePath) { ILCode = File.ReadAllText(ilFileName) });
        }

        public void AddILCode(string assemblyName, string ilCode)
        {
            Assemblies.Add(new Assembly(assemblyName) { ILCode = ilCode });
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
                    foreach (var c in result.Classes)
                    {
                        assembly.Classes.AddLast(c);
                    }
                }
            }
        }

        public void RefreshAssemblies()
        {
            foreach (var assembly in Assemblies)
            {
                assembly.Classes = new LinkedList<ILClass>();
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
                // look for the nearest (to division point) not nested class to split in this place
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

        private bool IsNestedClass(string ilCode, int startIndex)
        {
            int newLineIndex = ilCode.IndexOf(Environment.NewLine, startIndex);
            return ilCode.IndexOf("nested", startIndex, newLineIndex - startIndex) != -1;
        }

        private void ParseClasses(Assembly assembly, IClassContainer classContainer, string ilCode, int offset = 0)
        {
            bool newLine = false;
            var length = ilCode.Length;
            var fms = new ILFMS();
            ILClass currentClass = null;

            for (int i = 0; i < length; i++)
            {
                fms.GoToNextState(ilCode[i]);
                if (fms.CurrentState.IsFinal)
                {
                    var ilUnit = new ILUnit() { ParentAssembly = assembly, ParentClass = currentClass };

                    if (fms.CurrentState.StateId == ILFMS.StateIdentifier.Class)
                    {
                        int newLineIndex = ilCode.IndexOf(Environment.NewLine, i);
                        var name = ExtractClassName(ilCode, i, newLineIndex);
                        currentClass = new ILClass()
                        {
                            StartIndex = offset + i - 5,
                            NameStartIndex = offset + name.Item1,
                            Name = name.Item2,
                            ParentAssembly = assembly,
                            ParentClass = currentClass
                        };

                        // append class to the parent
                        if (currentClass.ParentClass != null)
                        {
                            currentClass.ParentClass.Classes.AddLast(currentClass);
                        }
                        else
                        {
                            classContainer.Classes.AddLast(currentClass);
                        }
                    }
                    else if (fms.CurrentState.StateId == ILFMS.StateIdentifier.Method)
                    {
                        SetPropertyMethodName(ilUnit, ilCode, i, offset, MethodNameEndToken);
                        currentClass.Methods.AddLast(ilUnit);
                    }
                    else if (fms.CurrentState.StateId == ILFMS.StateIdentifier.Property)
                    {
                        SetPropertyMethodName(ilUnit, ilCode, i, offset, PropertyNameEndToken);
                        currentClass.Properties.AddLast(ilUnit);
                    }
                    else if (fms.CurrentState.StateId == ILFMS.StateIdentifier.Field)
                    {
                        SetFieldName(ilUnit, ilCode, i, offset);
                        currentClass.Fields.AddLast(ilUnit);
                    }
                    else if (fms.CurrentState.StateId == ILFMS.StateIdentifier.Event)
                    {
                        SetEventName(ilUnit, ilCode, i, offset);
                        currentClass.Events.AddLast(ilUnit);
                    }

                    fms.Restart();
                }
                else if (ilCode[i] == '\n')
                {
                    newLine = true;
                }
                else if (currentClass != null && ilCode[i] == '{')
                {
                    currentClass.BracketsCounter++;
                    newLine = false;
                }
                else if (currentClass != null && ilCode[i] == '}')
                {
                    if (--currentClass.BracketsCounter == 0 && newLine) //closing bracket should be in another line then the opening one
                    {
                        currentClass.EndIndex = offset + i + 1;
                        currentClass = currentClass.ParentClass;
                    }
                }
            }
        }

        private void SetEventName(ILUnit ilUnit, string ilCode, int startIndex, int offset)
        {
            int newLineIndex = ilCode.IndexOf(Environment.NewLine, startIndex);
            var nameStartIndex = LastIndexOf(ilCode, ' ', startIndex, newLineIndex);

            ilUnit.NameStartIndex = offset + nameStartIndex;
            ilUnit.Name = ilCode.Substring(nameStartIndex, newLineIndex - nameStartIndex);
        }

        private void SetFieldName(ILUnit ilUnit, string ilCode, int startIndex, int offset)
        {
            int newLineIndex = ilCode.IndexOf(Environment.NewLine, startIndex);
            var line = ilCode.Substring(startIndex, newLineIndex - startIndex);
            var name = ExtractFieldName(line);

            ilUnit.NameStartIndex = offset + startIndex + name.Item1;
            ilUnit.Name = name.Item2;
        }

        private void SetPropertyMethodName(ILUnit ilUnit, string ilCode, int startIndex, int offset, string endPhrase)
        {
            var name = GetMethodPropertyName(ilCode, endPhrase, startIndex);
            ilUnit.NameStartIndex = offset + name.Item1;
            ilUnit.Name = name.Item2;
        }

        #region Names Extracting

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
            return null;
        }

        private Tuple<int, string> GetMethodPropertyName(string ilCode, string endPhrase, int index)
        {
            int nameEndIndex = FindNameEndIndex(ilCode, endPhrase, index);
            if (nameEndIndex == -1)
            {
                return null;
            }

            int nameStartIndex = FindNameStartIndex(ilCode, index, nameEndIndex);
            return new Tuple<int, string>(nameStartIndex, ilCode.Substring(nameStartIndex, nameEndIndex - nameStartIndex));
        }

        private Tuple<int, string> ExtractFieldName(string line)
        {
            int fieldNameIndex;
            string fieldName;

            int initializationIndex = line.IndexOf(FieldInitializerToken);
            initializationIndex = initializationIndex == -1 ? line.IndexOf(FieldInitializerToken2) : initializationIndex;

            if (initializationIndex != -1)
            {
                fieldNameIndex = LastIndexOf(line, ' ', 0, initializationIndex - 1) + 1;
                fieldName = line.Substring(fieldNameIndex, initializationIndex - fieldNameIndex).Trim();
            }
            else
            {
                fieldNameIndex = LastIndexOf(line, ' ', 0, line.Length - 1) + 1;
                fieldName = line.Substring(fieldNameIndex).Trim();
            }

            return new Tuple<int, string>(fieldNameIndex, fieldName);
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

            //TODO: do not obfuscate pinvoke
            // handle pinvokes
            if (nameEndIndex >= PInvokeToken.Length && ilCode.Substring(nameEndIndex - PInvokeToken.Length, PInvokeToken.Length) == PInvokeToken)
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

        #endregion
    }
}
