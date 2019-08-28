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
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LanguageServerClient {
    //[Export(typeof(IWpfTextViewConnectionListener))]
    //[ContentType(PythonCoreConstants.ContentType)]
    //[TextViewRole(PredefinedTextViewRoles.Document)]
    class WpfTextViewConnectionListener : IWpfTextViewConnectionListener, IDisposable {
        internal const string BufferContentChangedSubscriptionCount = "Python_ContentChangedSubscriptionCount";
        public const string DocumentPathProperty = "PythonDocumentPath";

        private readonly IServiceProvider _site;
        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly ILanguageClientBroker _broker;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        public WpfTextViewConnectionListener(
            [Import(typeof(SVsServiceProvider))] IServiceProvider site,
            [Import] IVsFolderWorkspaceService workspaceService,
            [Import] IInterpreterOptionsService optionsService,
            [Import] IInterpreterRegistryService registryService,
            [Import] ILanguageClientBroker broker,
            [Import] ITextDocumentFactoryService textDocumentFactoryService
        ) {
            _site = site;
            _workspaceService = workspaceService;
            _optionsService = optionsService;
            _registryService = registryService;
            _broker = broker;
            _textDocumentFactoryService = textDocumentFactoryService;
            _joinableTaskFactory = ThreadHelper.JoinableTaskFactory;
        }

        public void Dispose() {
        }

        public void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers) {
            if (textView != null) {
                // We only want to subscribe to the TextDataModel.DocumentBuffer changed event since that buffer represents buffer for file on disk changing.
                // However, since this method is called mutliple times when different buffers are connected to a text view, we want to make sure we only subscribe
                // to the Changed event once.  The way to do it is use an internal ref count.  We only subscribe when the count is 0, and incremenet the ref count
                // every time a subject buffer connects.  We decrement every time a buffer disconnects and only unsubscribe when the count hits 0.
                if (!textView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(BufferContentChangedSubscriptionCount, out int count) || count == 0) {
                    var buffer = textView.TextDataModel.DocumentBuffer;
                    _joinableTaskFactory.RunAsync(async () => {
                        if (!IsCustomBufferContentType(buffer.ContentType.TypeName) &&
                            _textDocumentFactoryService.TryGetTextDocument(buffer, out ITextDocument document)) {
                            document.FileActionOccurred += Document_FileActionOccurred;
                            buffer.ContentTypeChanged += this.OnContentTypeChanged;

                            string clientName = null;
                            if (textView.TextBuffer != null && textView.TextBuffer.Properties != null) {
                                textView.TextBuffer.Properties.TryGetProperty(LanguageClientConstants.ClientNamePropertyKey, out clientName);
                            }
                            await this.NotifyDocumentOpenedAsync(buffer.ContentType, buffer.CurrentSnapshot, clientName);

                            //this.InitializeTextViewCommandsAndNavBar(textView);
                        }
                    }).Task.HandleAllExceptions(_site, GetType()).DoNotWait(); //.SafeFileAndForget(TelemetryConstants.DocumentOpenedFailedEventName);

                    var buffer2 = textView.TextDataModel.DocumentBuffer as ITextBuffer2;
                    if (buffer2 != null) {
                        buffer2.ChangedOnBackground += this.OnBufferChanged;
                    }
                }

                textView.TextDataModel.DocumentBuffer.Properties[BufferContentChangedSubscriptionCount] = ++count;
            }
        }

        public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers) {
            if (textView != null) {
                if (textView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(BufferContentChangedSubscriptionCount, out int count)) {
                    count--;
                    if (count == 0) {
                        _joinableTaskFactory.RunAsync(async () => {
                            var buffer = textView.TextDataModel.DocumentBuffer;
                            if (buffer.Properties.TryGetProperty<string>(DocumentPathProperty, out string filePath)) {
                                buffer.ContentTypeChanged -= this.OnContentTypeChanged;

                                //if (!IsCustomBufferContentType(buffer.ContentType.TypeName)) {
                                //    await this.broker.OnDidCloseTextDocumentAsync(buffer.CurrentSnapshot);
                                //}
                            }
                        }).Task.HandleAllExceptions(_site, GetType()).DoNotWait(); //.SafeFileAndForget(TelemetryConstants.DocumentClosedFailedEventName);

                        var buffer2 = textView.TextDataModel.DocumentBuffer as ITextBuffer2;
                        if (buffer2 != null) {
                            buffer2.ChangedOnBackground -= this.OnBufferChanged;
                        }
                    }

                    textView.TextDataModel.DocumentBuffer.Properties[BufferContentChangedSubscriptionCount] = count;
                }
            }
        }

        private async Task NotifyDocumentOpenedAsync(IContentType contentType, ITextSnapshot snapshot, string clientName) {
            var path = snapshot.TextBuffer.GetFilePath();
            Trace.WriteLine(path);

            if (path != null) {
                if (clientName == null) {
                    var proj = _site.GetProjectFromOpenFile(path);
                    if (proj != null) {
                        clientName = proj.Name;
                    } else if (_workspaceService.CurrentWorkspace != null) {
                        clientName = _workspaceService.CurrentWorkspace.GetName();
                    }

                    if (clientName != null) {
                        snapshot.TextBuffer.Properties.AddProperty(LanguageClientConstants.ClientNamePropertyKey, clientName);

                        //await PythonLanguageClient.EnsureLanguageClient(
                        //    _site,
                        //    _workspaceService,
                        //    _optionsService,
                        //    _registryService,
                        //    _broker,
                        //    clientName
                        //);
                    }
                }
            }



            //            if (this.languageClientsHelper != null) {
            //                var languageClients = new List<Lazy<ILanguageClient, IContentTypeMetadata>>();
            //                foreach (var lc in this.languageClientsHelper.Items) {
            //                    if (lc.Metadata is LanguageClientMetadata lcMetadata) {
            //                        if ((lcMetadata.ContentTypes.Any(c => contentType.IsOfType(c)) || lcMetadata.ContentTypes.Contains("any")) &&
            //                            string.Equals(lcMetadata.ClientName, clientName, StringComparison.OrdinalIgnoreCase)) {
            //                            languageClients.Add(lc);
            //                        }
            //                    }
            //                }

            //                Func<Lazy<ILanguageClient, IContentTypeMetadata>, bool> isOverriding = lazy => {
            //                    var languageClientMetadata = lazy.Metadata as IIsOverridingMetadata;
            //                    if (languageClientMetadata != null) {
            //                        return languageClientMetadata.IsOverriding;
            //                    }

            //                    // TODO: Remove this block after LiveShare converts to using IsOverridingAttribute.
            //                    var priority = lazy.Value as ILanguageClientPriority;
            //                    if (priority != null) {
            //#pragma warning disable CS0618 // Type or member is obsolete
            //                        return priority.IsOverriding;
            //#pragma warning restore CS0618 // Type or member is obsolete
            //                    }

            //                    return false;
            //                };

            //                var nonOverridingClients = languageClients.Where(i => !isOverriding(i)).ToArray();
            //                foreach (var lc in nonOverridingClients) {
            //                    var lcContentTypes = new HashSet<string>(lc.Metadata.ContentTypes);

            //                    // find override language if exists
            //                    if (languageClients.Any(i => isOverriding(i) && lcContentTypes.IsSubsetOf(i.Metadata.ContentTypes))) {
            //                        languageClients.Remove(lc);
            //                    }
            //                }

            //                foreach (var languageClient in languageClients) {
            //                    await this.broker.LoadAsync((ILanguageClientMetadata)languageClient.Metadata, languageClient.Value);
            //                }

            //                await this.broker.OnDidOpenTextDocumentAsync(snapshot);

            //                ImmutableInterlocked.Update(ref this.documentsOpened, l => l.Add(snapshot.TextBuffer));

            //                await this.FlushChangesPendingAsync(snapshot.TextBuffer);
            //            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs args) {
            //var currentDocumentsOpened = this.documentsOpened;
            //var buffer = sender as ITextBuffer;
            //if (buffer != null && !IsCustomBufferContentType(buffer.ContentType.TypeName)) {
            //    if (currentDocumentsOpened.Contains(buffer)) {
            //        // If the corresponding document has been opened in an editor already, that means it would be recorded in this.documentsOpened list.
            //        // In this case, we can just send the document changed event over to the server.
            //        this.broker.JoinableTaskFactory.RunAsync(async () => {
            //            await this.broker.OnDidChangeTextDocumentAsync(args.Before, args.After, args.Changes);
            //        }).SafeFileAndForget(TelemetryConstants.DocumentChangedFailedEventName);
            //    } else {
            //        // If we can't find a corresponding document in this.documentsOpened that means we haven't activated the server yet.
            //        // In this case, we will hold onto the changes and send them out when the server is activated.
            //        lock (this.contentChangeLock) {
            //            List<TextContentChangedEventArgs> changes;
            //            if (this.changesPending.TryGetValue(buffer, out changes)) {
            //                changes.Add(args);
            //            } else {
            //                changes = new List<TextContentChangedEventArgs>() { args };
            //                this.changesPending.Add(buffer, changes);
            //            }
            //        }
            //    }
            //}
        }

        private bool IsCustomBufferContentType(string contentType) {
            //var languageServiceBroker = this.broker as RemoteLanguageServiceBroker;
            //if (languageServiceBroker != null) {
            //    return languageServiceBroker.CustomBufferContentTypes.Contains(contentType);
            //}

            return false;
        }

        private void Document_FileActionOccurred(object sender, TextDocumentFileActionEventArgs e) {
            if (e.FileActionType == FileActionTypes.ContentSavedToDisk && sender is ITextDocument textDocument &&
                    !IsCustomBufferContentType(textDocument.TextBuffer.ContentType.TypeName)) {
                //this.broker.JoinableTaskFactory.RunAsync(async () => {
                //    await this.broker.OnDidSaveTextDocumentAsync(textDocument);
                //}).SafeFileAndForget(TelemetryConstants.DocumentSavedFailedEventName);
            }
        }

        private void OnContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            if (IsCustomBufferContentType(e.AfterContentType.TypeName)) {
                return;
            }

            //_joinableTaskFactory.RunAsync(async () => {
            //    await this.broker.OnDidCloseTextDocumentAsync(e.Before);

            //    e.After.TextBuffer.Properties.TryGetProperty(LanguageClientConstants.ClientNamePropertyKey, out string clientName);
            //    await this.NotifyDocumentOpenedAsync(e.AfterContentType, e.After.TextBuffer.CurrentSnapshot, clientName);
            //}).SafeFileAndForget(TelemetryConstants.DocumentContentTypeChangedFailedEventName);
        }
    }
}
