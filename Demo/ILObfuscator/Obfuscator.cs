using ObfuscatorService.Models;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System;
using ObfuscatorService;
using System.Threading.Tasks;

namespace ILObfuscator
{
    public class Obfuscator
    {
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

        private void CompileAssemblies(IList<Assembly> assemblies)
        {
            if (Directory.Exists("Obfuscated"))
            {
                Directory.Delete("Obfuscated", true);
            }
            Directory.CreateDirectory("Obfuscated");

            foreach (var assembly in assemblies)
            {
                string resources = string.Empty;
                string ilPath = ILReader.GetILPath(assembly.FilePath);
                string res1Name = assembly.FileNameWithoutExt + ".resource";
                string res2Name = assembly.FileNameWithoutExt + ".res";

                File.Delete(ilPath);
                File.WriteAllText(ilPath, assembly.ILCode);

                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(ilPath)).Where(x => x.EndsWith(res1Name) || x.EndsWith(res2Name)))
                {
                    resources = String.Format("{0} /res:\"{1}\"", resources, file);
                }

                string arguments = String.Format("{0}\"{1}\" {2}",
                                    assembly.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "/DLL " : String.Empty,
                                    ilPath,
                                    resources);

                Process.Start(new ProcessStartInfo()
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Arguments = arguments,
                    FileName = ilasmPath,
                });
            }

            while (Process.GetProcessesByName("ilasm").Any())
            {
                Task.Delay(100).Wait();
            }

            foreach (var assembly in assemblies)
            {
                string path = Path.GetDirectoryName(ILReader.GetILPath(assembly.FilePath));

                foreach (var file in Directory.GetFiles(path).Where(x => x.EndsWith(".dll") || x.EndsWith(".exe")))
                {
                    File.Delete("Obfuscated\\" + Path.GetFileName(file));
                    File.Move(file, "Obfuscated\\" + Path.GetFileName(file));
                }
            }
        }
    }
}
