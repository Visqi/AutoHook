using AutoHook.Enums;
using AutoHook.Utils;
using Dalamud.Game;

namespace AutoHook.SeFunctions;

public sealed class SeTugType(ISigScanner sigScanner) : SeAddressBase(sigScanner,
        SignaturePatterns.TugType)
{
    public unsafe BiteType Bite
        => Address != IntPtr.Zero ? *(BiteType*)Address : BiteType.Unknown;
}

