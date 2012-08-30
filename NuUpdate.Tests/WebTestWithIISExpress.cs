﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using NUnit.Framework;

namespace NuUpdate.Tests {
    [TestFixture]
    [Explicit]
    public class WebTestWithIISExpress : TestBaseWithLogging {
        public static int Port = 8084;
        private IISExpressDriver _iisExpress;

        [TestFixtureSetUp]
        public void StartWebServer() {
            string applicationPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), @"..\..\..\NuUpdate.NuGetTestServer"));

            _iisExpress = new IISExpressDriver();
            _iisExpress.Start(applicationPath, Port);
        }

        [TestFixtureTearDown]
        public void StopWebServer() {
            _iisExpress.Dispose();
        }

        [Test]
        public void DownloadPackage() {
            string path;
            using (CreateTempTestPath(out path)) {
                var sut = new UpdateManager(APP_NAME, null, rep, appPathBase: path);
                var updateInfo = sut.CheckForUpdate().Result;
                sut.DownloadPackage(updateInfo).Wait();
            }
        }
    }

    #region IISExpress

    public abstract class ProcessDriver {
        protected Logger _logger = LogManager.GetCurrentClassLogger();
        protected Process _process;
        private string _logPrefix;

        protected BlockingCollection<string> _output = new BlockingCollection<string>(new ConcurrentQueue<string>());
        protected BlockingCollection<string> _error = new BlockingCollection<string>(new ConcurrentQueue<string>());
        protected BlockingCollection<string> _input = new BlockingCollection<string>(new ConcurrentQueue<string>());

        protected void StartProcess(string exePath, string arguments = "") {
            _logPrefix = new FileInfo(exePath).Name + ": ";

            var psi = new ProcessStartInfo(exePath) {
                LoadUserProfile = false,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            _process = Process.Start(psi);

            Task.Factory.StartNew(() => {
                string line;
                while ((line = _process.StandardOutput.ReadLine()) != null) {
                    _logger.Debug(_logPrefix + line);
                    _output.Add(line);
                }
            });

            Task.Factory.StartNew(() => {
                string line;
                while ((line = _process.StandardError.ReadLine()) != null) {
                    _logger.Error(_logPrefix + line);
                    _error.Add(line);
                }
            });

            Task.Factory.StartNew(() => {
                while (_process.HasExited == false) {
                    try {
                        _process.StandardInput.WriteLine(_input.Take());
                        _process.StandardInput.Flush();
                    } catch (ObjectDisposedException) {
                    }
                }
            });
        }

        protected virtual void Shutdown() {
        }

        public void Dispose() {
            // does not work, see http://jasondentler.com/blog/2012/08/integration-testing-03-iis-express/
            //_input.Add("q");
            //Thread.Sleep(2000);

            _input.Dispose();

            if (_process != null) {
                Shutdown();

                var toDispose = _process;
                _process = null;

                toDispose.Dispose();
            }
        }

        protected Match WaitForConsoleOutputMatching(string pattern, int msMaxWait = 10000, int msWaitInterval = 500) {
            var t = DateTimeOffset.Now;

            var sb = new StringBuilder();
            Match match;
            while (true) {
                string nextLine;
                _output.TryTake(out nextLine, 100);

                if (nextLine == null) {
                    if ((DateTimeOffset.Now - t).TotalMilliseconds > msMaxWait) {
                        throw new TimeoutException("Timeout waiting for regular expression " + pattern + Environment.NewLine + sb);
                    }

                    continue;
                }

                sb.AppendLine(nextLine);

                match = Regex.Match(nextLine, pattern);

                if (match.Success) {
                    break;
                }
            }
            return match;
        }
    }

    internal class IISExpressDriver : ProcessDriver {
        public string Url { get; private set; }

        public void Start(string physicalPath, int port) {
            var sitePhysicalDirectory = physicalPath;

            foreach (var process in Process.GetProcessesByName("iisexpress.exd")) {
                process.Kill();
            }

            if (File.Exists(@"c:\program files (x86)\IIS Express\IISExpress.exe")) {
                StartProcess(@"c:\program files (x86)\IIS Express\IISExpress.exe",
                             @"/systray:false /port:" + port + @" /path:""" + sitePhysicalDirectory + @"""");
            } else {
                StartProcess(@"c:\program files\IIS Express\IISExpress.exe",
                             @"/systray:false /port:" + port + @" /path:""" + sitePhysicalDirectory + @"""");
            }

            var match = WaitForConsoleOutputMatching(@"Successfully registered URL ""([^""]*)""");

            Url = match.Groups[1].Value;
        }

        protected override void Shutdown() {
            try {
                _process.Kill();
            } catch (Exception ex) {
                _logger.DebugException("Killing IIS Express", ex);
            }

            if (!_process.WaitForExit(10000)) {
                throw new Exception("IISExpress did not halt within 10 seconds.");
            }
        }
    }

    #endregion
}