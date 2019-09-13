// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class TestExplorerTests {
        private const string resultStackTraceSection = "Result StackTrace:";
        private const string resultMessageSection = "Result Message:";

        private static TestInfo[] AllPytests = new TestInfo[] {
            // test_pt.py
            new TestInfo("test__pt_fail", "test_pt", "test_pt", "test_pt.py", 4, "Failed", "assert False"),
            new TestInfo("test__pt_pass", "test_pt", "test_pt", "test_pt.py", 1, "Passed"),
            new TestInfo("test_method_pass", "test_pt", "TestClassPT", "test_pt.py", 8, "Passed"),

            // test_ut.py
            new TestInfo("test__ut_fail", "test_ut", "TestClassUT", "test_ut.py", 4, "Failed", "AssertionError: Not implemented"),
            new TestInfo("test__ut_pass", "test_ut", "TestClassUT", "test_ut.py", 7, "Passed"),

            // test_mark.py
            new TestInfo("test_webtest", "test_mark", "test_mark", "test_mark.py", 5, "Passed"),
            new TestInfo("test_skip", "test_mark", "test_mark", "test_mark.py", 9, "Skipped", "skip unconditionally"),
            new TestInfo("test_skipif_not_skipped", "test_mark", "test_mark", "test_mark.py", 17, "Passed"),
            new TestInfo("test_skipif_skipped", "test_mark", "test_mark", "test_mark.py", 13, "Skipped", "skip VAL == 1"),

            // test_fixture.py
            new TestInfo("test_data[0]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Passed"),
            new TestInfo("test_data[1]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Passed"),
            new TestInfo("test_data[3]", "test_fixture", "test_fixture", "test_fixture.py", 7, "Failed", "assert 3 != 3"),
        };

        private static TestInfo[] AllUnittests = new TestInfo[] {
            new TestInfo("test_failure", "test1", "Test_test1", "test1.py", 4, "Failed", "Not implemented", new[] {
                "", // skip the python version - see https://github.com/microsoft/PTVS/issues/5463
                "test1.py\", line 11, in helper",
                "self.fail('Not implemented')",
                "test1.py\", line 5, in test_failure",
                "self.helper()",
            }),
            new TestInfo("test_success", "test1", "Test_test1", "test1.py", 7, "Passed"),
        };

        public void RunAllUnittestProject(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\TestExplorerUnittest.sln");
            app.OpenProject(sln);

            RunAllTests(app, AllUnittests);
        }

        public void RunAllUnittestWorkspace(PythonVisualStudioApp app) {
            var workspaceFolderPath = PrepareWorkspace(
                "unittest",
                "TestExplorerUnittest",
                TestData.GetPath("TestData", "TestExplorerUnittest")
            );

            app.OpenFolder(workspaceFolderPath);

            RunAllTests(app, AllUnittests);
        }

        public void RunAllPytestProject(PythonVisualStudioApp app) {
            var defaultSetter = new InterpreterWithPackageSetter(app.ServiceProvider, "pytest");
            using (defaultSetter) {
                var sln = app.CopyProjectForTest(@"TestData\TestExplorerPytest.sln");
                app.OpenProject(sln);

                RunAllTests(app, AllPytests);
            }
        }

        public void RunAllPytestWorkspace(PythonVisualStudioApp app) {
            var defaultSetter = new InterpreterWithPackageSetter(app.ServiceProvider, "pytest");
            using (defaultSetter) {
                var workspaceFolderPath = PrepareWorkspace(
                    "pytest",
                    "TestExplorerPytest",
                    TestData.GetPath("TestData", "TestExplorerPytest")
                );

                app.OpenFolder(workspaceFolderPath);

                RunAllTests(app, AllPytests);
            }
        }

        private static string PrepareWorkspace(string framework, string workspaceName, string sourceProjectFolderPath) {
            // Create a workspace folder with test framework enabled and with the
            // set of files from the source project folder
            var workspaceFolderPath = Path.Combine(TestData.GetTempPath(), workspaceName);
            Directory.CreateDirectory(workspaceFolderPath);

            var pythonSettingsJson = "{\"TestFramework\": \"" + framework + " \"}";
            File.WriteAllText(Path.Combine(workspaceFolderPath, "PythonSettings.json"), pythonSettingsJson);

            foreach (var filePath in Directory.GetFiles(sourceProjectFolderPath, "*.py")) {
                var destFilePath = Path.Combine(workspaceFolderPath, Path.GetFileName(filePath));
                File.Copy(filePath, destFilePath, true);
            }

            return workspaceFolderPath;
        }

        private static void RunAllTests(PythonVisualStudioApp app, TestInfo[] tests) {
            var testExplorer = app.OpenTestExplorer();
            Assert.IsNotNull(testExplorer, "Could not open test explorer");

            Console.WriteLine("Waiting for tests discovery");
            app.WaitForOutputWindowText("Tests", $"Discovery finished: {tests.Length} tests found", 15_000);

            testExplorer.GroupByProjectNamespaceClass();

            foreach (var test in tests) {
                var item = testExplorer.WaitForItem(test.Path);
                Assert.IsNotNull(item, $"Coult not find {string.Join(":", test.Path)}");
            }

            Console.WriteLine("Running all tests");
            testExplorer.RunAll(TimeSpan.FromSeconds(10));
            app.WaitForOutputWindowText("Tests", $"Run finished: {tests.Length} tests run", 10_000);

            foreach (var test in tests) {
                var item = testExplorer.WaitForItem(test.Path);
                Assert.IsNotNull(item, $"Coult not find {string.Join(":", test.Path)}");

                item.Select();
                item.SetFocus();

                var actualDetails = testExplorer.GetDetailsWithRetry();

                AssertUtil.Contains(actualDetails, $"Test Name:	{test.Name}");
                AssertUtil.Contains(actualDetails, $"Test Outcome:	{test.Outcome}");
                AssertUtil.Contains(actualDetails, $"{test.SourceFile} : line {test.SourceLine}");

                if (test.ResultMessage != null) {
                    AssertUtil.Contains(actualDetails, $"{resultMessageSection}	{test.ResultMessage}");
                }

                if (test.CallStack != null) {
                    var actualStack = ParseCallStackFromResultDetails(actualDetails);

                    Assert.AreEqual(test.CallStack.Length, actualStack.Length, "Unexpected stack depth.");
                    for (int i = 0; i < test.CallStack.Length; i++) {
                        AssertUtil.Contains(actualStack[i], test.CallStack[i]);
                    }
                }
            }
        }

        private static string[] ParseCallStackFromResultDetails(string details) {
            int startIndex = details.IndexOf(resultStackTraceSection);
            if (startIndex < 0) {
                Assert.Fail("Stack trace was expected but not found in test result details");
            }

            // There's always a message section when there's an error,
            // and that marks the end of the stack trace section.
            startIndex += resultStackTraceSection.Length;
            int endIndex = details.IndexOf(resultMessageSection, startIndex);
            if (endIndex < 0) {
                Assert.Fail("Message section was expected but not found in test result details");
            }

            return details
                .Substring(startIndex, endIndex - startIndex)
                .Trim()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        class TestInfo {
            public TestInfo(string name, string project, string classOrModule, string sourceFile, int sourceLine, string outcome, string resultMessage = null, string[] callStack = null) {
                Name = name;
                Project = project;
                ClassOrModule = classOrModule;
                SourceFile = sourceFile;
                SourceLine = sourceLine;
                Outcome = outcome;
                ResultMessage = resultMessage;
                CallStack = callStack;

                Path = new string[] { Project, SourceFile, ClassOrModule, Name };
            }

            public string Name { get; }
            public string Project { get; }
            public string ClassOrModule { get; }
            public string SourceFile { get; }
            public int SourceLine { get; }
            public string Outcome { get; }
            public string ResultMessage { get; }
            public string[] Path { get; }
            public string[] CallStack { get; }
        }
    }
}
