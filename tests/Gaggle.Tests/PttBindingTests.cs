using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Gaggle.Input;

namespace Gaggle.Tests;

/// <summary>
/// Describe() reads the localised table, so these assert the English wording. Nothing
/// here calls Strings.Use: Strings.Current starts out English and is global, and xunit
/// runs test classes in parallel, so a test that switched it would reach the others.
/// </summary>
public class PttBindingTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void FromKeyProducesAKeyboardBinding()
    {
        PttBinding binding = PttBinding.FromKey(Keys.F13);

        Assert.Equal(PttSource.Keyboard, binding.Source);
        Assert.True(binding.IsKeyboard);
        Assert.Equal(Keys.F13, binding.Key);
    }

    [Fact]
    public void FromButtonProducesAJoystickBinding()
    {
        PttBinding binding = PttBinding.FromButton(joystickId: 2, button: 7);

        Assert.Equal(PttSource.Joystick, binding.Source);
        Assert.False(binding.IsKeyboard);
        Assert.Equal(2, binding.JoystickId);
        Assert.Equal(7, binding.Button);
    }

    [Fact]
    public void DescribesCapsLockTheWayPeopleNameIt()
    {
        // Keys.CapsLock shares a value with Keys.Capital and would otherwise
        // print as "Capital".
        Assert.Equal("Caps Lock", PttBinding.FromKey(Keys.CapsLock).Describe());
    }

    [Theory]
    [InlineData(Keys.Back, "Backspace")]
    [InlineData(Keys.Return, "Enter")]
    [InlineData(Keys.Next, "Page Down")]
    [InlineData(Keys.F13, "F13")]
    public void DescribesKeysReadably(Keys key, string expected)
    {
        Assert.Equal(expected, PttBinding.FromKey(key).Describe());
    }

    [Fact]
    public void DescribesJoystickButtonsOneBasedForHumans()
    {
        // Stored zero-based, shown one-based, because no one calls it button 0.
        Assert.Equal("Stick 1 · button 5", PttBinding.FromButton(0, 4).Describe());
    }

    [Fact]
    public void DescribesAnUnboundKeyAsUnbound()
    {
        Assert.Equal("Unbound", PttBinding.FromKey(Keys.None).Describe());
    }

    [Fact]
    public void KeyboardBindingSurvivesJsonRoundTrip()
    {
        PttBinding original = PttBinding.FromKey(Keys.Scroll);

        string json = JsonSerializer.Serialize(original, Options);
        PttBinding? restored = JsonSerializer.Deserialize<PttBinding>(json, Options);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void JoystickBindingSurvivesJsonRoundTrip()
    {
        PttBinding original = PttBinding.FromButton(3, 11);

        string json = JsonSerializer.Serialize(original, Options);
        PttBinding? restored = JsonSerializer.Deserialize<PttBinding>(json, Options);

        Assert.Equal(original, restored);
        Assert.Equal("Stick 4 · button 12", restored!.Describe());
    }
}
