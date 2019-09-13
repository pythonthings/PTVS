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
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.PythonTools.TestAdapter {

    /// <summary>
    /// Note even though we specify  [DefaultExecutorUri(PythonConstants.UnitTestExecutorUriString)] we still get all .py source files
    /// from all testcontainers.  
    /// </summary>
    [FileExtension(".py")]
    [DefaultExecutorUri(PythonConstants.UnitTestExecutorUriString)]
    public class UnitTestDiscoverer : PythonTestDiscoverer {
        public UnitTestDiscoverer() : base(TestFrameworkType.UnitTest) {
        }
    }

    /// <summary>
    /// Note even though we specify  [DefaultExecutorUri(PythonConstants.PytestExecutorUriString)] we still get all .py source files
    /// from all testcontainers.  
    /// </summary>
    [FileExtension(".py")]
    [DefaultExecutorUri(PythonConstants.PytestExecutorUriString)]
    public class PytestTestDiscoverer : PythonTestDiscoverer {
        public PytestTestDiscoverer() : base(TestFrameworkType.Pytest) { 
        }
    }
    
    public abstract class PythonTestDiscoverer : ITestDiscoverer {
        private TestFrameworkType _frameworkType;

        /// <summary>
        /// Create a test framework specific Test Discoverer
        /// </summary>
        /// <param name="frameworkType"></param>
        protected PythonTestDiscoverer(TestFrameworkType frameworkType) {
            _frameworkType = frameworkType;
        }

        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink
        ) {
            if (sources == null) {
                throw new ArgumentNullException(nameof(sources));
            }

            if (discoverySink == null) {
                throw new ArgumentNullException(nameof(discoverySink));
            }

            var sourceToProjSettings = RunSettingsUtil.GetSourceToProjSettings(discoveryContext.RunSettings, filterType: _frameworkType);
            if (!sourceToProjSettings.Any()) {
                return;
            }

            foreach (var testGroup in sources.GroupBy(x => sourceToProjSettings.TryGetValue(x, out PythonProjectSettings project) ? project : null)) {
                DiscoverTestGroup(testGroup, discoveryContext, logger, discoverySink);
            }
        }

        private void DiscoverTestGroup(
            IGrouping<PythonProjectSettings, string> testGroup,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink
        ) {
            PythonProjectSettings settings = testGroup.Key;
            if (settings == null || settings.TestFramework != _frameworkType) {
                return;
            }

            try {
                var discovery = DiscovererFactory.GetDiscoverer(settings);
                discovery.DiscoverTests(testGroup, logger, discoverySink);
            } catch (Exception ex) {
                logger.SendMessage(TestMessageLevel.Error, ex.Message);
            }
        }
    }
}
