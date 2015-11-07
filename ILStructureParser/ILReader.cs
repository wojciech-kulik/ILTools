using ObfuscatorService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ObfuscatorService
{
    public class ILReader
    {
        private const string FieldIdentifier = ".field";
        private const string PropertyIdentifier = ".property";
        private const string MethodIdentifier = ".method";
        private const string ClassIdentifier = ".class";
        private const string ClassEndIdentifierFormat = "// end of class {0}";
        private const string MethodNameEndToken = "(";
        private const string PropertyNameEndToken = "()\r\n";

        public List<Assembly> Assemblies { get; private set; }

        public ILReader()
        {
            Assemblies = new List<Assembly>();
        }

        public void AddAssembly(string filePath)
        {
            Assemblies.Add(new Assembly(filePath) { ILCode = File.ReadAllText(filePath) });
        }

        public void ParseAssemblies()
        {
            foreach (var assembly in Assemblies)
            {
                ParseClasses(assembly, assembly, assembly.ILCode);
            }
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

        private Tuple<int, string> ExtractClassName(string ilCode, int index, int lastIndex)
        {
            var genericCharIndex = ilCode.IndexOf('`', index, lastIndex - index); 
            if (genericCharIndex != -1)
            {
                lastIndex = genericCharIndex;
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
                    var classCode = ilCode.Substring(index, classEndIndex - index);

                    var ilClass = new ILClass()
                    {
                        Name = classNameTuple.Item2,
                        NameStartIndex = classNameTuple.Item1 + offset,
                        StartIndex = index + offset,
                        EndIndex = classEndIndex + offset,
                        ParentAssembly = assembly
                    };
                    classContainer.Classes.Add(ilClass);

                    ParseClasses(assembly, ilClass, classCode.Substring(ClassIdentifier.Length), index + ClassIdentifier.Length);
                    ParseFields(assembly, ilClass, classCode, index);
                    ParseUnits(assembly, ilClass, classCode, index, PropertyIdentifier, PropertyNameEndToken, ilClass.Properties);
                    ParseUnits(assembly, ilClass, classCode, index, MethodIdentifier, MethodNameEndToken, ilClass.Methods);
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
                        ParentAssembly = assembly
                    });
                }

                startIndex = index + FieldIdentifier.Length;
            }
        }

        private void ParseUnits(Assembly assembly, ILClass ilClass, string ilCode, int offset, string startPhrase, string endPhrase, List<ILUnit> destination)
        {
            int index, startIndex = 0;

            while ((index = ilCode.IndexOf(startPhrase, startIndex)) != -1)
            {
                if (!IsInNestedClass(ilClass, index + offset))
                {
                    int propertyNameEndIndex = ilCode.IndexOf(endPhrase, index);
                    string propertyName = ilCode.Substring(index, propertyNameEndIndex - index);

                    int propertyNameStartIndex = propertyName.LastIndexOf(' ') + 1;
                    propertyName = propertyName.Substring(propertyNameStartIndex);

                    destination.Add(new ILUnit()
                    {
                        Name = propertyName,
                        NameStartIndex = offset + index + propertyNameStartIndex,
                        ParentAssembly = assembly
                    });
                }

                startIndex = index + startPhrase.Length;
            }
        }
    }
}
