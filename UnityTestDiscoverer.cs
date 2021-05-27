using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityTestAdapter
{
    /// <summary>Test Discoverer for test cases in test executable built with Unity unit testing framework.</summary>
    [FileExtension(".exe")]
    [DefaultExecutorUri(UnityTestExecutor.ExtensionUriString)]
    public class UnityTestDiscoverer : ITestDiscoverer
    {
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            //logger.SendMessage(TestMessageLevel.Informational, discoveryContext.RunSettings.SettingsXml);
            logger.SendMessage(TestMessageLevel.Informational, $"Discovering tests in sources...");
            try
            {
                foreach (string source in sources)
                {
                    logger.SendMessage(TestMessageLevel.Informational, $"Discovering tests in {source}...");
                    //UnityTestExecutor.RunSource(source, tr => discoverySink.SendTestCase(tr.TestCase));

                    // HACK: As Unity does not provide a way list tests, looks for test declaration strings in binary
                    string content = File.ReadAllText(source, Encoding.ASCII);
                    if (content.Contains("Unity test run"))
                    {
                        logger.SendMessage(TestMessageLevel.Informational, $"Source is a Unity test run.");

                        foreach (Match match in Regex.Matches(content, @"TEST\(([\w\d]+), *([\w\d]+)\)"))
                        {
                            string testGroup = match.Groups[1].Value;
                            string testName = match.Groups[2].Value;
                            var testCase = new TestCase($"{testGroup}.{testName}", UnityTestExecutor.ExtensionUri, source)
                            {
                                CodeFilePath = "unavailable",
                                LineNumber = 0
                            };
                            //logger.SendMessage(TestMessageLevel.Informational, $"  TestCase: '{testCase.DisplayName}' ('{testCase.FullyQualifiedName}').");
                            discoverySink.SendTestCase(testCase);
                        }
                    }
                    else
                    {
                        logger.SendMessage(TestMessageLevel.Informational, $"Source is not Unity test run.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.SendMessage(TestMessageLevel.Error, ex.ToString());
            }
        }
    }
}
