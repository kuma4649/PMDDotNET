using System;
using Microsoft.Extensions.Logging;

namespace PMDDotNETCompilerTestService
{
    public enum CompileStatus
    {
        Succeeded = 0,
        Failed,
        Exception,
        Warning
    }

    public enum CompareResult
    {
        Unspecified = 0,
        Match,
        Unmatch,
        Match_NotEqualLength,
        Match_WithoutMemo
    }

    public class CompileResult
    {
        public CompileStatus Status { get; }

        public int ExitCode { get; }

        public byte[]? CompiledBinary { get; }

        public string Log { get; }

        public CompileResult(bool succeeded, byte[]? compiledBinary, string log) :
            this(succeeded ? 0 : 1, compiledBinary, log)
        {
        }

        public CompileResult(int exitCode, byte[]? compiledBinary, string log)
        {
            var succeeded = exitCode == 0;
            if (succeeded)
            {
                if (log.IndexOf("Warning", StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    Status = CompileStatus.Succeeded;
                }
                else
                {
                    Status = CompileStatus.Warning;
                }
            }
            else
            {
                if (log.IndexOf("Exception", StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    Status = CompileStatus.Failed;
                }
                else
                {
                    Status = CompileStatus.Exception;
                }
            }
            ExitCode = exitCode;
            CompiledBinary = compiledBinary;
            Log = log;
        }

        public void WriteLog(ILogger logger)
        {
            switch (Status)
            {
                case CompileStatus.Failed: logger.LogError(Log); break;
                case CompileStatus.Exception: logger.LogError(Log); break;
                case CompileStatus.Warning: logger.LogWarning(Log); break;
            }
        }

        public CompareResult Compare(CompileResult target)
        {
            if (Status == CompileStatus.Failed || target.Status == CompileStatus.Failed ||
                Status == CompileStatus.Exception || target.Status == CompileStatus.Exception ||
                CompiledBinary == null || target.CompiledBinary == null)
            {
                return CompareResult.Unspecified;
            }
            int size = Math.Min(CompiledBinary.Length, target.CompiledBinary.Length);

            for (int i = 0; i < size; i++)
            {
                if (CompiledBinary[i] != target.CompiledBinary[i])
                {
                    return i >= GetMemoOffset(CompiledBinary) ? CompareResult.Match_WithoutMemo : CompareResult.Unmatch;
                }
            }

            return CompiledBinary.Length == target.CompiledBinary.Length ? CompareResult.Match : CompareResult.Match_NotEqualLength;
        }

        public int GetMemoOffset(byte[] array)
        {
            if (array.Length < 0x1a)
            {
                return array.Length;
            }

            if (array[1] == 0x1a)
            {
                var offset = array[0x19] + array[0x1a] * 256 - 4 + 1;
                offset = array[offset] + array[offset + 1] * 256 + 1;
                offset = array[offset] + array[offset + 1] * 256;
                return offset;
            }

            return array.Length;
        }
    }
}
