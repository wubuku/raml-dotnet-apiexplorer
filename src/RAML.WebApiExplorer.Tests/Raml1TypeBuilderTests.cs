﻿using System.Collections.Generic;
using FstabExplorerTest.Fstab.Models;
using NUnit.Framework;
using RAML.WebApiExplorer.Tests.Types;

namespace RAML.WebApiExplorer.Tests
{
	[TestFixture]
    public class Raml1TypeBuilderTests
	{
        private Raml1TypeBuilder raml1TypeBuilder;
	    private RamlTypesOrderedDictionary raml1Types;

	    [SetUp]
	    public void TestSetup()
	    {
            raml1Types = new RamlTypesOrderedDictionary();
            raml1TypeBuilder = new Raml1TypeBuilder(raml1Types);
	    }

	    [Test]
		public void ShouldParseTypeWithNestedTypes()
		{
			var raml1Type = raml1TypeBuilder.Add(typeof (ForksPostResponse));
            Assert.IsNotNullOrEmpty(raml1Type);
            Assert.IsTrue(raml1Types.ContainsKey("ForksPostResponse"));
            Assert.IsTrue(raml1Types.ContainsKey("Owner"));
		}

        [Test]
        public void ShouldParseUriAsString()
        {
            raml1TypeBuilder.Add(typeof(WebLocation));
            Assert.AreEqual(1, raml1Types.Count);
            Assert.IsTrue(!raml1Types.ContainsKey("Uri"));
            Assert.AreEqual(2, raml1Types.GetByKey("WebLocation").Object.Properties.Count);
            Assert.IsTrue(raml1Types.GetByKey("WebLocation").Object.Properties.ContainsKey("BaseUri"));
            Assert.IsTrue(raml1Types.GetByKey("WebLocation").Object.Properties.ContainsKey("Location"));
        }

		[Test]
		public void ShouldSkipTypeThatHasNoSettableProperties()
        {
            raml1TypeBuilder.Add(typeof(TypeWithReadOnlyProperties));
            Assert.AreEqual(1, raml1Types.Count);
            Assert.IsFalse(raml1Types.GetByKey("TypeWithReadOnlyProperties").Object.Properties.ContainsKey("ReadOnlyProp"));
            Assert.IsFalse(raml1Types.GetByKey("TypeWithReadOnlyProperties").Object.Properties.ContainsKey("ReadOnlyObject"));
		}

		[Test]
		public void ShouldParseArray()
		{
			var raml1Type = raml1TypeBuilder.Add(typeof(Owner[]));
            Assert.AreEqual(2, raml1Types.Count);
			Assert.IsTrue(raml1Types.ContainsKey("Owner"));
            Assert.AreEqual("Owner", raml1Types.GetByKey("ListOfOwner").Array.Items.Type);
		}

        [Test]
        public void ShouldParseArrayOfPrimitives()
        {
            raml1TypeBuilder.Add(typeof(int[]));
            Assert.AreEqual(1, raml1Types.Count);
            Assert.AreEqual("integer", raml1Types.GetByKey("ListOfInt32").Array.Items.Type);
        }

        [Test]
        public void ShouldParseListOfPrimitives()
        {
            raml1TypeBuilder.Add(typeof(List<int>));
            Assert.AreEqual(1, raml1Types.Count);
            Assert.AreEqual("integer", raml1Types.GetByKey("ListOfInt32").Array.Items.Type);
        }

        [Test]
        public void ShouldParseArrayOfArray()
        {
            raml1TypeBuilder.Add(typeof(List<int[]>));
            Assert.AreEqual(1, raml1Types.Count);
            Assert.AreEqual("integer", raml1Types.GetByKey("ListOfListOfInt32").Array.Items.Array.Items.Type);
        }

        [Test]
        public void ShouldParseDictionary()
        {
            raml1TypeBuilder.Add(typeof(IDictionary<string, Owner>));
            Assert.AreEqual(2, raml1Types.Count);
            Assert.IsTrue(raml1Types.ContainsKey("Owner"));
            Assert.IsTrue(raml1Types.ContainsKey("OwnerMap"));
            Assert.IsTrue(raml1Types.GetByKey("OwnerMap").Object.Properties.ContainsKey("[]"));
            Assert.AreEqual("Owner", raml1Types.GetByKey("OwnerMap").Object.Properties["[]"].Type);
        }

		[Test]
		public void ShouldParseComplexType()
		{
			raml1TypeBuilder.Add(typeof(SearchGetResponse));
            Assert.AreEqual(6, raml1Types.Count);
		}

        [Test, Ignore] // it should not be necesary
        public void ShouldGetSubclassesOfType()
        {
            raml1TypeBuilder.Add(typeof(Entry));
            Assert.AreEqual(6, raml1Types.Count);
            Assert.AreEqual("Storage", raml1Types.GetByKey("StoragediskUUID").Type);
        }

        [Test]
        public void ShouldParseTypeWithRecursiveTypes()
        {
            raml1TypeBuilder.Add(typeof(Employee));
            Assert.AreEqual(2, raml1Types.Count);
            Assert.AreEqual("Person", raml1Types.GetByKey("Employee").Type);
        }

        [Test]
        public void ShouldGenerateAnnotations()
        {
            raml1TypeBuilder.Add(typeof(AnnotatedObject));
            var raml1Type = raml1Types.GetByKey("AnnotatedObject");
            Assert.AreEqual(18.00, raml1Type.Object.Properties["Age"].Scalar.Minimum);
            Assert.AreEqual(120.00, raml1Type.Object.Properties["Age"].Scalar.Maximum);
            Assert.AreEqual(20.50, raml1Type.Object.Properties["Weight"].Scalar.Minimum);
            Assert.AreEqual(300.50, raml1Type.Object.Properties["Weight"].Scalar.Maximum);
            Assert.AreEqual(255, raml1Type.Object.Properties["City"].Scalar.MaxLength);
            Assert.AreEqual(2, raml1Type.Object.Properties["State"].Scalar.MinLength);
        }
    }
}
