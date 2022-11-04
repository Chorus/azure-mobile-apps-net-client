#nullable enable annotations
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;
using System;
using Xunit;
using Moq;
using FluentAssertions;
using static FluentAssertions.FluentActions;
using System.Linq;

namespace MobileClient.Tests.Table.Sync
{
    public class MobileServiceUpdateOperationErrorTests
    {
        [Fact]
        public void WhenIdenticalItems_ThenNoConflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");
            var remote = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");
            var @base = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().BeEmpty();
        }
        [Fact]
        public void WhenRemoteDifferentFromBase_ThenNoConflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");
            var remote = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abcdef""}");
            var @base = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenLocalDifferentFromBase_ThenNoConflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abcdef""}");
            var remote = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");
            var @base = (JObject)JToken.Parse(@"{""Property1"":0, ""Property2"": ""abc""}");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenLocalProperty2ChangedAndRemoteProperty1Changed_Then2Conflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(@"{""Property1"": 0, ""Property2"": ""abcdef""}");
            var remote = (JObject)JToken.Parse(@"{""Property1"": 1, ""Property2"": ""abc""}");
            var @base = (JObject)JToken.Parse(@"{""Property1"": 0, ""Property2"": ""abc""}");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().HaveCount(2).And
                .ContainEquivalentOf(new { PropertyName = "Property1", IsLocalChanged = false, IsRemoteChanged = true }).And
                .ContainEquivalentOf(new { PropertyName = "Property2", IsLocalChanged = true, IsRemoteChanged = false });
        }

        private MobileServiceUpdateOperationError CreateSut(JObject? local, JObject? remote, JObject? @base)
        {
            return new MobileServiceUpdateOperationError(
                "id",
                1,
                MobileServiceTableOperationKind.Update,
                System.Net.HttpStatusCode.PreconditionFailed,
                "tableName",
                local,
                @base,
                "rawResult",
                remote);
        }
    }
}
