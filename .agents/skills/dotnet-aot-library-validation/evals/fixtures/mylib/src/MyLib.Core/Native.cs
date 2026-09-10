using System;
using System.Runtime.InteropServices;

namespace MyLib.Core;

public static partial class Native
{
    [DllImport("nativelib", EntryPoint = "mylib_now", CharSet = CharSet.Unicode)]
    internal static extern long Now(string label);

    [DllImport("nativelib", EntryPoint = "mylib_compress")]
    internal static extern byte[] Compress(byte[] input);
}
