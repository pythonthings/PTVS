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
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient {
    class PythonLanguageClientMiddleLayer : ILanguageClientCompletionProvider {
        private readonly object _replEvaluator;

        public PythonLanguageClientMiddleLayer(object replEvaluator) {
            _replEvaluator = replEvaluator;
        }

        public async Task<object> RequestCompletions(CompletionParams param, Func<CompletionParams, Task<object>> sendRequest) {
            var serverCompletions = await sendRequest(param);

            var jsonObj = (JObject)serverCompletions;
            var items = (JArray)jsonObj.GetValue("items");

            // Example of modifying the server results (update labels)
            foreach (JObject item in items) {
                var label = (JValue)item["label"];
                label.Value = label.Value + " PLS";
            }

            if (_replEvaluator != null) {
                // call into repl evaluator to get completions from running python process
                //var replObj = await _replEvaluator.GetCompletionsAsync();
                //if (replObj != null) {
                //    // TODO: merge REPL results with the ones from LS
                //}
            }

            return serverCompletions;
        }

        public Task<CompletionItem> ResolveCompletion(CompletionItem item, Func<CompletionItem, Task<CompletionItem>> sendRequest) {
            return Task.FromResult<CompletionItem>(null);
        }
    }
}
