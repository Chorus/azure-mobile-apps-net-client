#nullable enable annotations
using FluentAssertions;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Microsoft.WindowsAzure.MobileServices.Sync.Conflicts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MobileClient.Tests.Table.Sync
{
    public class MobileServiceUpdateOperationErrorTests
    {
        [Fact]
        public void WhenIdenticalItems_ThenNoConflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "local" }""");
            var remote = (JObject)JToken.Parse("""{"Property1": 0, "Property2": "abc", "version": "remote" }""");
            var @base = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "base" }""");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().BeEmpty();
        }

        [Fact]
        public void WhenRemoteDifferentFromBase_Then1RemoteConflict()
        {
            // Arrange
            var local = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "local" }""");
            var remote = (JObject)JToken.Parse("""{"Property1": 0, "Property2": "abcdef", "version": "remote" }""");
            var @base = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "base" }""");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            var conflict = sut.PropertyConflicts.Should().ContainSingle().Which;
            conflict.Should().BeEquivalentTo(new { PropertyName = "Property2", IsLocalChanged = false, IsRemoteChanged = true });
        }

        [Fact]
        public void WhenLocalDifferentFromBase_Then1LocalConflict()
        {
            // Arrange
            var local = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abcdef", "version": "local" }""");
            var remote = (JObject)JToken.Parse("""{"Property1": 0, "Property2": "abc", "version": "remote" }""");
            var @base = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "base" }""");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            var conflict = sut.PropertyConflicts.Should().ContainSingle().Which;
            conflict.Should().BeEquivalentTo(new { PropertyName = "Property2", IsLocalChanged = true, IsRemoteChanged = false });
        }

        [Fact]
        public void WhenLocalProperty2ChangedAndRemoteProperty1Changed_Then2Conflicts()
        {
            // Arrange
            var local = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abcdef", "version": "local" }""");
            var remote = (JObject)JToken.Parse("""{"Property1": 1, "Property2": "abc", "version": "remote" }""");
            var @base = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "base" }""");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().HaveCount(2).And
                .ContainEquivalentOf(new { PropertyName = "Property1", IsLocalChanged = false, IsRemoteChanged = true }).And
                .ContainEquivalentOf(new { PropertyName = "Property2", IsLocalChanged = true, IsRemoteChanged = false });
        }

        [Fact]
        public void WhenLocalAndRemoteSamePropertyChanged_Then1Conflict()
        {
            // Arrange
            var local = (JObject)JToken.Parse(""" {"Property1": 2, "Property2": "abc", "version": "local" }""");
            var remote = (JObject)JToken.Parse("""{"Property1": 1, "Property2": "abc", "version": "remote" }""");
            var @base = (JObject)JToken.Parse(""" {"Property1": 0, "Property2": "abc", "version": "base" }""");

            // Act
            var sut = CreateSut(local, remote, @base);

            // Assert
            sut.PropertyConflicts.Should().ContainSingle().Which.Should()
                .BeEquivalentTo(new { PropertyName = "Property1", IsLocalChanged = true, IsRemoteChanged = true });
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
