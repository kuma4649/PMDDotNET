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
        [TestMethod]
        public void MultiMMLTest()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
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
                var lines = new List<string>();
                using (var sr = new StreamReader(mmllistfile, Encoding.UTF8))
                {
                    while (true)
                    {
                        var l = sr.ReadLine();
                        if (l == null)
                        {
                            break;
                        }
                        lines.Add(l);
                    }
                }

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
