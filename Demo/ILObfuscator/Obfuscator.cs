﻿using ILStructureParser.Models;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System;
using ILStructureParser;
using System.Threading.Tasks;

namespace ILObfuscator
{
    public class Obfuscator
    {
        public const string ObfuscatedDirectory = "Obfuscated";

        private StringBuilder _currentName = new StringBuilder("a");
        private readonly string ilasmPath =
            Microsoft.Build.Utilities.ToolLocationHelper.GetPathToDotNetFrameworkFile("ilasm.exe", Microsoft.Build.Utilities.TargetDotNetFrameworkVersion.VersionLatest);

        public void Obfuscate(IList<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                foreach (var ilClass in assembly.Classes.OrderByDescending(x => x.Name.Length))
                {
                    ObfuscateName(assemblies, ilClass.Name, GenerateNewName(ilClass.Name));
                }
            }

            CompileAssemblies(assemblies);
        }

        private void ObfuscateName(IList<Assembly> assemblies, string oldName, string newName)
        {
            foreach (var assembly in assemblies)
            {
                assembly.ILCode = assembly.ILCode.Replace(oldName, newName);
            }
        }

        private string GenerateNewName(string oldName)
        {
            if (_currentName[_currentName.Length - 1] == 'z')
            {
                _currentName.Append('a');
            }
            else
            {
                _currentName[_currentName.Length - 1] = (char)(_currentName[_currentName.Length - 1] + 1);
            }

            string suffix = string.Empty;
            if (oldName.IndexOf('`') != -1)
            {
                suffix = oldName.Substring(oldName.IndexOf('`'));
                if (suffix[suffix.Length - 1] == '\'')
                {
                    suffix = suffix.Substring(0, suffix.Length - 1);
                }
            }

            return _currentName.ToString() + suffix;
        }

        private void RecreateDirectory()
        {
            if (Directory.Exists(ObfuscatedDirectory))
            {
                Directory.Delete(ObfuscatedDirectory, true);
            }
            Directory.CreateDirectory(ObfuscatedDirectory);
        }

        private void CompileAssemblies(IList<Assembly> assemblies)
        {
            RecreateDirectory();

            foreach (var assembly in assemblies)
            {
                string resArguments = string.Empty;
                string ilPath = ILReader.GetILPath(assembly.FilePath);
                string dir = Path.GetDirectoryName(ilPath);
                string resName = String.Format("{0}\\{1}.res", dir, assembly.FileNameWithoutExt);

                // verify if res file is available
                if (File.Exists(resName))
                {
                    resArguments = String.Format("{0} /res:\"{1}\"", resArguments, resName);
                }

                // save obfuscated IL code
                File.Delete(ilPath);
                File.WriteAllText(ilPath, assembly.ILCode);

                // prepare arguments
                string arguments = String.Format("{0}\"{1}\" {2}",
                                    assembly.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "/DLL " : String.Empty,
                                    ilPath,
                                    resArguments);

                // run compilation
                Process.Start(new ProcessStartInfo()
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Arguments = arguments,
                    FileName = ilasmPath,
                });
            }

            // wait until compilation is finished
            while (Process.GetProcessesByName("ilasm").Any())
            {
                Task.Delay(100).Wait();
            }

            // move assemblies to another directory
            MoveAssembliesToDirectory(assemblies, ObfuscatedDirectory);
        }

        private void MoveAssembliesToDirectory(IEnumerable<Assembly> assemblies, string directory)
        {
            foreach (var assembly in assemblies)
            {
                string path = Path.GetDirectoryName(ILReader.GetILPath(assembly.FilePath));

                foreach (var file in Directory.GetFiles(path).Where(x => x.EndsWith(".dll") || x.EndsWith(".exe")))
                {
                    var savePath = String.Format("{0}\\{1}", directory, Path.GetFileName(file));
                    File.Delete(savePath);
                    File.Move(file, savePath);
                }
            }
        }
    }
}
