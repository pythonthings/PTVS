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
using System.Linq;
using System.Runtime.InteropServices;
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
    class PythonLanguageClientDocumentTracker : IVsRunningDocTableEvents, IDisposable {
        private IServiceProvider _site;
        private IVsRunningDocumentTable _runDocTable;
        private uint _runDocTableEventsCookie;
        private IVsEditorAdaptersFactoryService _editorAdapterFactoryService;
        private IVsFolderWorkspaceService _workspaceService;
        private IInterpreterOptionsService _optionsService;
        private IInterpreterRegistryService _registryService;
        private ILanguageClientBroker _broker;

        public PythonLanguageClientDocumentTracker() {
        }

        public void Dispose() {
            if (_site != null) {
                _runDocTable.UnadviseRunningDocTableEvents(_runDocTableEventsCookie);
            }
        }

        public void Initialize(IServiceProvider site) {
            _site = site;

            _runDocTable = (IVsRunningDocumentTable)_site.GetService(typeof(SVsRunningDocumentTable));
            _runDocTable.AdviseRunningDocTableEvents(this, out _runDocTableEventsCookie);

            var componentModel = (IComponentModel)site.GetService(typeof(SComponentModel));
            _editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            _workspaceService = componentModel.GetService<IVsFolderWorkspaceService>();
            _optionsService = componentModel.GetService<IInterpreterOptionsService>();
            _registryService = componentModel.GetService<IInterpreterRegistryService>();
            _broker = componentModel.GetService<ILanguageClientBroker>();

            var names = HandleLoadedDocuments();
            foreach (var name in names) {
                EnsureLanguageClient(name);
            }
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
            if (fFirstShow != 0) {
                var name = HandleDocument(docCookie);
                if (!string.IsNullOrEmpty(name)) {
                    EnsureLanguageClient(name);
                }
            }

            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
            return VSConstants.S_OK;
        }

        private string[] HandleLoadedDocuments() {
            var names = new HashSet<string>();

            if (ErrorHandler.Succeeded(_runDocTable.GetRunningDocumentsEnum(out var pEnumRdt))) {
                if (ErrorHandler.Succeeded(pEnumRdt.Reset())) {
                    uint[] cookie = new uint[1];
                    while (VSConstants.S_OK == pEnumRdt.Next(1, cookie, out _)) {
                        var name = HandleDocument(cookie[0]);
                        if (!string.IsNullOrEmpty(name)) {
                            names.Add(name);
                        }
                    }
                }
            }

            return names.ToArray();
        }

        private string HandleDocument(uint docCookie) {
            string name = null;

            var res = _runDocTable.GetDocumentInfo(docCookie, out _, out _, out _, out var path, out var hier, out var item, out var docDataPtr);
            if (res == VSConstants.S_OK) {
                try {
                    if (docDataPtr != IntPtr.Zero) {
                        var obj = Marshal.GetObjectForIUnknown(docDataPtr);
                        var vsTextBuffer = obj as IVsTextBuffer;
                        var textBuffer = obj as ITextBuffer;
                        if (textBuffer == null && vsTextBuffer != null) {
                            textBuffer = _editorAdapterFactoryService.GetDocumentBuffer(vsTextBuffer);
                        }

                        if (textBuffer != null && hier != null) {
                            var contentType = textBuffer.ContentType;
                            if (contentType.IsOfType(PythonCoreConstants.ContentType)) {
                                if (!textBuffer.Properties.TryGetProperty(LanguageClientConstants.ClientNamePropertyKey, out name)) {
                                    name = hier.GetNameProperty();
                                    textBuffer.Properties.AddProperty(LanguageClientConstants.ClientNamePropertyKey, name);
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

            return name;
        }

        private void EnsureLanguageClient(string name) {
            PythonLanguageClient.EnsureLanguageClient(
                _site,
                _workspaceService,
                _optionsService,
                _registryService,
                _broker,
                name
            ).HandleAllExceptions(_site, GetType()).DoNotWait();
        }
    }
}
