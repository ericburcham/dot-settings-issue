using Code;
using FluentAssertions;

namespace Tests;

public class MultiplyTests
{
    [Test]
    public void WhenEitherInputIsZeroTheResultIsZero()
    {
        Functions.Multiply(0, 1).Should().Be(0);
        Functions.Multiply(1, 0).Should().Be(0);
    }

    [Test]
    public void WhenBothInputsArePositiveTheResultShouldBePositive()
    {
        Functions.Multiply(1, 1).Should().BeGreaterThan(0);
    }

    [Test]
    public void WhenBothInputsAreNegativeTheResultShouldBePositive()
    {
        Functions.Multiply(-1, -1).Should().BeGreaterThan(0);
    }

    [Test]
    public void WhenOneInputIsNegativeTheResultShouldBeNegative()
    {
        Functions.Multiply(1, -1).Should().BeLessThan(0);
        Functions.Multiply(-1, 1).Should().BeLessThan(0);
    }
}