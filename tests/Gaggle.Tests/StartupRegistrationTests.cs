using Gaggle.Configuration;

namespace Gaggle.Tests;

/// <summary>
/// The registration itself needs the real registry and a real logon to prove anything,
/// so what is pinned here is the part that breaks without saying so: an unquoted path
/// with a space in it is a command Windows runs at logon and fails.
/// </summary>
public class StartupRegistrationTests
{
    [Fact]
    public void CommandQuotesThePath()
    {
        Assert.Equal(
            @"""C:\Program Files\Gaggle\Gaggle.exe""",
            StartupRegistration.CommandFor(@"C:\Program Files\Gaggle\Gaggle.exe"));
    }
}
