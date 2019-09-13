﻿using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace Microsoft.PythonTools.TestAdapter.Config {
    public class RunSettingsUtil {
        public static Dictionary<string, PythonProjectSettings> GetSourceToProjSettings(IRunSettings settings, TestFrameworkType filterType) {
            var doc = Read(settings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/TestCases/Project");
            var res = new Dictionary<string, PythonProjectSettings>(StringComparer.OrdinalIgnoreCase);

            foreach (XPathNavigator project in nodes) {
                
                PythonProjectSettings projSettings = new PythonProjectSettings(
                    project.GetAttribute("name", ""),
                    project.GetAttribute("home", ""),
                    project.GetAttribute("workingDir", ""),
                    project.GetAttribute("interpreter", ""),
                    project.GetAttribute("pathEnv", ""),
                    project.GetAttribute("nativeDebugging", "").IsTrue(),
                    project.GetAttribute("isWorkspace", "").IsTrue(),
                    project.GetAttribute("useLegacyDebugger", "").IsTrue(),
                    project.GetAttribute("testFramework", ""),
                    project.GetAttribute("unitTestPattern", ""),
                    project.GetAttribute("unitTestRootDir", ""),
                    project.GetAttribute("discoveryWaitTime", "")
                ); 

                if (projSettings.TestFramework != filterType) {
                    continue;
                }

                foreach (XPathNavigator environment in project.Select("Environment/Variable")) {
                    projSettings.Environment[environment.GetAttribute("name", "")] = environment.GetAttribute("value", "");
                }

                string djangoSettings = project.GetAttribute("djangoSettingsModule", "");
                if (!String.IsNullOrWhiteSpace(djangoSettings)) {
                    projSettings.Environment["DJANGO_SETTINGS_MODULE"] = djangoSettings;
                }

                foreach (XPathNavigator searchPath in project.Select("SearchPaths/Search")) {
                    projSettings.SearchPath.Add(searchPath.GetAttribute("value", ""));
                }

                foreach (XPathNavigator test in project.Select("Test")) {
                    string testFile = test.GetAttribute("file", "");
                    projSettings.TestContainerSources.Add(testFile, testFile);
                    res[testFile] = projSettings;
                }
            }
            return res;
        }

        public static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StringReader(xml), settings));
        }
    }
}
