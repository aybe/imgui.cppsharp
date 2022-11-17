﻿using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Text;
using im.NET.Generator;

namespace imgui.NET.Generator;

internal sealed class ConsoleGeneratorImGui : ConsoleGenerator
{
    public ConsoleGeneratorImGui(string moduleName, string? directory = null) : base(moduleName, directory)
    {
        Namespaces = GetDefaultNamespaces();

        Classes = new SortedSet<KeyValuePair<string, string>>
            {
                new("imgui", "ImGui")
            }
            .ToImmutableSortedSet();

        Aliases = GetDefaultAliases();
    }

    public override ImmutableSortedSet<string> Namespaces { get; }

    public override ImmutableSortedSet<KeyValuePair<string, string>> Classes { get; }

    public override ImmutableSortedSet<Type> Aliases { get; }

    protected override void Process(ref string text)
    {
        
        var builder = new StringBuilder(text);

        const string @namespace = Constants.ImGuiNamespace;

        // merge symbols with class to remove __Symbols namespace

        builder.Replace(
            $"}}\r\nnamespace {@namespace}.__Symbols\r\n{{\r\n    internal class imgui",
            "    public unsafe partial class imgui"
        );

        // hide structs that should have been internal

        builder.Replace(
            "public partial struct __Internal",
            "internal partial struct __Internal"
        );

        builder.Replace(
            "public unsafe partial struct __Internal",
            "internal unsafe partial struct __Internal"
        );

        // hide replaced vectors, their internal stuff is still used

        builder.Replace(
            "public unsafe partial struct ImVec2",
            "internal unsafe partial struct ImVec2"
        );


        builder.Replace(
            "public unsafe partial struct ImVec4",
            "internal unsafe partial struct ImVec4"
        );

        // pass vectors directly, doesn't mean that we can ditch type maps that did the heavy lifting

        builder.Replace(
            $"new global::{@namespace}.ImVec2.__Internal()",
            "new global::System.Numerics.Vector2()"
        );

        builder.Replace(
            $"new global::{@namespace}.ImVec4.__Internal()",
            "new global::System.Numerics.Vector4()"
        );

        // they're now unused in sources, let's add a little guard though

        builder.Replace(
            "internal unsafe partial struct ImVec2",
            "[global::System.Obsolete(null, true)] internal unsafe partial struct ImVec2"
        );

        builder.Replace(
            "internal unsafe partial struct ImVec4",
            "[global::System.Obsolete(null, true)] internal unsafe partial struct ImVec4"
        );

        // hide ImVector namespace as internal class as it cannot be moved onto ImVector<T> because of CS7042

        builder.Replace(
            "namespace ImVector",
            "internal static partial class ImVector"
        );
        builder.Replace(
            "public static IntPtr _EmptyString_ImGuiTextBuffer__2PADA",
            "internal static IntPtr _EmptyString_ImGuiTextBuffer__2PADA"
        );

        builder.Replace(
            ".__Symbols",
            string.Empty
        );

        // type map does not appear to be thoroughly applied, fix

        builder.Replace(
            "__element is null ? new global::System.Numerics.Vector2() : *(global::System.Numerics.Vector2*) __element.__Instance",
            "__element"
        );

        // use our own symbol resolver

        builder.Replace(
            "CppSharp.SymbolResolver",
            $"{@namespace}.SymbolResolver"
        );

        // XML comments are somehow wrong, fix that

        builder.Replace(
            "// <summary>",
            "/// <summary>"
        );

        var str = builder.ToString();

        // add some inherit doc

        str = Regex.Replace(
            str,
            @"^(\s+)(public void Dispose\(\))",
            @"$1/// <inheritdoc />$1$2",
            RegexOptions.Multiline
        );

        str = Regex.Replace(
            str,
            @"^(\s+)(~\w+\(\))",
            @"$1/// <inheritdoc />$1$2",
            RegexOptions.Multiline
        );

        text = str;

        base.Process(ref text);
    }
}