using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace NuUpdate.Tests {
    public class TestBaseWithLogging {
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
    }
}