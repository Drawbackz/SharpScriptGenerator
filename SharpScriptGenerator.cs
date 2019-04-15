using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;

namespace SuperSharpScriptHeaderGenerator
{
    public class SharpScriptGenerator
    {
        private readonly string _scriptDirectory;
        private readonly string _projectDirectory;
        private string _projectScriptFile;
        private string _projectConfigFile;

        private readonly string[] _ignoredFiles;
        private readonly string[] _ignoredDirectories;
        private readonly string[] _requiredReferences;

        public event Action<string> MessageOut;

        public SharpScriptGenerator(string projectDirectory)
        {
            MessageOut?.Invoke("Initializing...");
            _projectDirectory = projectDirectory;
            MessageOut?.Invoke($"Project Directory: {projectDirectory}");
            _scriptDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SharpScript");
            MessageOut?.Invoke($"Output Directory: {_scriptDirectory}");
            _projectScriptFile = Path.Combine(_scriptDirectory, "SharpScript.ps1");
            MessageOut?.Invoke($"Script File: {_scriptDirectory}");
            _projectConfigFile = Path.Combine(_scriptDirectory, "SharpScript.config");
            MessageOut?.Invoke($"Configuration File: {_scriptDirectory}");
            if (!Directory.Exists(_scriptDirectory))
            {
                MessageOut?.Invoke("Creating Script Directory...");
                Directory.CreateDirectory(_scriptDirectory);
            }

            _ignoredFiles = new[]
            {
                Path.Combine(_projectDirectory, "Program.cs")
            };
            _ignoredDirectories = new[]
            {
                Path.Combine(_projectDirectory, "obj"),
                Path.Combine(_projectDirectory, "bin"),
                Path.Combine(_projectDirectory, "Properties")
            };
            _requiredReferences = new[]
            {
                "System.Core",
            };

            MessageOut?.Invoke($"Ignored Files: {_scriptDirectory}");
            MessageOut?.Invoke($"Ignored Directories: {_scriptDirectory}");
            MessageOut?.Invoke($"Required References: {_scriptDirectory}");
        }

        public void CreateConfigurationFile()
        {
            File.WriteAllLines(_projectConfigFile, GenerateConfiguration());
        }

        public void CreatePowershellScript()
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpScriptGenerator.SharpScript.ps1");
            var fileStream = new FileStream(_projectScriptFile, FileMode.OpenOrCreate);
            if (stream == null) return;
            for (var i = 0; i < stream.Length; i++)
            {
                fileStream.WriteByte((byte) stream.ReadByte());
            }

            fileStream.Close();
        }

        public void CopyProjectFiles()
        {
            if (Directory.Exists(_scriptDirectory))
            {
                MessageOut?.Invoke("Cleaning Script Directory...");
                Directory.Delete(_scriptDirectory, true);
            }
            MessageOut?.Invoke("Copying Project Files...");
            CopyDirectory(new DirectoryInfo(_projectDirectory), new DirectoryInfo(_scriptDirectory));
        }

