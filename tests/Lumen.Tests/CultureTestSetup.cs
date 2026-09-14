using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lumen.Tests;

internal static class CultureTestSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        string? cultureName = Environment.GetEnvironmentVariable("LUMEN_TEST_CULTURE");
        if (string.IsNullOrEmpty(cultureName))
        {
            return;
        }

        CultureInfo culture = new(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
