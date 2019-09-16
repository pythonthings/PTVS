﻿// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Parsing;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Microsoft.VisualStudioTools;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    /// <summary>
    /// Implementation of the language server client.
    /// </summary>
    /// <remarks>
    /// See documentation at https://docs.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension?view=vs-2019
    /// </remarks>
    //[ContentType(PythonCoreConstants.ContentType)]
    //[Export(typeof(ILanguageClient))]
    class PythonLanguageClient : ILanguageClient, ILanguageClientCustomMessage2, IDisposable {
        private readonly IServiceProvider _site;
        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly ILanguageClientBroker _broker;
        private readonly PythonProjectNode _project;
        private readonly IInteractiveWindow _replWindow;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private JsonRpc _rpc;
        private DisposableBag _disposables;

        private static readonly List<PythonLanguageClient> _languageClients = new List<PythonLanguageClient>();

        //[ImportingConstructor]
        //public PythonLanguageClient(
        //    [Import(typeof(SVsServiceProvider))] IServiceProvider site,
        //    [Import] IVsFolderWorkspaceService workspaceService,
        //    [Import] IInterpreterOptionsService optionsService,
        //    [Import] IInterpreterRegistryService registryService,
        //    [Import] ILanguageClientBroker broker
        //) : this(site, PythonCoreConstants.ContentType, workspaceService, optionsService, registryService, broker, null, null) {
        //}

        public PythonLanguageClient(
            IServiceProvider site,
            string contentTypeName,
            IVsFolderWorkspaceService workspaceService,
            IInterpreterOptionsService optionsService,
            IInterpreterRegistryService registryService,
            ILanguageClientBroker broker,
            PythonProjectNode project,
            IInteractiveWindow replWindow
        ) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            ContentTypeName = contentTypeName ?? throw new ArgumentNullException(nameof(contentTypeName));
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
            _optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _project = project;
            _replWindow = replWindow;
            _disposables = new DisposableBag(GetType().Name);
            _contentTypeRegistryService = site.GetComponentModel().GetService<IContentTypeRegistryService>();

            if (project != null) {
                project.LanguageServerRestart += OnProjectChanged;
                project.AddActionOnClose(this, OnProjectClosed);
            }

            _optionsService.DefaultInterpreterChanged += OnDefaultInterpreterChanged;
            _workspaceService.OnActiveWorkspaceChanged += OnActiveWorkspaceChanged;
            _disposables.Add(() => {
                _optionsService.DefaultInterpreterChanged -= OnDefaultInterpreterChanged;
                _workspaceService.OnActiveWorkspaceChanged -= OnActiveWorkspaceChanged;
                if (project != null) {
                    project.LanguageServerRestart -= OnProjectChanged;
                }
            });

            //MiddleLayer = new PythonLanguageClientMiddleLayer(null);
            CustomMessageTarget = new PythonLanguageClientCustomTarget(site);
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider serviceProvider,
            IInteractiveWindow replWindow,
            string contentTypeName
        ) {
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            var componentModel = serviceProvider.GetComponentModel();
            var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            var optionsService = componentModel.GetService<IInterpreterOptionsService>();
            var registryService = componentModel.GetService<IInterpreterRegistryService>();
            var broker = componentModel.GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(
                serviceProvider,
                workspaceService,
                optionsService,
                registryService,
                broker,
                contentTypeName,
                null,
                replWindow
            );
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider serviceProvider,
            PythonProjectNode project,
            string contentTypeName
        ) {
            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            var componentModel = serviceProvider.GetComponentModel();
            var workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            var optionsService = componentModel.GetService<IInterpreterOptionsService>();
            var registryService = componentModel.GetService<IInterpreterRegistryService>();
            var broker = componentModel.GetService<ILanguageClientBroker>();

            await EnsureLanguageClientAsync(
                serviceProvider,
                workspaceService,
                optionsService,
                registryService,
                broker,
                contentTypeName,
                project,
                null
            );
        }

        public static async Task EnsureLanguageClientAsync(
            IServiceProvider site,
            IVsFolderWorkspaceService workspaceService,
            IInterpreterOptionsService optionsService,
            IInterpreterRegistryService registryService,
            ILanguageClientBroker broker,
            string contentTypeName,
            PythonProjectNode project,
            IInteractiveWindow replWindow
        ) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            PythonLanguageClient client = null;
            lock (_languageClients) {
                if (!_languageClients.Any(lc => lc.ContentTypeName == contentTypeName)) {
                    client = new PythonLanguageClient(site, contentTypeName, workspaceService, optionsService, registryService, broker, project, replWindow);
                    _languageClients.Add(client);
                }
            }

            if (client != null) {
                await broker.LoadAsync(new PythonLanguageClientMetadata(null, contentTypeName), client);
            }
        }

        public static PythonLanguageClient FindLanguageClient(string contentTypeName) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            lock (_languageClients) {
                return _languageClients.SingleOrDefault(lc => lc.ContentTypeName == contentTypeName);
            }
        }

        public static PythonLanguageClient FindLanguageClient(ITextBuffer textBuffer) {
            if (textBuffer == null) {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            return FindLanguageClient(textBuffer.ContentType.TypeName);
        }

        public static void StopLanguageClient(string contentTypeName) {
            if (contentTypeName == null) {
                throw new ArgumentNullException(nameof(contentTypeName));
            }

            PythonLanguageClient client = null;
            lock (_languageClients) {
                client = _languageClients.SingleOrDefault(lc => lc.ContentTypeName == contentTypeName);
                if (client != null) {
                    _languageClients.Remove(client);
                }
            }

            if (client != null) {
                client.StopAsync?.Invoke(client, EventArgs.Empty);
            }
        }

        public string ContentTypeName { get; }

        public IPythonInterpreterFactory Factory { get; private set; }

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

            // Force initialization of python tools service by requesting it
            _site.GetPythonToolsService();

            var interpreterPath = string.Empty;
            var interpreterVersion = string.Empty;
            var searchPaths = new List<string>();
            string rootPath = null;

            if (_replWindow != null) {
                var evaluator = _replWindow.Evaluator as PythonCommonInteractiveEvaluator;
                if (_replWindow.Evaluator is SelectableReplEvaluator selEvaluator) {
                    evaluator = selEvaluator.Evaluator as PythonCommonInteractiveEvaluator;
                }

                if (evaluator != null) {
                    interpreterPath = evaluator.Configuration.Interpreter.InterpreterPath;
                    interpreterVersion = evaluator.LanguageVersion.ToVersion().ToString();
                    searchPaths.AddRange(evaluator.Configuration.SearchPaths);
                }
            } else if (_project != null) {
                Factory = _project.ActiveInterpreter;
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                    searchPaths.AddRange(_project._searchPaths.GetAbsoluteSearchPaths());
                    rootPath = _project.ProjectHome;
                }
            } else if (workspace != null) {
                Factory = workspace.GetInterpreterFactory(_registryService, _optionsService);
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                    // VSCode captures the python.exe env variables, uses PYTHONPATH to build this list
                    searchPaths.AddRange(workspace.GetAbsoluteSearchPaths());
                    rootPath = workspace.Location;
                }
            } else {
                Factory = _optionsService.DefaultInterpreter;
                if (Factory != null) {
                    interpreterPath = Factory.Configuration.InterpreterPath;
                    interpreterVersion = Factory.Configuration.Version.ToString();
                }
            }

            if (string.IsNullOrEmpty(interpreterPath) || string.IsNullOrEmpty(interpreterVersion)) {
                return;
            }

            InitializationOptions = new PythonInitializationOptions {
                // we need to read from the workspace settings in order to populate this correctly
                // (or from the project)
                interpreter = new PythonInitializationOptions.Interpreter {
                    properties = new PythonInitializationOptions.Interpreter.InterpreterProperties {
                        InterpreterPath = interpreterPath,
                        Version = interpreterVersion,
                        DatabasePath = PythonLanguageClientStartInfo.DatabaseFolderPath,
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
                },
                rootPathOverride = rootPath
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

        public void Dispose() {
            _disposables.TryDispose();
        }

        private void OnProjectChanged(object sender, EventArgs e) {
            var project = _project;
            _site.GetUIThread().InvokeTaskSync(async () => {
                PythonLanguageClient.StopLanguageClient(ContentTypeName);
                await PythonLanguageClient.EnsureLanguageClientAsync(
                    _site,
                    project,
                    ContentTypeName
                );
            }, CancellationToken.None);
        }

        private void OnProjectClosed(object key) {
            PythonLanguageClient.StopLanguageClient(
                PythonFilePathToContentTypeProvider.GetContentTypeNameForProject(_project)
            );
        }

        private void OnDefaultInterpreterChanged(object sender, EventArgs e) {
            if (_optionsService.DefaultInterpreter == Factory) {
                return;
            }

            if (_project != null) {
                // This event happens while loading the project and needs to be ignored.
                // Project will send a restart event if its effective factory changes
                // when the global default is changed so no need to do anything here.
                return;
            }

            StopLanguageClient(ContentTypeName);
            EnsureLanguageClientAsync(
                _site,
                _workspaceService,
                _optionsService,
                _registryService,
                _broker,
                ContentTypeName,
                _project,
                _replWindow
            ).HandleAllExceptions(_site, GetType()).DoNotWait();
        }

        private Task OnActiveWorkspaceChanged(object sender, EventArgs e) {
            // TODO: determine if we need to stop this language server client
            // we also need to restart language server when things like search path, active interpreter are changing
            return Task.CompletedTask;
        }
    }
}
