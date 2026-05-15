using Xunit;

namespace WinLangSwitcher.Tests;

public class LayoutSwitcherTests
{
    private static readonly IntPtr Ru = new(0x04190419);
    private static readonly IntPtr En = new(0x04090409);
    private static readonly IntPtr Other = new(0x0407);

    [Fact]
    public void PickNext_TwoLayouts_CurrentFirst_ReturnsSecond()
        => Assert.Equal(En, LayoutSwitcher.PickNext(Ru, new[] { Ru, En }));

    [Fact]
    public void PickNext_TwoLayouts_CurrentSecond_ReturnsFirst()
        => Assert.Equal(Ru, LayoutSwitcher.PickNext(En, new[] { Ru, En }));

    [Fact]
    public void PickNext_OneLayout_ReturnsCurrent()
        => Assert.Equal(Ru, LayoutSwitcher.PickNext(Ru, new[] { Ru }));

    [Fact]
    public void PickNext_EmptyList_ReturnsCurrent()
        => Assert.Equal(Ru, LayoutSwitcher.PickNext(Ru, Array.Empty<IntPtr>()));

    [Fact]
    public void PickNext_ThreeLayouts_ReturnsFirstNonCurrent()
        => Assert.Equal(Ru, LayoutSwitcher.PickNext(En, new[] { Ru, En, Other }));

    [Fact]
    public void PickNext_CurrentNotInList_ReturnsFirstElement()
        => Assert.Equal(Ru, LayoutSwitcher.PickNext(Other, new[] { Ru, En }));
}
