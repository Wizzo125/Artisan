﻿namespace FFXIVClientStructs.FFXIV.Component.GUI;

[StructLayout(LayoutKind.Explicit, Size = 0x34)]
public unsafe struct AtkUldComponentDataMap
{
    [FieldOffset(0x00)] public AtkUldComponentDataBase Base;
    [FieldOffset(0x0C)] public fixed uint Nodes[10];
}