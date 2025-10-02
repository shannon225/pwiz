using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class BlibLockingTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestBlibLocking()
        {
            var random = new Random(0);
            TestFilesDir = new TestFilesDir(TestContext, @"Test\BlibLockingTest.zip");
            int cancelCount = 0;
            foreach (var blibFileName in new[]
                     {
                         "InternationalFilenamesTest.blib",
                         "Bereman_5proteins_spikein.blib"
                     })
            {
                var sourcePath = TestFilesDir.GetTestPath(blibFileName);
                var filePath = TestFilesDir.GetTestPath("test.blib");
                File.Copy(sourcePath, filePath);
                var spec = new BiblioSpecLiteSpec("TestBlibLocking", filePath);
                var library = LoadLibrary(spec, null);
                Assert.IsNotNull(library);
                var startTime = DateTime.UtcNow;
                foreach (var stream in library.ReadStreams)
                {
                    stream?.CloseStream();
                }
                File.Delete(filePath);
                var elapsedTime = DateTime.UtcNow.Subtract(startTime);
                for (int i = 0; i < 1000; i++)
                {
                    File.Copy(sourcePath, filePath);
                    library = null;
                    var cancelAfterTicks = (long)((elapsedTime.Ticks + 1) * 2 * random.NextDouble());
                    try
                    {
                        library = LoadLibrary(spec, TimeSpan.FromTicks(cancelAfterTicks));
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    if (library != null)
                    {
                        foreach (var stream in library.ReadStreams)
                        {
                            stream?.CloseStream();
                        }
                    }
                    else
                    {
                        cancelCount++;
                    }
                    File.Delete(filePath);
                }
            }
            Console.Out.WriteLine("Cancel count: {0}", cancelCount);
        }

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private BiblioSpecLiteLibrary LoadLibrary(BiblioSpecLiteSpec spec, TimeSpan? cancelAfter)
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            if (cancelAfter.HasValue)
            {
                Task.Delay(cancelAfter.Value).ContinueWith(_=>cancellationTokenSource.Cancel());
            }
            var loadMonitor = new DefaultFileLoadMonitor(
                new SilentProgressMonitor(cancellationTokenSource.Token));
            return BiblioSpecLiteLibrary.Load(spec, loadMonitor);
        }
    }
}
