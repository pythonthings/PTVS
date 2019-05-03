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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Implementation of the language server client.
    /// </summary>
    /// <remarks>
    /// See documentation at https://docs.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension?view=vs-2019
    /// </remarks>
    [ContentType(PythonCoreConstants.ContentType)]
    [Export(typeof(ILanguageClient))]
    class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage {
        private readonly IServiceProvider _site;
        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private JsonRpc _rpc;

        [ImportingConstructor]
        public PythonLanguageClient(
            [Import(typeof(SVsServiceProvider))] IServiceProvider site,
            [Import] IVsFolderWorkspaceService workspaceService,
            [Import] IInterpreterOptionsService optionsService,
            [Import] IInterpreterRegistryService registryService
        ) {
            // TODO: if this is a language client for a REPL window, we need to pass in the REPL evaluator to middle layer
            MiddleLayer = new PythonLanguageClientMiddleLayer(null);
            CustomMessageTarget = new PythonLanguageClientCustomTarget(site);
            _site = site;
            _workspaceService = workspaceService;
            _optionsService = optionsService;
            _registryService = registryService;
        }

        public string Name => "Python Language Extension";

        public IEnumerable<string> ConfigurationSections {
            get {
                // Called by Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageServiceBroker.UpdateClientsWithConfigurationSettingsAsync
                // Used to send LS WorkspaceDidChangeConfiguration notification
                yield return "python";
            }
        }

        // called from Microsoft.VisualStudio.LanguageServer.Client.RemoteLanguageClientInstance.InitializeAsync
        // which sets Capabilities, RootPath, ProcessId, and InitializationOptions (to this property value)
        // initParam.Capabilities.TextDocument.Rename = new DynamicRegistrationSetting(false); ??
        // 
        // in vscode, the equivalent is in src/client/activation/languageserver/analysisoptions
        public object InitializationOptions { get; private set; }

        // TODO: what do we do with this?
        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer { get; private set; }

        public object CustomMessageTarget { get; private set; }

        public event AsyncEventHandler<EventArgs> StartAsync;

#pragma warning disable CS0067
        public event AsyncEventHandler<EventArgs> StopAsync;
#pragma warning restore CS0067

        public async Task<Connection> ActivateAsync(CancellationToken token) {
            await Task.Yield();

            var info = PythonLanguageClientStartInfo.Create();

            var process = new Process {
                StartInfo = info
            };

            if (process.Start()) {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public async Task OnLoadedAsync() {
            var workspace = _workspaceService.CurrentWorkspace;
            var workspaceFolder = workspace.Location;

            // Force initialization of python tools service by requesting it
            _site.GetPythonToolsService();

            string interpreterPath = string.Empty;
            string interpreterVersion = string.Empty;
            var factory = workspace.GetInterpreterFactory(_registryService, _optionsService);
            if (factory != null) {
                interpreterPath = factory.Configuration.InterpreterPath;
                interpreterVersion = factory.Configuration.Version.ToString();
            }

            // VSCode captures the python.exe env variables, uses PYTHONPATH to build this list
            var searchPaths = new List<string>();
            searchPaths.AddRange(workspace.GetAbsoluteSearchPaths());

            InitializationOptions = new PythonInitializationOptions {
                // we need to read from the workspace settings in order to populate this correctly
                // (or from the project)
                interpreter = new PythonInitializationOptions.Interpreter {
                    properties = new Dictionary<string, object> {
                        { "InterpreterPath", interpreterPath },
                        { "Version", interpreterVersion },
                        { "DatabasePath", PythonLanguageClientStartInfo.DatabaseFolderPath }
                    }
                },
                searchPaths = searchPaths.ToArray(),
                typeStubSearchPaths = new[] {
                    Path.Combine(PythonLanguageClientStartInfo.TypeshedFolderPath)
                },
                excludeFiles = new[] {
                    "**/Lib/**",
                    "**/site-packages/**",
                    "**/node_modules",
                    "**/bower_components",
                    "**/.git",
                    "**/.svn",
                    "**/.hg",
                    "**/CVS",
                    "**/.DS_Store",
                    "**/.git/objects/**",
                    "**/.git/subtree-cache/**",
                    "**/node_modules/*/**",
                    ".vscode/*.py",
                    "**/site-packages/**/*.py"
                }
            };

            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync() {
            return Task.CompletedTask;
        }

        public Task OnServerInitializeFailedAsync(Exception e) {
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc) {
            _rpc = rpc;
            return Task.CompletedTask;
        }

        public async Task SendServerCustomNotificationAsync(object arg) {
            await _rpc.NotifyWithParameterObjectAsync("OnCustomNotification", arg);
        }

        public async Task<string> SendServerCustomMessageAsync(string test) {
            return await _rpc.InvokeAsync<string>("OnCustomRequest", test);
        }
    }
}
