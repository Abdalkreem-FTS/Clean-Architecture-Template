using CleanArchitecture.Application.Common;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests;

public sealed class PageRequestTests
{
    [Fact]
    public void Of_WithNothingSupplied_UsesTheDefaults()
    {
        var page = PageRequest.Of(null, null);

        page.Number.ShouldBe(1);
        page.Size.ShouldBe(PageRequest.DefaultSize);
        page.Skip.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Of_WithAPageBelowOne_ClampsToTheFirstPage(int number) =>
        PageRequest.Of(number, null).Number.ShouldBe(1);

    [Theory]
    [InlineData(101)]
    [InlineData(10_000)]
    [InlineData(int.MaxValue)]
    public void Of_WithAnOversizedPage_ClampsToTheMaximum(int size) =>
        PageRequest.Of(null, size).Size.ShouldBe(PageRequest.MaxSize);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Of_WithANonPositiveSize_ClampsToAtLeastOne(int size) =>
        PageRequest.Of(null, size).Size.ShouldBe(1);

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 25, 50)]
    public void Skip_IsTheOffsetOfTheRequestedPage(int number, int size, int expected) =>
        PageRequest.Of(number, size).Skip.ShouldBe(expected);
}
