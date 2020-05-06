using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PMDDotNETCompilerTestService;

namespace PMDDotNETCompilerUnitTest
{
    [TestClass]
    public class UnitTest1
    {
        private static string otherLangFilename = Path.Combine("lang", "PMDDotNETmessage.{0}.txt");
        private static string englishFilename = Path.Combine("lang", "PMDDotNETmessage.txt");

        [TestInitialize]
        public void TestInitialize()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string[]? lines = null;
            try
            {
                var path = Path.GetDirectoryName((typeof(UnitTest1).Assembly.Location));
                var lang = System.Globalization.CultureInfo.CurrentCulture.Name;
                var file = Path.Combine(path ?? string.Empty, string.Format(otherLangFilename, lang));
                file = file.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(file))
                {
                    file = Path.Combine(path ?? string.Empty, englishFilename);
                    file = file.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                }
                lines = File.ReadAllLines(file);
            }
            catch
            {
                ;//握りつぶす
            }

            PMDDotNET.Common.msg.MakeMessageDic(lines);
        }

        [TestMethod]
        public void 複数のMMLコンパイルテスト_Vあり()
        {
            TestMain(new string[] { "/v" });
        }

        [TestMethod]
        public void 複数のMMLコンパイルテスト_Vなし()
        {
            TestMain(null);
        }

        private void TestMain(string[]? options)
        {
            var logdir = GetLogDir();
            Directory.CreateDirectory(logdir);
            var logwriter = File.CreateText(Path.Combine(logdir, "log.txt"));
            using var listener = new TextWriterTraceListener(logwriter.BaseStream);

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(configure =>
                {
                    configure.Format = ConsoleLoggerFormat.Systemd;
                });
                builder.AddTraceSource(new SourceSwitch("TraceSourceLog", SourceLevels.Verbose.ToString()), listener);
            });
            var logger = loggerFactory.CreateLogger<PMDCompileTestService>();
            var service = new PMDCompileTestService(logger);

            var mmlfilesDir = GetMMLDir();
            Assert.IsTrue(service.MultiTest(mmlfilesDir, options, GetToolDir(), logdir));

            var mmllistfile = Path.Combine(mmlfilesDir, "MMLFiles.txt");
            if (File.Exists(mmllistfile))
            {
                var lines = File.ReadAllLines(mmllistfile);

                foreach(var item in lines)
                {
                    if (Directory.Exists(item))
                    {
                        Assert.IsTrue(service.MultiTest(item, options, GetToolDir(), logdir));
                    }
                }
            }
        }

        private static string GetToolDir()
        {
            var dir = Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location);
            if (dir != null)
            {
                return Path.GetFullPath(Path.Combine(dir, "../../../../PMDDotNETCompilerTestService/DOSTOOLS"));
            }
            return Environment.CurrentDirectory;
        }

        private static string GetMMLDir()
        {
            var dir = Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location);
            if (dir != null)
            {
                return Path.GetFullPath(Path.Combine(dir, "../../../MMLFILES"));
            }
            return Environment.CurrentDirectory;
        }

        private static string GetLogDir()
        {
            var dir = Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location);
            if (dir != null)
            {
                return Path.GetFullPath(Path.Combine(dir, string.Format("../../../LOGS/{0}", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"))));
            }
            return Environment.CurrentDirectory;
        }
    }
}
