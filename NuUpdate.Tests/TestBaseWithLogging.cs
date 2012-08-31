using System;
using System.Diagnostics;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace NuUpdate.Tests {
    public class TestBaseWithLogging {
        protected string _nuGetCachePathForTests = null;

        [TestFixtureSetUp]
        public void PrepareLogging() {
            var config = new LoggingConfiguration();
            var debugTarget = new TraceTarget {
                Layout = "${level:uppercase=true:padding=-8} ${message}"
            };
            config.AddTarget("Debug", debugTarget);
            var rule = new LoggingRule("*", LogLevel.Debug, debugTarget);
            config.LoggingRules.Add(rule);
            LogManager.Configuration = config;
        }

        [TestFixtureSetUp]
        public void CreatePackageCacheForTests() {
            _nuGetCachePathForTests = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_nuGetCachePathForTests);
            LogManager.GetCurrentClassLogger().Debug("Creating NuGet cache path " + _nuGetCachePathForTests);
        }

        [TestFixtureTearDown]
        public void DeletePackageCacheForTests() {
            if (String.IsNullOrEmpty(_nuGetCachePathForTests) && Directory.Exists(_nuGetCachePathForTests)) {
                LogManager.GetCurrentClassLogger().Debug("Deleting NuGet cache path " + _nuGetCachePathForTests);
                Directory.Delete(_nuGetCachePathForTests, true);
            }
        }

        protected IDisposable CreateTempTestPath(out string path) {
            var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
            LogManager.GetCurrentClassLogger().Debug("Creating temp path " + folder);

            path = folder;

            return ActionAsDisposable.Create(() => {
                LogManager.GetCurrentClassLogger().Debug("Deleting temp path " + folder);
                Directory.Delete(folder, true);
            });
        }

        public class ActionAsDisposable : IDisposable {
            private readonly Action _action;

            public static IDisposable Create(Action action) {
                return new ActionAsDisposable(action);
            }

            private ActionAsDisposable(Action action) {
                _action = action;
            }

            public void Dispose() {
                _action();
            }
        }
    }
}