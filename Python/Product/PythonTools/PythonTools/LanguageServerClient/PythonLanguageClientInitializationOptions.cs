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
using System.Collections.Generic;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Required layout for the initializationOptions member of initializeParams
    /// </summary>
    [Serializable]
    public sealed class PythonInitializationOptions {
        [Serializable]
        public struct Interpreter {
            /// <summary>
            /// The serialized info required to restore an interpreter factory
            /// </summary>
            public string assembly;
            public string typeName;
            public Dictionary<string, object> properties;

            /// <summary>
            /// The x.y language version of the interpreter in case the factory
            /// cannot be restored.
            /// </summary>
            public string version;
        }

        public Interpreter interpreter;

        /// <summary>
        /// Paths to search when attempting to resolve module imports.
        /// </summary>
        public string[] searchPaths = Array.Empty<string>();

        /// <summary>
        /// Paths to search for module stubs.
        /// </summary>
        public string[] typeStubSearchPaths = Array.Empty<string>();

        /// <summary>
        /// Controls tooltip display appearance. Different between VS and VS Code.
        /// </summary>
        public InformationDisplayOptions displayOptions = new InformationDisplayOptions();

        /// <summary>
        /// Glob pattern of files and folders to exclude from loading
        /// into the Python analysis engine.
        /// </summary>
        public string[] excludeFiles = Array.Empty<string>();

        /// <summary>
        /// Glob pattern of files and folders under the root folder that
        /// should be loaded into the Python analysis engine.
        /// </summary>
        public string[] includeFiles = Array.Empty<string>();

        /// <summary>
        /// Enables an even higher level of logging via the logMessage event.
        /// This will likely have a performance impact.
        /// </summary>
        public bool traceLogging;
    }

    public sealed class InformationDisplayOptions {
        public string preferredFormat;
        public bool trimDocumentationLines;
        public int maxDocumentationLineLength;
        public bool trimDocumentationText;
        public int maxDocumentationTextLength;
        public int maxDocumentationLines;
    }
}
