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
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Core;
using Microsoft.VisualStudio.Shell;

[assembly: AssemblyTitle("Visual Studio - Python support")]
[assembly: AssemblyDescription("Provides Python support within Visual Studio.")]

[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

[assembly: ProvideRawCodeBase(
    AssemblyName = "Microsoft.Python.Analysis", CodeBase = @"$PackageFolder$\LanguageServer\x86\Microsoft.Python.Analysis.dll",
    Version = LSConstants.Version, Culture = LSConstants.Culture, PublicKeyToken = LSConstants.PublicKeyToken
)]

[assembly: ProvideRawCodeBase(
    AssemblyName = "Microsoft.Python.Analysis.Core", CodeBase = @"$PackageFolder$\LanguageServer\x86\Microsoft.Python.Analysis.Core.dll",
    Version = LSConstants.Version, Culture = LSConstants.Culture, PublicKeyToken = LSConstants.PublicKeyToken
)]

[assembly: ProvideRawCodeBase(
    AssemblyName = "Microsoft.Python.Core", CodeBase = @"$PackageFolder$\LanguageServer\x86\Microsoft.Python.Core.dll",
    Version = LSConstants.Version, Culture = LSConstants.Culture, PublicKeyToken = LSConstants.PublicKeyToken
)]

[assembly: ProvideRawCodeBase(
    AssemblyName = "Microsoft.Python.LanguageServer", CodeBase = @"$PackageFolder$\LanguageServer\x86\Microsoft.Python.LanguageServer.dll",
    Version = LSConstants.Version, Culture = LSConstants.Culture, PublicKeyToken = LSConstants.PublicKeyToken
)]

[assembly: ProvideRawCodeBase(
    AssemblyName = "Microsoft.Python.Parsing", CodeBase = @"$PackageFolder$\LanguageServer\x86\Microsoft.Python.Parsing.dll",
    Version = LSConstants.Version, Culture = LSConstants.Culture, PublicKeyToken = LSConstants.PublicKeyToken
)]

// LSC - can't use these on build machine for unknown reason (it fails to load the assembly when registering)
//[assembly: ProvideCodeBase(AssemblyName = "Microsoft.Python.Analysis", CodeBase = @"LanguageServer\x86\Microsoft.Python.Analysis.dll")]
//[assembly: ProvideCodeBase(AssemblyName = "Microsoft.Python.Analysis.Core", CodeBase = @"LanguageServer\x86\Microsoft.Python.Analysis.Core.dll")]
//[assembly: ProvideCodeBase(AssemblyName = "Microsoft.Python.Core", CodeBase = @"LanguageServer\x86\Microsoft.Python.Core.dll")]
//[assembly: ProvideCodeBase(AssemblyName = "Microsoft.Python.LanguageServer", CodeBase = @"LanguageServer\x86\Microsoft.Python.LanguageServer.dll")]
//[assembly: ProvideCodeBase(AssemblyName = "Microsoft.Python.Parsing", CodeBase = @"LanguageServer\x86\Microsoft.Python.Parsing.dll")]

[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools", CodeBase = "Microsoft.PythonTools.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Attacher", CodeBase = "Microsoft.PythonTools.Attacher.exe", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.AttacherX86", CodeBase = "Microsoft.PythonTools.AttacherX86.exe", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Common", CodeBase = "Microsoft.PythonTools.Common.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.EnvironmentsList", CodeBase = "Microsoft.PythonTools.EnvironmentsList.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Debugger", CodeBase = "Microsoft.PythonTools.Debugger.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Debugger.Concord", CodeBase = "Microsoft.PythonTools.Debugger.Concord.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Ipc.Json", CodeBase = "Microsoft.PythonTools.Ipc.Json.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.ProjectWizards", CodeBase = "Microsoft.PythonTools.ProjectWizards.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.VSCommon", CodeBase = "Microsoft.PythonTools.VSCommon.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.VSInterpreters", CodeBase = "Microsoft.PythonTools.VSInterpreters.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Workspace", CodeBase = "Microsoft.PythonTools.Workspace.dll", Version = AssemblyVersionInfo.StableVersion)]

internal static class LSConstants {
    // important: keep in sync with Build\PreBuild.ps1
    public const string Version = "0.4.11.0";
    public const string Culture = "neutral";
    public const string PublicKeyToken = "b03f5f7f11d50a3a";
}
