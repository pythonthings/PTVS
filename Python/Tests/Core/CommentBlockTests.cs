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

using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class CommentBlockTests {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentCurrentLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello'
print 'goodbye'
");


            editorTestToolset.UIThread.Invoke(() => {
                view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"#print 'hello'
print 'goodbye'
",
                view.GetText());

            editorTestToolset.UIThread.Invoke(() => {
                view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"#print 'hello'
#print 'goodbye'
",
                view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestUnCommentCurrentLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"#print 'hello'
#print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);
                CommentHelper.CommentOrUncommentBlock(view, false);
            });

            Assert.AreEqual(@"print 'hello'
#print 'goodbye'", 
                 view.GetText());

            editorTestToolset.UIThread.Invoke(() => {
                view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
                CommentHelper.CommentOrUncommentBlock(view, false);
            });

            Assert.AreEqual(@"print 'hello'
print 'goodbye'",
                view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestComment() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello'
print 'goodbye'
");

            editorTestToolset.UIThread.Invoke(() => {
                view.SelectAll();
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"#print 'hello'
#print 'goodbye'
",
                 view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentEmptyLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello'

print 'goodbye'
");

            editorTestToolset.UIThread.Invoke(() => {
                view.SelectAll();
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"#print 'hello'

#print 'goodbye'
",
                 view.GetText());
        }

        private static MockTextBuffer MockTextBuffer(string code) {
            return new MockTextBuffer(code, PythonCoreConstants.ContentType, "C:\\fob.py");
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentWhiteSpaceLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello'
   
print 'goodbye'
");

            editorTestToolset.UIThread.Invoke(() => {
                view.SelectAll();
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"#print 'hello'
   
#print 'goodbye'
",
                 view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentIndented() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"def f():
    print 'hello'
    print 'still here'
    print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(@"    print 'hello'
    print 'still here'");
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"def f():
    #print 'hello'
    #print 'still here'
    print 'goodbye'",
                    view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentIndentedBlankLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"def f():
    print 'hello'

    print 'still here'
    print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(@"    print 'hello'

    print 'still here'");
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"def f():
    #print 'hello'

    #print 'still here'
    print 'goodbye'",
                    view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentBlankLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print('hi')

print('bye')");

            editorTestToolset.UIThread.Invoke(() => {
                view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"print('hi')

print('bye')",
             view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentIndentedWhiteSpaceLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"def f():
    print 'hello'
  
    print 'still here'
    print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(@"    print 'hello'
  
    print 'still here'");
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            Assert.AreEqual(@"def f():
    #print 'hello'
  
    #print 'still here'
    print 'goodbye'",
                    view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestUnCommentIndented() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"def f():
    #print 'hello'
    #print 'still here'
    print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(@"    #print 'hello'
    #print 'still here'");
                CommentHelper.CommentOrUncommentBlock(view, false);
            });

            Assert.AreEqual(@"def f():
    print 'hello'
    print 'still here'
    print 'goodbye'",
                    view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestUnComment() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"#print 'hello'
#print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.SelectAll();
                CommentHelper.CommentOrUncommentBlock(view, false);
            });

            var expected = @"print 'hello'
print 'goodbye'";
            Assert.AreEqual(expected, view.GetText());
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/814
        /// </summary>
        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentStartOfLastLine() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello'
print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(@"print 'hello'
");
                CommentHelper.CommentOrUncommentBlock(view, true);
            });

            var expected = @"#print 'hello'
print 'goodbye'";
            Assert.AreEqual(expected, view.GetText());
        }

        [TestMethod, Priority(TestExtensions.P0_FAILING_UNIT_TEST)]
        public void TestCommentAfterCodeIsNotUncommented() {
            var editorTestToolset = new EditorTestToolset();
            var view = editorTestToolset.CreatePythonTextView(@"print 'hello' #comment that should stay a comment
#print 'still here' # another comment that should stay a comment
print 'goodbye'");

            editorTestToolset.UIThread.Invoke(() => {
                view.Select(0, view.GetText().IndexOf("print 'goodbye'"));
                CommentHelper.CommentOrUncommentBlock(view, false);
            });

            Assert.AreEqual(@"print 'hello' #comment that should stay a comment
print 'still here' # another comment that should stay a comment
print 'goodbye'",
                view.GetText());
        }
    }
}
