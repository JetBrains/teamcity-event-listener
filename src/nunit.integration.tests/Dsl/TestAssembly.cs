﻿namespace nunit.integration.tests.Dsl
{
    using System;
    using System.Collections.Generic;

    using Microsoft.CodeAnalysis;

    internal class TestAssembly
    {
        private readonly string _assemblyName;
        private readonly Dictionary<string, TestClass> _classes = new Dictionary<string, TestClass>(StringComparer.InvariantCultureIgnoreCase);
        private readonly List<string> _reference = new List<string>();
        private readonly List<string> _attributes = new List<string>();

        public TestAssembly(string assemblyName)
        {
            Platform = Platform.AnyCpu;
            _assemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        }

        public IEnumerable<TestClass> Classes => _classes.Values;

        public IEnumerable<string> References => _reference;

        public IEnumerable<string> Attributes => _attributes;

        public Platform Platform { get; set; }

        public TestClass GetOrCreateClass(string namespaceName, string className)
        {
            if (namespaceName == null) throw new ArgumentNullException(nameof(namespaceName));
            if (className == null) throw new ArgumentNullException(nameof(className));
            TestClass testClass;
            var key = $"{namespaceName}.{className}";
            if (!_classes.TryGetValue(key, out testClass))
            {
                _classes[key] = testClass = new TestClass(namespaceName, className);
            }

            return testClass;
        }

        public void AddReference(string assemblyName)
        {
            if (assemblyName == null) throw new ArgumentNullException(nameof(assemblyName));
            _reference.Add(assemblyName);
        }

        public void AddAttribute(string attribute)
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));
            _attributes.Add(attribute);
        }
    }
}
