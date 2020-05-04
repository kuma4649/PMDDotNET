using System;
using System.Collections.Generic;
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

            string[] lines = null;
            try
            {
                string path = Path.GetDirectoryName((typeof(UnitTest1).Assembly.Location));
                string lang = System.Globalization.CultureInfo.CurrentCulture.Name;
                string file = Path.Combine(path, string.Format(otherLangFilename, lang));
                file = file.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(file))
                {
                    file = Path.Combine(path, englishFilename);
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
        public void 複数のMMLコンパイルテスト()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(configure =>
                {
                    configure.Format = ConsoleLoggerFormat.Systemd;
                });
            });
            var logger = loggerFactory.CreateLogger<PMDCompileTestService>();
            var service = new PMDCompileTestService(logger);

            var mmlfilesDir = GetMMLDir();
            Assert.IsTrue(service.MultiTest(mmlfilesDir, GetToolDir()));

            var mmllistfile = Path.Combine(mmlfilesDir, "MMLFiles.txt");
            if (File.Exists(mmllistfile))
            {
                var lines = File.ReadAllLines(mmllistfile);

                foreach(var item in lines)
                {
                    if (Directory.Exists(item))
                    {
                        Assert.IsTrue(service.MultiTest(item, GetToolDir()));
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
    }
}
