using System;
using System.Collections.Generic;

namespace KernelMemory.Extensions.Helper;

public static class LigatureHelper
{
    // Common ligature mappings
    private static readonly Dictionary<string, string> Ligatures = new()
    {
        // Latin ligatures
        {"ﬀ", "ff"},     // U+FB00
        {"ﬁ", "fi"},     // U+FB01
        {"ﬂ", "fl"},     // U+FB02
        {"ﬃ", "ffi"},    // U+FB03
        {"ﬄ", "ffl"},    // U+FB04
        {"ﬅ", "st"},     // U+FB05
        {"ﬆ", "st"},     // U+FB06
        
        // Additional typographic ligatures
        {"℔", "lb"},     // U+2114
        {"℞", "Rx"},     // U+211E
    };

    public static bool IsLigature(Char c)
    {
        return Ligatures.ContainsKey(c.ToString());
    }

    public static string ExpandLigature(Char c)
    {
        return Ligatures[c.ToString()];
    }
}