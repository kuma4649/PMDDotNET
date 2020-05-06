using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using musicDriverInterface;
using PMDDotNET.Compiler;

namespace PMDDotNETCompilerTestService
{
    public class DotnetCompiler
    {
        public static (CompileResult result, string outputFileName) Compile(string mmlFilePath, string[]? options)
        {
            var log = new StringBuilder();
            Log.writeLine = (level, msg) => log.AppendFormat("[{0,-7}] {1}{2}", level, msg, Environment.NewLine);

            var fullpath = Path.GetFullPath(mmlFilePath);
            var dir = Path.GetDirectoryName(fullpath) ?? Environment.CurrentDirectory;
            var fname = Path.GetFileName(fullpath);

            var compiler = new Compiler();
            compiler.Init();

            Func<string?, Stream?> fnAppendFileReaderCallback = fname =>
            {
                try
                {
                    if (fname != null)
                    {
                        return new FileStream(Path.Combine(dir, fname), FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
                catch
                {
                }
                return null;
            };

            var outputFileName = GetOutputFileName(compiler, mmlFilePath, fnAppendFileReaderCallback);

            using (var fs = new FileStream(fullpath, FileMode.Open, FileAccess.Read,FileShare.Read))
            using (var ms = new MemoryStream())
            {
                if (options != null)
                {
                    var tmp = new List<string>(options.Length + 1);
                    foreach (var item in options)
                    {
                        tmp.Add(item);
                    }
                    tmp.Add(fname);
                    compiler.mcArgs = tmp.ToArray();
                }
                else
                {
                    compiler.mcArgs = new string[] { fname };
                }

                var envs = new List<string>();
                void AddEnv(string envname)
                {
                    var env = Environment.GetEnvironmentVariable(envname, EnvironmentVariableTarget.User);
                    if (!string.IsNullOrEmpty(env))
                    {
                        envs.Add(string.Format("{0}={1}", envname, env));
                    }
                }

                AddEnv("ARRANGER");
                AddEnv("COMPOSER");
                AddEnv("USER");
                AddEnv("MCOPT");
                AddEnv("PMD");
                compiler.env = envs.ToArray();

                var r = compiler.Compile(fs, ms, fnAppendFileReaderCallback);
                ms.Flush();

                return (new CompileResult(succeeded: r, compiledBinary: ms?.ToArray(), log: log.ToString(), memoWriteAddress: compiler.memo_writeAddress), outputFileName);
            }
        }

        private static string GetOutputFileName(Compiler compiler, string mmlFilePath, Func<string?, Stream?> fnAppendFileReaderCallback)
        {
            using (var sourceMML = new FileStream(mmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(sourceMML, Encoding.GetEncoding(932)))
            {
                var srcText = sr.ReadToEnd();
                var tags = compiler.GetTags(srcText, fnAppendFileReaderCallback);
                if (tags != null)
                {
                    foreach (var item in tags)
                    {
                        //mcは3文字まで判定している為
                        if (item.Item1.ToUpper().IndexOf("#FI") == 0)
                        {
                            if (item.Item2[0] == '.')
                            {
                                return Path.GetFileNameWithoutExtension(mmlFilePath) + item.Item2;
                            }
                            else
                            {
                                return Path.GetFileName(item.Item2);
                            }
                        }
                    }
                }
            }
            return Path.GetFileNameWithoutExtension(mmlFilePath) + ".M";
        }
    }
}
