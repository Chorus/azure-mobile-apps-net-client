using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;
using System;
using Xunit;
using Moq;
using FluentAssertions;
using static FluentAssertions.FluentActions;

namespace MobileClient.Tests.Table.Sync
{
    public class PropertyConflictTests
    {
        [Fact]
        public void WhenLocalDifferentFromBaseAndRemote_ThenIsLocalChangedIsTrueAndRemoteChangedIsFalse()
        {
            // Arrange
            var baseValue = JToken.Parse(@"{""Property1"":""abc""}");
            var remoteValue = JToken.Parse(@"{""Property1"":""abc""}");
            var localValue = JToken.Parse(@"{""Property1"":""abcdef""}");
            var error = Mock.Of<IMobileServiceUpdateOperationError>(x =>
                x.PreviousItem == baseValue &&
                x.Result == remoteValue &&
                x.Item == localValue);

            // Act
            var sut = new PropertyConflict("Property1", error);

            // Assert
            sut.IsLocalChanged.Should().BeTrue();
            sut.IsRemoteChanged.Should().BeFalse();
        }

        [Fact]
        public void WhenRemoteDifferentFromBaseAndLocal_ThenIsLocalChangedIsFalseAndRemoteChangedIsTrue()
        {
            // Arrange
            var baseValue = JToken.Parse(@"{""Property1"":1}");
            var remoteValue = JToken.Parse(@"{""Property1"":-2000}");
            var localValue = JToken.Parse(@"{""Property1"":1}");
            var error = Mock.Of<IMobileServiceUpdateOperationError>(x =>
                x.PreviousItem == baseValue &&
                x.Result == remoteValue &&
                x.Item == localValue);

            // Act
            var sut = new PropertyConflict("Property1", error);

            // Assert
            sut.IsLocalChanged.Should().BeFalse();
            sut.IsRemoteChanged.Should().BeTrue();
        }

        [Fact]
        public void WhenBaseDifferentFromRemoteAndLocal_ThenIsLocalChangedIsTrueAndRemoteChangedIsTrue()
        {
            // Arrange
            var baseValue = JToken.Parse(@"{""Property1"":null}");
            var remoteValue = JToken.Parse(@"{""Property1"":0}");
            var localValue = JToken.Parse(@"{""Property1"":0}");
            var error = Mock.Of<IMobileServiceUpdateOperationError>(x =>
                x.PreviousItem == baseValue &&
                x.Result == remoteValue &&
                x.Item == localValue);

            // Act
            var sut = new PropertyConflict("Property1", error);

            // Assert
            sut.IsLocalChanged.Should().BeTrue();
            sut.IsRemoteChanged.Should().BeTrue();
        }

        [Fact]
        public void WhenValueIsArray_ThenInvalidOperationException()
        {
            // Arrange
            var remoteValue = JToken.Parse(@"{""Property1"":[]}");
            var error = Mock.Of<IMobileServiceUpdateOperationError>(x => x.Result == remoteValue);

            // Act
            Invoking(() => new PropertyConflict("Property1", error))

            // Assert
            .Should()
            .Throw<InvalidOperationException>()
            .Where(r => r.Message.Contains("value is an object or array which is not supported"));
        }
    }
}
