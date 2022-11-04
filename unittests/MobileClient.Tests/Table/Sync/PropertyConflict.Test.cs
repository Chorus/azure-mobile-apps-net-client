using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Moq;
using FluentAssertions;

namespace MobileClient.Tests.Table.Sync
{
    public class PropertyConflictTests
    {
        [Fact]
        public void GivenLocalValueDifferentFromBaseValue_ThenIsLocalChangedTrueAndRemoteChangedFalse()
        {
            // Arrange
            var baseValue = JToken.Parse(@"{""Property1"":""abc""}");
            var localValue = JToken.Parse(@"{""Property1"":""abcdef""}");
            var resultValue = JToken.Parse(@"{""Property1"":""abc""}");
            var error = Mock.Of<IMobileServiceUpdateOperationError>(x =>
                x.Item == localValue &&
                x.PreviousItem == baseValue &&
                x.Result == resultValue);

            // Act
            var sut = new PropertyConflict("Property1", error);

            // Assert
            sut.IsLocalChanged.Should().BeTrue();
            sut.IsRemoteChanged.Should().BeFalse();
        }

        [Fact]
        public void CompareJTokenTests()
        {
            // Arrange
            var baseValue = JToken.Parse(@"""abc""");
            var localValue = JToken.Parse(@"""abc""");
            var differentValue = JToken.Parse(@"""abcdef""");

            (Equals(baseValue,localValue)).Should().BeTrue();
            (Equals(differentValue, localValue)).Should().BeFalse();
        }
    }
}
