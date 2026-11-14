using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Security.Cryptography;

namespace System.Security.CryptographyRuntimeOverrides;

[RuntimeOverrideType(typeof(RandomNumberGenerator))]
internal static class RandomNumberGeneratorOverrides
{
    [JavaScriptOverride]
    internal static byte[] GetBytes(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        throw new PlatformNotSupportedException();
    }

    [JavaScriptOverride]
    internal static int GetInt32(int toExclusive)
    {
        if (toExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toExclusive));
        }

        throw new PlatformNotSupportedException();
    }

    [JavaScriptOverride]
    internal static int GetInt32(int fromInclusive, int toExclusive)
    {
        if (fromInclusive >= toExclusive)
        {
            throw new ArgumentException(
                "The exclusive upper bound must exceed the inclusive lower bound.");
        }

        throw new PlatformNotSupportedException();
    }
}
