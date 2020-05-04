using System;
using System.IO;
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
            
            Assert.IsTrue(service.MultiTest(GetMMLDir(), GetToolDir()));
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
