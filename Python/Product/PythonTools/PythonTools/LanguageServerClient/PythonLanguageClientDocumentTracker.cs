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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;

namespace Microsoft.PythonTools.LanguageServerClient {
    class PythonLanguageClientDocumentTracker {
        private IServiceProvider _site;
        private RunDocTableEvents _runDocTableEvents;

        public PythonLanguageClientDocumentTracker() {
        }

        public void Initialize(IServiceProvider site) {
            _site = site;
         
            var docTable = (IVsRunningDocumentTable)_site.GetService(typeof(SVsRunningDocumentTable));
            _runDocTableEvents = new RunDocTableEvents(_site);

            docTable.AdviseRunningDocTableEvents(_runDocTableEvents, out uint cookie);

            var componentModel = (IComponentModel)_site.GetService(typeof(SComponentModel));
            var remoteBroker = componentModel.GetService<ILanguageClientBroker>();
            var textFactory = componentModel.GetService<ITextBufferFactoryService>();

            textFactory.TextBufferCreated += OnTextFactoryTextBufferCreated;

            //var solution = (IVsSolution)await this.GetServiceAsync(typeof(SVsSolution));
            //if (solution != null) {
            //    var emptyGuid = Guid.Empty;
            //    IEnumHierarchies hierarchiesEnum;
            //    if (ErrorHandler.Succeeded(solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref emptyGuid, out hierarchiesEnum)) && hierarchiesEnum != null) {
            //        var hierarchies = new List<IVsHierarchy>();
            //        IVsHierarchy[] projectEnumHierarchies = new IVsHierarchy[1];
            //        while (hierarchiesEnum.Next(1, projectEnumHierarchies, out uint fetched) == 0 && fetched > 0) {
            //            foreach (var client in languageClients) {
            //                // Consider the overriding attribute for LiveShare clients.
            //                var projectMetadata = client.Metadata as IAppliesToProjectMetadata;
            //                if (projectEnumHierarchies[0].IsCapabilityMatch(projectMetadata.AppliesToProjects)) {
            //                    await remoteBroker.LoadAsync(client.Metadata, client.Value);
            //                }
            //            }
            //        }
            //    }
            //}
        }

        private void OnTextFactoryTextBufferCreated(object sender, TextBufferCreatedEventArgs e) {
            var path = e.TextBuffer.GetFilePath();
            Trace.WriteLine(path);
        }

        class RunDocTableEvents : IVsRunningDocTableEvents {
            private readonly IVsRunningDocumentTable _docTable;
            private readonly IVsEditorAdaptersFactoryService _editorAdapterFactoryService;

            private readonly IServiceProvider _site;
            private readonly IVsFolderWorkspaceService _workspaceService;
            private readonly IInterpreterOptionsService _optionsService;
            private readonly IInterpreterRegistryService _registryService;
            private readonly ILanguageClientBroker _broker;

            public RunDocTableEvents(IServiceProvider site) {
                _site = site;
                _docTable = (IVsRunningDocumentTable)site.GetService(typeof(SVsRunningDocumentTable));
                var componentModel = (IComponentModel)site.GetService(typeof(SComponentModel));
                _editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
                _workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
                _optionsService = componentModel.GetService<IInterpreterOptionsService>();
                _registryService = componentModel.GetService<IInterpreterRegistryService>();
                _broker = componentModel.GetService<ILanguageClientBroker>();
            }

            public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
                return VSConstants.S_OK;
            }

            public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
                return VSConstants.S_OK;
            }

            public int OnAfterSave(uint docCookie) {
                return VSConstants.S_OK;
            }

            public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) {
                return VSConstants.S_OK;
            }

            public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) {
                var res = _docTable.GetDocumentInfo(docCookie, out _, out _, out _, out var path, out var hier, out var item, out var docDataPtr);
                if (res == VSConstants.S_OK) {
                    try {
                        if (docDataPtr != null) {
                            var obj = Marshal.GetObjectForIUnknown(docDataPtr);
                            var vsTextBuffer = obj as IVsTextBuffer;
                            ITextBuffer textBuffer = obj as ITextBuffer;
                            if (textBuffer == null) {
                                textBuffer = _editorAdapterFactoryService.GetDocumentBuffer(vsTextBuffer);
                            }

                            if (textBuffer != null && hier != null) {
                                var contentType = textBuffer.ContentType;
                                if (contentType.IsOfType(PythonCoreConstants.ContentType)) {
                                    if (!textBuffer.Properties.TryGetProperty(LanguageClientConstants.ClientNamePropertyKey, out string name)) {
                                        name = hier.GetNameProperty();

                                        textBuffer.Properties.AddProperty(LanguageClientConstants.ClientNamePropertyKey, name);

                                        //PythonLanguageClient.EnsureLanguageClient(
                                        //    _site,
                                        //    _workspaceService,
                                        //    _optionsService,
                                        //    _registryService,
                                        //    _broker,
                                        //    name
                                        //).HandleAllExceptions(_site, GetType()).DoNotWait();
                                    }
                                }
                            }
                        }
                    } finally {
                        if (docDataPtr != IntPtr.Zero) {
                            Marshal.Release(docDataPtr);
                        }
                    }
                }

                return VSConstants.S_OK;
            }

            public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
                return VSConstants.S_OK;
            }
        }

    }
}
