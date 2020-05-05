using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PMDDotNETCompilerTestService
{
    public class PMDCompileTestService
    {
        public class TestResult
        {
            public string MMLFilePath { get; }
            public CompareResult CompareResult { get; }
            public CompileStatus DotNetResult { get; }
            public CompileStatus DosResult { get; }
            public int DosExitCode { get; }
            public string? CompiledFilesDir { get; }

            public TestResult(string mmlFilePath, CompareResult compareResult, CompileStatus dotnetResult, int dosExitCode, CompileStatus dosResult, string? compiledFilesdir)
            {
                MMLFilePath = mmlFilePath;
                CompareResult = compareResult;
                DotNetResult = dotnetResult;
                DosResult = dosResult;
                DosExitCode = dosExitCode;
                CompiledFilesDir = compiledFilesdir;
            }

            public bool IsPerfect =>
                CompareResult == CompareResult.Match &&
                DotNetResult == CompileStatus.Succeeded &&
                DosResult == CompileStatus.Succeeded;

            public bool IsAllowed =>
                (CompareResult == CompareResult.Match || CompareResult == CompareResult.Match_NotEqualLength || CompareResult == CompareResult.Match_WithoutMemo) &&
                DotNetResult == CompileStatus.Succeeded &&
                DosResult == CompileStatus.Succeeded;

            public bool IsWarning =>
                (CompareResult == CompareResult.Match || CompareResult == CompareResult.Match_NotEqualLength || CompareResult == CompareResult.Match_WithoutMemo) &&
                (DotNetResult == CompileStatus.Succeeded || DotNetResult == CompileStatus.Warning &&
                (DosResult == CompileStatus.Succeeded || DosResult == CompileStatus.Warning));
        }

        private readonly ILogger _logger;

        public PMDCompileTestService(ILogger<PMDCompileTestService> logger)
        {
            _logger = logger;
        }

        public TestResult SingleTest(string mmlFilePath, string tooldir, string? logdir)
        {
            _logger.LogInformation("---- Test Start - {0}", mmlFilePath);
            try
            {
                var dotnet = DotnetCompiler.Compile(mmlFilePath);
                _logger.LogInformation(".NET Compile Result: {0}", dotnet.result.Status);
                dotnet.result.WriteLog(_logger);

                var dos = DosCompiler.Compile(mmlFilePath, dotnet.outputFileName, tooldir);
                _logger.LogInformation("DOS Compile Result: {0}", dos.Status);
                dos.WriteLog(_logger);

                var compareResult = dotnet.result.Compare(dos);
                _logger.LogInformation("Compare Result: {0}", compareResult);

                string? compiledFilesDir = null;
                if (logdir != null && compareResult == CompareResult.Unmatch)
                {
                    var basename = Path.GetFileNameWithoutExtension(mmlFilePath);
                    var dir = Path.Combine(logdir, basename);
                    for (int i = 2; i < 100; i++)
                    {
                        if (!Directory.Exists(dir))
                        {
                            compiledFilesDir = dir;
                            break;
                        }
                        dir = Path.Combine(logdir, string.Format("{0}{1}", basename, i));
                    }
                    if (compiledFilesDir != null)
                    {
                        Directory.CreateDirectory(compiledFilesDir);

                        static void WriteFile(string path, byte[]? bin)
                        {
                            if (bin != null && bin.Length > 0)
                            {
                                using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                                {
                                    fs.Write(bin, 0, bin.Length);
                                }
                            }
                        }

                        WriteFile(Path.Combine(compiledFilesDir, "dotnet.m"), dotnet.result.CompiledBinary);
                        WriteFile(Path.Combine(compiledFilesDir, "dos.m"), dos.CompiledBinary);
                    }
                }

                return new TestResult(
                    mmlFilePath: mmlFilePath,
                    compareResult: compareResult,
                    dotnetResult: dotnet.result.Status,
                    dosResult: dos.Status,
                    dosExitCode: dos.ExitCode,
                    compiledFilesdir: compiledFilesDir);
            }
            catch (Exception e)
            {
                _logger.LogError(e.StackTrace);
                return new TestResult(
                    mmlFilePath: mmlFilePath,
                    compareResult: CompareResult.Unspecified,
                    dotnetResult: CompileStatus.Exception,
                    dosResult: CompileStatus.Exception,
                    dosExitCode: 0,
                    compiledFilesdir: null);
            }
            finally
            {
                _logger.LogInformation("---- Test End - {0}", mmlFilePath);
            }
        }

        public bool MultiTest(string mmlFileDir, string tooldir, string? logdir)
        {
            var mmls = Directory.EnumerateFiles(mmlFileDir, "*.mml", SearchOption.AllDirectories);

            var count = 0;
            var allowfiles = new List<TestResult>();
            var warningfiles = new List<TestResult>();
            var doserrorfiles = new List<TestResult>();
            var errorfiles = new List<TestResult>();
            foreach (var mml in mmls)
            {
                var r = SingleTest(mml, tooldir, logdir);

                if (!r.IsPerfect)
                {
                    if (r.IsAllowed)
                    {
                        allowfiles.Add(r);
                    }
                    else if (r.IsWarning)
                    {
                        warningfiles.Add(r);
                    }
                    else if (r.DosResult == CompileStatus.Failed)
                    {
                        doserrorfiles.Add(r);
                    }
                    else
                    {
                        errorfiles.Add(r);
                    }
                }
                count++;
            }

            _logger.LogInformation("Test Files: {0} files", count);
            _logger.LogInformation("Allowed Files: {0} files", allowfiles.Count);
            _logger.LogInformation("Warning Files: {0} files", warningfiles.Count);
            _logger.LogInformation("DOS Compiler Error Files: {0} files", doserrorfiles.Count);
            _logger.LogInformation("Error Files: {0} files", errorfiles.Count);

            void LogFiles(List<TestResult> list)
            {
                foreach (var item in list)
                {
                    if (item.CompiledFilesDir != null)
                    {
                        _logger.LogInformation("{0}, Compare = {1}, .NET = {2}, DOS = {3}, Binary = {4}", item.MMLFilePath, item.CompareResult, item.DotNetResult, item.DosResult, item.CompiledFilesDir);
                    }
                    else
                    {
                        _logger.LogInformation("{0}, Compare = {1}, .NET = {2}, DOS = {3}", item.MMLFilePath, item.CompareResult, item.DotNetResult, item.DosResult);
                    }
                }
            }

            if (allowfiles.Count > 0)
            {
                _logger.LogInformation("Allowed Files List:");
                LogFiles(allowfiles);
            }

            if (warningfiles.Count > 0)
            {
                _logger.LogInformation("Warning Files List:");
                LogFiles(warningfiles);
            }

            if (doserrorfiles.Count > 0)
            {
                _logger.LogInformation("DOS Compiler Error Files List:");
                foreach (var item in doserrorfiles)
                {
                    _logger.LogInformation("{0}, Compare = {1}, .NET = {2}, DOS = {3} (exitcode = {4})", item.MMLFilePath, item.CompareResult, item.DotNetResult, item.DosResult, item.DosExitCode);
                }
            }

            if (errorfiles.Count > 0)
            {
                _logger.LogInformation("Error Files List:");
                LogFiles(errorfiles);
            }

            return errorfiles.Count == 0;
        }
    }
}
