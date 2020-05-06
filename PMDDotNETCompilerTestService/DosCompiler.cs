using System;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace PMDDotNETCompilerTestService
{
    public class DosCompiler
    {
        public static CompileResult Compile(string mmlFilePath, string[]? options, string outputFileName, string tooldir)
        {
            var tooldirFull = Path.GetFullPath(tooldir);
            var currentDir = Environment.CurrentDirectory;
            try
            {
                var fullpath = Path.GetFullPath(mmlFilePath);
                var dir = Path.GetDirectoryName(fullpath);
                var fname = Path.GetFileName(fullpath);

                if (dir != null)
                {
                    Environment.CurrentDirectory = dir;
                }

                var psi = new ProcessStartInfo()
                {
                    FileName = Path.Combine(tooldirFull, "msdos.exe"),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                string option;
                if (options != null && options.Length > 0)
                {
                    var tmp = new StringBuilder();
                    foreach(var item in options)
                    {
                        tmp.AppendFormat(" {0}", item);
                    }
                    option = tmp.ToString();
                }
                else
                {
                    option = " ";
                }
                psi.Arguments = string.Format("{0}{1} {2}", Path.Combine(tooldirFull, "MC"), option, fname);


                using (var p = Process.Start(psi))
                {
                    var stdout = p.StandardOutput.ReadToEnd();
                    var stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    string? outputFileName2 = null;
                    if (File.Exists(outputFileName))
                    {
                        outputFileName2 = outputFileName;
                    }
                    else
                    {
                        //  拡張子のみファイルが生成されるパターンがある?
                        var ext = Path.GetExtension(outputFileName);
                        if (File.Exists(ext))
                        {
                            outputFileName2 = ext;
                        }
                    }

                    try
                    {
                        byte[]? compiledBinary = null;
                        if (p.ExitCode == 0 && outputFileName2 != null && File.Exists(outputFileName2))
                        {
                            byte[] buffer;
                            using (var fs = new FileStream(outputFileName, FileMode.Open))
                            {
                                buffer = new byte[fs.Length];
                                fs.Read(buffer, 0, buffer.Length);
                            }

                            compiledBinary = buffer;
                        }

                        var log = (stdout?.Equals(stderr)).GetValueOrDefault() ?
                            stdout :
                            string.Format("stdout:{0}{1}{0}stderr:{2}", Environment.NewLine, stdout, stderr);

                        return new CompileResult(p.ExitCode, compiledBinary: compiledBinary, log: log ?? string.Empty);
                    }
                    finally
                    {
                        if (outputFileName2 != null && File.Exists(outputFileName2))
                        {
                            File.Delete(outputFileName2);
                        }
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = currentDir;
            }
        }
    }
}
