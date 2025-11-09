using System.Runtime.InteropServices;
using AutoHook.Spearfishing.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHook.Spearfishing.Struct;

[StructLayout(LayoutKind.Explicit)]
public struct SpearfishWindow
{
    [FieldOffset(0)]
    public AtkUnitBase Base;

    [StructLayout(LayoutKind.Explicit)]
    public struct Info
    {
        [FieldOffset(8)]
        public bool Available;

        [FieldOffset(16)]
        public bool InverseDirection;

        [FieldOffset(17)]
        public bool GuaranteedLarge;

        [FieldOffset(18)]
        public SpearfishSize Size;

        [FieldOffset(20)]
        public SpearfishSpeed Speed;
    }

    [FieldOffset(0x294)]
    public Info Fish1;

    [FieldOffset(0x2B0)]
    public Info Fish2;

    [FieldOffset(0x2CC)]
    public Info Fish3;

    public unsafe AtkResNode* FishLines
        => Base.UldManager.NodeList[3];

    public unsafe AtkResNode* Fish1Node
        => Base.UldManager.NodeList[15];

    public unsafe AtkResNode* Fish2Node
        => Base.UldManager.NodeList[16];

    public unsafe AtkResNode* Fish3Node
        => Base.UldManager.NodeList[17];

    public unsafe AtkComponentGaugeBar* GaugeBar
        => (AtkComponentGaugeBar*)Base.UldManager.NodeList[35];

}