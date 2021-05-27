using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityTestAdapter
{
    /// <summary>Test Executor for test cases in test executable built with Unity unit testing framework.</summary>
    [ExtensionUri(ExtensionUriString)]
    public class UnityTestExecutor : ITestExecutor
    {
        public const string ExtensionUriString = "executor://UnityTestAdapter";
        public static readonly Uri ExtensionUri = new Uri(ExtensionUriString);

        private volatile bool _isCanceled = false;

        /// <summary>Tries to cancel </summary>
        public void Cancel()
        {
            _isCanceled = true;
        }

        /// <summary>Runs the specified test cases executables.</summary>
        /// <param name="tests"></param>
        /// <param name="runContext"></param>
        /// <param name="frameworkHandle"></param>
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            _isCanceled = false;
            try
            {
                foreach (var test in tests)
                {
                    if (_isCanceled)
                        break;

                    frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Running test case {test.DisplayName}...");
                    runTestCase(test, tr => frameworkHandle.RecordResult(tr), runContext, frameworkHandle);
                }
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
            }
        }

        /// <summary>Runs all test cases in specified source executables.</summary>
        /// <param name="sources">The executables from which to run all tests.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">The test framework object used to report test results and perform logging.</param>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            _isCanceled = false;
            try
            {
                foreach (string source in sources)
                {
                    if (_isCanceled)
                        break;

                    frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Running source {source}...");
                    runSource(source, tr => frameworkHandle.RecordResult(tr));
                }
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
            }
        }

        private static void runTestCase(TestCase testCase, Action<TestResult> action, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var splitName = testCase.FullyQualifiedName.Split('.');
            runExecutable(testCase.Source, $"-v -g {splitName[0]} -n {splitName[1]}", action, runContext, frameworkHandle);
        }

        private static void runSource(string source, Action<TestResult> action)
        {
            runExecutable(source, "-v", action, null, null);
        }

        private static void runExecutable(string source, string arguments, Action<TestResult> action, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var psi = new ProcessStartInfo
            {
                FileName = source,
                Arguments = arguments,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (runContext?.IsBeingDebugged == true && frameworkHandle != null)
            {
                int processId = frameworkHandle.LaunchProcessWithDebuggerAttached(source, Path.GetDirectoryName(source), arguments, new Dictionary<string, string>());
                using (var process = Process.GetProcessById(processId))
                    process.WaitForExit();
            }
            else
            {
                using (var process = Process.Start(psi))
                {
                    string line;
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        var match = Regex.Match(line, @"(TEST\(([\w\d]+), *([\w\d]+)\)) *([^\n]+)");
                        if (match.Success)
                        {
                            string testGroup = match.Groups[2].Value;
                            string testName = match.Groups[3].Value;
                            string result = match.Groups[4].Value;
                            bool passed = result == "PASS";
                            string errorStackTrace = null;
                            string errorMessage = null;
                            if (!passed)
                            {
                                var splitFail = result.Split(new string[] { "::" }, StringSplitOptions.None);
                                errorStackTrace = splitFail[0].Trim();
                                errorMessage = splitFail[1].Trim();
                                if (errorMessage.StartsWith("FAIL: "))
                                    errorMessage = errorMessage.Substring(6);

                                int splitIndex = errorStackTrace.LastIndexOf(':');
                                if (splitIndex > 0)
                                {
                                    string filePath = errorStackTrace.Substring(0, splitIndex);
                                    string fileLine = errorStackTrace.Substring(splitIndex + 1);
                                    errorStackTrace = $"   at TEST_{testGroup}_{testName}_() in {filePath}:line {fileLine}\r\n";
                                }
                            }

                            var testCase = new TestCase($"{testGroup}.{testName}", ExtensionUri, source)
                            {
                                CodeFilePath = "unavailable",
                                LineNumber = 0
                            };
                            var testResult = new TestResult(testCase)
                            {
                                Outcome = passed ? TestOutcome.Passed : TestOutcome.Failed,
                                ErrorMessage = errorMessage,
                                ErrorStackTrace = errorStackTrace
                            };
                            frameworkHandle?.SendMessage(TestMessageLevel.Informational, $"  {testCase.DisplayName}: {testResult.Outcome}");
                            action(testResult);
                        }
                    }
                    process.WaitForExit();
                }
            }
        }
    }
}