        private void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            foreach (FileInfo fi in source.GetFiles("*.dll"))
            {
                MessageOut?.Invoke($@"Copying {target.FullName}\{fi.Name}");
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                if (!_ignoredDirectories.Contains(diSourceSubDir.FullName))
                {
                    CopyDirectory(diSourceSubDir, nextTargetSubDir);
                }
            }
            foreach (FileInfo fi in source.GetFiles("*.cs"))
            {
                MessageOut?.Invoke($@"Copying {target.FullName}\{fi.Name}");
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                if (!_ignoredDirectories.Contains(diSourceSubDir.FullName))
                {
                    CopyDirectory(diSourceSubDir, nextTargetSubDir);
                }
            }
        }
        
        private string[] GenerateConfiguration()
        {
            var modeHeader = new string[] {"//  <mode>winexe</mode>"};
            var fileHeader = GenerateFiles(_projectDirectory);
            var referenceHeader = GenerateReferences(_projectDirectory);
            var compiledHeader = new List<string>();
            compiledHeader.Add("//<csscript>");
            compiledHeader.AddRange(modeHeader);
            compiledHeader.AddRange(fileHeader);
            compiledHeader.AddRange(referenceHeader);
            compiledHeader.Add("//</csscript>");
            return compiledHeader.ToArray();
        }

        private string[] GenerateFiles(string projectRoot)
        {
            var list = new List<string>();
            foreach (var projectFile in GetProjectFiles(projectRoot, "cs"))
            {
                MessageOut?.Invoke($"Added Project File: {projectFile}");
                list.Add($@"//     <file>{projectFile.Replace($"{_projectDirectory}\\", "")}</file>");
            }

            return WrapHeaderContents("files", list.ToArray());
        }

        private string[] GenerateReferences(string projectRoot)
        {
            MessageOut?.Invoke("Searching References...");

            var referenceList = new List<string>();

            foreach (var requiredReference in _requiredReferences)
            {
                MessageOut?.Invoke($"Added Required Reference: {requiredReference}");
                referenceList.Add(requiredReference);
            }

            foreach (var projectFile in GetProjectFiles(projectRoot, "cs"))
            {
                MessageOut?.Invoke($"Parsing File References: {projectFile}");
                foreach (var reference in ReadFileReferences(projectFile))
                {
                    if (!referenceList.Contains(reference))
                    {
                        MessageOut?.Invoke($"Found New Reference: {reference}");
                        referenceList.Add(reference);
                    }
                }
                referenceList.Sort();
            }

            foreach (var reference in referenceList.ToArray())
            {
                MessageOut?.Invoke($"Verifying Reference: {reference}");
                if (Assembly.LoadWithPartialName($"{reference}") == null)
                {
                    MessageOut?.Invoke($"Ignoring Reference: {reference}");
                    referenceList.Remove(reference);
                }
                else
                {
                    MessageOut?.Invoke($"Added Reference: {reference}");
                }
            }

            var referenceHeaders = new List<string>();
            foreach (var reference in referenceList)
            {
                referenceHeaders.Add($@"//     <reference>{reference.Replace(_projectDirectory, "")}</reference>");
            }

            foreach (var projectFile in GetProjectFiles(projectRoot, "dll"))
            {
                referenceHeaders.Add($@"//     <reference>{projectFile.Replace($"{_projectDirectory}\\", "")}</reference>");
            }

            return WrapHeaderContents("references", referenceHeaders.ToArray());
        }

        private string[] GetProjectFiles(string projectRoot, string extension)
        {
            if (_ignoredDirectories.Contains(projectRoot))
            {
                return new string[0];
            }

            var files = new List<string>();
            foreach (var file in Directory.GetFiles(projectRoot, $"*.{extension}"))
            {
                if (!_ignoredFiles.Contains(file))
                {
                    files.Add(file);
                }
            }

            foreach (var dir in Directory.GetDirectories(projectRoot))
            {
                files.AddRange(GetProjectFiles(dir, extension));
            }

            return files.ToArray();
        }

        private static string[] ReadFileReferences(string filePath)
        {
            var references = new List<string>();
            var fileLines = File.ReadLines(filePath);
            foreach (var fileLine in fileLines)
            {
                if (!fileLine.Contains("using"))
                {
                    break;
                }

                var reference = fileLine.Replace("using", string.Empty).Trim();
                references.Add(reference.Substring(0, reference.Length - 1));
            }

            return references.ToArray();
        }

        private static string[] WrapHeaderContents(string tagName, string[] headerContents)
        {
            var fileHeader = new List<string>();
            fileHeader.Add($"//  <{tagName}>");
            fileHeader.AddRange(headerContents);
            fileHeader.Add($"//  </{tagName}>");
            return fileHeader.ToArray();
        }
    }
}