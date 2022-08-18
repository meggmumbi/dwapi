﻿using System;
using System.Collections.Generic;

using System.Linq;
using Dwapi.ExtractsManagement.Core.Interfaces.Reader.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.ExtractsManagement.Infrastructure.Tests.TestArtifacts;
using Dwapi.SettingsManagement.Core.Model;
using Dwapi.SettingsManagement.Infrastructure;
using Dwapi.SharedKernel.Model;
using Dwapi.SharedKernel.Utility;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Dwapi.ExtractsManagement.Infrastructure.Tests.Reader.Dwh
{
    [TestFixture]
    [Category("Dwh")]
    public class ExtractSourceReaderTests
    {
        private IDwhExtractSourceReader _reader;
        private List<Extract> _extracts;
        private DbProtocol _protocol;

        [OneTimeSetUp]
        public void Init()
        {
            TestInitializer.ClearDb();
            TestInitializer.SeedData(TestData.GenerateEmrSystems(TestInitializer.EmrConnectionString));
            _protocol = TestInitializer.Protocol;
            _extracts=TestInitializer.Extracts.Where(x => x.DocketId.IsSameAs("NDWH")).ToList();
        }

        [SetUp]
        public void SetUp()
        {
            _reader = TestInitializer.ServiceProvider.GetService<IDwhExtractSourceReader>();
        }

        [TestCase(nameof(PatientExtract))]
        [TestCase(nameof(PatientArtExtract))]
        [TestCase(nameof(PatientPharmacyExtract))]
        [TestCase(nameof(PatientStatusExtract))]
        [TestCase(nameof(PatientVisitExtract))]
        [TestCase("PatientLabExtract")]
        [TestCase("PatientBaselineExtract")]
        [TestCase(nameof(PatientAdverseEventExtract))]

        [TestCase(nameof(AllergiesChronicIllnessExtract))]
        [TestCase(nameof(ContactListingExtract))]
        [TestCase(nameof(DepressionScreeningExtract))]
        [TestCase(nameof(DrugAlcoholScreeningExtract))]
        [TestCase(nameof(EnhancedAdherenceCounsellingExtract))]
        [TestCase(nameof(GbvScreeningExtract))]
        [TestCase(nameof(IptExtract))]
        [TestCase(nameof(OtzExtract))]
        [TestCase(nameof(OvcExtract))]

        [TestCase(nameof(CovidExtract))]
        [TestCase(nameof(DefaulterTracingExtract))]
        public void should_Execute_Reader(string name)
        {
            var extract = _extracts.First(x => x.Name.IsSameAs(name));
            var reader = _reader.ExecuteReader(_protocol, extract).Result;
            Assert.NotNull(reader);
            reader.Read();
            Assert.NotNull(reader[0]);
            reader.Close();
        }
    }
}
