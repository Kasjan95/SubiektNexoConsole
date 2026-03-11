using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SubiektNexoConsole.Bootstrap
{
    public sealed class NexoLibraryUpdater
    {
        private static readonly string[] DeploymentFiles =
        {
            "Xml.pak"
        };

        private static readonly string[] ProgramFilesFilesToCopy =
        {
            "ijwhost.dll",
            "Microsoft.Data.SqlClient.SNI.dll"
        };

        private readonly string _podmiot;
        private readonly string _targetDirectory;
        private readonly string _deploymentsBinariesPath;
        private readonly string _programFilesNexoPath;

        public NexoLibraryUpdater(
            string podmiot,
            string? targetDirectory = null,
            string? programFilesNexoPath = null)
        {
            if (string.IsNullOrWhiteSpace(podmiot))
                throw new ArgumentException("Nazwa podmiotu nie może być pusta.", nameof(podmiot));

            _podmiot = podmiot;
            _targetDirectory = targetDirectory ?? AppContext.BaseDirectory;

            _programFilesNexoPath = programFilesNexoPath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "InsERT",
                    "nexo");
        }

        public NexoLibraryUpdateResult Update()
        {
            var deploymentsPath = ResolveDeploymentsBinariesPath();

            var result = new NexoLibraryUpdateResult
            {
                Podmiot = _podmiot,
                SourceDeploymentsPath = deploymentsPath,
                SourceProgramFilesPath = _programFilesNexoPath,
                TargetPath = _targetDirectory
            };

            Directory.CreateDirectory(_targetDirectory);

            CopyFromDeployments(deploymentsPath, result);
            CopyFromProgramFiles(result);

            return result;
        }

        private void CopyFromDeployments(string deploymentsPath, NexoLibraryUpdateResult result)
        {
            if (!Directory.Exists(deploymentsPath))
                throw new DirectoryNotFoundException(
                    $"Nie znaleziono katalogu binaries: {deploymentsPath}");

            foreach (var fileName in DeploymentFiles)
            {
                var sourceFile = Path.Combine(deploymentsPath, fileName);

                if (!File.Exists(sourceFile))
                {
                    result.Warnings.Add($"Brak pliku {fileName} w {deploymentsPath}");
                    continue;
                }

                CopyIfNeeded(sourceFile, result);
            }
        }

        private void CopyFromProgramFiles(NexoLibraryUpdateResult result)
        {
            if (!Directory.Exists(_programFilesNexoPath))
                throw new DirectoryNotFoundException(
                    $"Nie znaleziono katalogu instalacyjnego Nexo: {_programFilesNexoPath}");

            foreach (var fileName in ProgramFilesFilesToCopy)
            {
                var sourceFile = Path.Combine(_programFilesNexoPath, fileName);

                if (!File.Exists(sourceFile))
                {
                    result.Warnings.Add($"Brak pliku {fileName} w {_programFilesNexoPath}");
                    continue;
                }

                CopyIfNeeded(sourceFile, result);
            }
        }

        private void CopyIfNeeded(string sourceFile, NexoLibraryUpdateResult result)
        {
            var fileName = Path.GetFileName(sourceFile);
            var destinationFile = Path.Combine(_targetDirectory, fileName);

            try
            {
                if (!ShouldCopy(sourceFile, destinationFile))
                {
                    result.SkippedFiles.Add(fileName);
                    return;
                }

                File.Copy(sourceFile, destinationFile, overwrite: true);
                result.CopiedFiles.Add(fileName);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{fileName}: {ex.Message}");
            }
        }

        private static bool ShouldCopy(string sourceFile, string destinationFile)
        {
            if (!File.Exists(destinationFile))
                return true;

            var src = new FileInfo(sourceFile);
            var dst = new FileInfo(destinationFile);

            return src.Length != dst.Length || src.LastWriteTimeUtc != dst.LastWriteTimeUtc;
        }
        private string ResolveDeploymentsBinariesPath()
        {
            var deploymentsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InsERT",
                "Deployments",
                "Nexo");

            if (!Directory.Exists(deploymentsRoot))
                throw new DirectoryNotFoundException(
                    $"Nie znaleziono katalogu Deployments: {deploymentsRoot}");

            var podmiotDir = Directory
                .GetDirectories(deploymentsRoot)
                .Select(d => new DirectoryInfo(d))
                .Where(d => d.Name.StartsWith(_podmiot + "_", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .FirstOrDefault();

            if (podmiotDir == null)
                throw new Exception(
                    $"Nie znaleziono katalogu podmiotu rozpoczynającego się od '{_podmiot}' w {deploymentsRoot}");

            var binaries = Path.Combine(podmiotDir.FullName, "Binaries");

            if (!Directory.Exists(binaries))
                throw new DirectoryNotFoundException(
                    $"Nie znaleziono katalogu Binaries w {podmiotDir}");

            return binaries;
        }
    }

    public sealed class NexoLibraryUpdateResult
    {
        public string Podmiot { get; set; } = string.Empty;
        public string SourceDeploymentsPath { get; set; } = string.Empty;
        public string SourceProgramFilesPath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;

        public List<string> CopiedFiles { get; } = new();
        public List<string> SkippedFiles { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public bool HasErrors => Errors.Count > 0;
    }
}