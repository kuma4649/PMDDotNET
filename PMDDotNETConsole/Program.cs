using PMDDotNET.Compiler;
using PMDDotNET.Common;
using musicDriverInterface;
using System;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using System.Collections.Generic;

namespace PMDDotNET.Console
{
    class Program
    {
        private static string srcFile;
        private static string ffFile;
        private static string desFile;
        private static bool isXml = false;

        static void Main(string[] args)
        {
            Log.writeLine = WriteLine;
#if DEBUG
            Log.level = LogLevel.INFO;//.INFO;
            Log.off = 0;
#else
            Log.level = LogLevel.INFO;
#endif
            int fnIndex = AnalyzeOption(args);

            if (args == null || args.Length < 1)
            {
                WriteLine(LogLevel.ERROR, msg.get("E0600"));
                return;
            }

            try
            {
#if NETCOREAPP
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

                Compile(args, fnIndex);

            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.FATAL, ex.Message);
                Log.WriteLine(LogLevel.FATAL, ex.StackTrace);
            }
        }

        static void WriteLine(LogLevel level, string msg)
        {
            System.Console.WriteLine("[{0,-7}] {1}", level, msg);
        }

        static void WriteLine(string msg)
        {
            System.Console.WriteLine(msg);
        }

        static void Compile(string srcFile)
        {
            try
            {
                Program.srcFile = srcFile;

                Compiler.Compiler compiler = new Compiler.Compiler();
                compiler.Init();

                compiler.mcArgs = new string[]
                {
                    srcFile
                };

                if (!isXml)
                {
                    string destFileName = Path.Combine(
                        Path.GetDirectoryName(Path.GetFullPath(srcFile))
                        , string.Format("{0}.m", Path.GetFileNameWithoutExtension(srcFile)));

                    using (FileStream sourceMML = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destCompiledBin = new FileStream(destFileName, FileMode.Create, FileAccess.Write))
                    using (Stream bufferedDestStream = new BufferedStream(destCompiledBin))
                    {
                        compiler.Compile(sourceMML, bufferedDestStream, appendFileReaderCallback);
                    }
                }
                else
                {
                    string destFileName = Path.Combine(
                        Path.GetDirectoryName(Path.GetFullPath(srcFile))
                        , string.Format("{0}.xml", Path.GetFileNameWithoutExtension(srcFile)));
                    MmlDatum[] dest = null;

                    using (FileStream sourceMML = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        dest = compiler.Compile(sourceMML, appendFileReaderCallback);
                    }

                    XmlSerializer serializer = new XmlSerializer(typeof(MmlDatum[]), typeof(MmlDatum[]).GetNestedTypes());
                    using (StreamWriter sw = new StreamWriter(destFileName, false, Encoding.UTF8))
                    {
                        serializer.Serialize(sw, dest);
                    }

                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.FATAL, ex.Message);
                Log.WriteLine(LogLevel.FATAL, ex.StackTrace);
            }
            finally
            {
            }

        }

