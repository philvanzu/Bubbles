
using System;
using System.Security.Cryptography;

namespace Bubbles4.Services;
public static class CryptoRandom
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    public static int NextInt()
    {
        var bytes = new byte[4];
        _rng.GetBytes(bytes);
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue; // Keep it non-negative
    }
}