        static void Compile(string[] args,int argIndex)
        {
            try
            {
                //mc向け引数のリストを作る
                List<string> lstMcArg = new List<string>();
                for(int i = argIndex; i < args.Length; i++)
                    lstMcArg.Add(args[i]);



                Compiler.Compiler compiler = new Compiler.Compiler();
                compiler.Init();
                compiler.mcArgs = lstMcArg.ToArray();
                string arranger = Environment.GetEnvironmentVariable("arranger", EnvironmentVariableTarget.User);
                if (arranger != null) arranger = "ARRANGER=" + arranger;
                string composer = Environment.GetEnvironmentVariable("composer", EnvironmentVariableTarget.User);
                if (composer != null) composer = "COMPOSER=" + composer;
                string user = Environment.GetEnvironmentVariable("user", EnvironmentVariableTarget.User);
                if (user != null) user = "USER=" + user;
                string mcopt = Environment.GetEnvironmentVariable("mcopt", EnvironmentVariableTarget.User);
                if (mcopt != null) user = "MCOPT=" + mcopt;
                compiler.env = new string[]
                {
                    arranger,composer,user,mcopt
                };

                //各種ファイルネームを得る
                int s = 0;
                foreach (string arg in compiler.mcArgs)
                {
                    if (string.IsNullOrEmpty(arg)) continue;
                    if (arg[0] == '-' || arg[0] == '/') continue;
                    if (s == 0) srcFile = arg;
                    else if (s == 1) ffFile = arg;
                    else if (s == 2) desFile = arg;
                    s++;
                }

                byte[] ffFileBuf = null;
                if (!string.IsNullOrEmpty(ffFile) && File.Exists(ffFile))
                {
                    ffFileBuf = File.ReadAllBytes(ffFile);
                    compiler.SetFfFileBuf(ffFileBuf);
                }
                //compiler.SetCompileSwitch("IDE");
                //compiler.SetCompileSwitch("SkipPoint=R19:C30");

                if (!isXml)
                {
                    //デフォルトはソースファイル名の拡張子を.Mに変更したものにする
                    string destFileName = "";
                    if (!string.IsNullOrEmpty(srcFile))
                    {
                        destFileName = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(srcFile)), string.Format("{0}.M", Path.GetFileNameWithoutExtension(srcFile)));
                    }

                    //TagからFilenameを得る
                    string srcText;
                    using (FileStream sourceMML = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (StreamReader sr = new StreamReader(sourceMML, Encoding.GetEncoding("Shift_JIS")))
                        srcText = sr.ReadToEnd();

                    string outFileName = "";
                    Tuple<string, string>[] tags = compiler.GetTags(srcText, appendFileReaderCallback);
                    if (tags != null && tags.Length > 0)
                    {
                        foreach (Tuple<string, string> tag in tags)
                        {
                            Log.WriteLine(LogLevel.TRACE, string.Format("{0}\t: {1}", tag.Item1, tag.Item2));

                            //出力ファイル名を得る
                            if (tag.Item1.ToUpper().IndexOf("#FI") != 0) continue;//mcは3文字まで判定している為
                            outFileName = tag.Item2;
                        }
                    }

                    //TagにFileName指定がある場合はそちらを適用する
                    if (!string.IsNullOrEmpty(outFileName))
                    {
                        if (outFileName[0] != '.')
                        {
                            //ファイル名指定の場合
                            destFileName = Path.Combine(
                                Path.GetDirectoryName(Path.GetFullPath(srcFile))
                                , outFileName);
                        }
                        else
                        {
                            //拡張子のみの指定の場合
                            destFileName = Path.Combine(
                                Path.GetDirectoryName(Path.GetFullPath(srcFile))
                                , string.Format("{0}{1}"
                                , Path.GetFileNameWithoutExtension(srcFile)
                                , outFileName));
                        }
                    }

                    //最終的にdesFileの指定がある場合は、そちらを優先する
                    if (desFile != null)
                    {
                        destFileName = desFile;
                    }

                    bool isSuccess = false;
                    using (FileStream sourceMML = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    //using (FileStream destCompiledBin = new FileStream(destFileName, FileMode.Create, FileAccess.Write))
                    using (MemoryStream destCompiledBin = new MemoryStream())
                    using (Stream bufferedDestStream = new BufferedStream(destCompiledBin))
                    {
                        isSuccess = compiler.Compile(sourceMML, bufferedDestStream, appendFileReaderCallback);

                        if (isSuccess)
                        {
                            bufferedDestStream.Flush();
                            byte[] destbuf = destCompiledBin.ToArray();
                            File.WriteAllBytes(destFileName, destbuf);
                        }
                    }

                }
                else
                {
                    string destFileName = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(srcFile)), string.Format("{0}.xml", Path.GetFileNameWithoutExtension(srcFile)));
                    if (desFile != null)
                    {
                        destFileName = desFile;
                    }
                    MmlDatum[] dest = null;

                    using (FileStream sourceMML = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        dest = compiler.Compile(sourceMML, appendFileReaderCallback);
                    }

                    XmlSerializer serializer = new XmlSerializer(typeof(MmlDatum[]), typeof(MmlDatum[]).GetNestedTypes());
                    using (StreamWriter sw = new StreamWriter(destFileName, false, Encoding.UTF8))
                    {
                        serializer.Serialize(sw, dest);
                    }

                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.FATAL, ex.Message);
                Log.WriteLine(LogLevel.FATAL, ex.StackTrace);
            }
            finally
            {
            }

        }


        private static Stream appendFileReaderCallback(string arg)
        {

            string fn = Path.Combine(
                Path.GetDirectoryName(srcFile)
                , arg
                );

            FileStream strm;
            try
            {
                strm = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                strm = null;
            }

            return strm;
        }


        private static int AnalyzeOption(string[] args)
        {
            if (args.Length < 1) return 0;

            int i = 0;
            while (args[i] != null && args[i].Length > 0 && args[i][0] == '-')
            {
                string op = args[i].Substring(1).ToUpper();
                if (op == "LOGLEVEL=FATAL")
                {
                    Log.level = LogLevel.FATAL;
                }
                else if (op == "LOGLEVEL=ERROR")
                {
                    Log.level = LogLevel.ERROR;
                }
                else if (op == "LOGLEVEL=WARNING")
                {
                    Log.level = LogLevel.WARNING;
                }
                else if (op == "LOGLEVEL=INFO")
                {
                    Log.level = LogLevel.INFO;
                }
                else if (op == "LOGLEVEL=DEBUG")
                {
                    Log.level = LogLevel.DEBUG;
                }
                else if (op == "LOGLEVEL=TRACE")
                {
                    Log.level = LogLevel.TRACE;
                }
                else if (op == "OFFLOG=WARNING")
                {
                    Log.off = (int)LogLevel.WARNING;
                }
                else if (op == "XML")
                {
                    isXml = true;
                }
                else
                {
                    break;
                }

                i++;
            }

            return i;
        }

    }
}
