﻿using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CppSharp;
using im.NET.Generator.Extensions;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;

namespace im.NET.Generator;

public abstract class ConsoleGenerator
{
    /// <summary>
    ///     The set of aliases to be renamed.
    /// </summary>
    [PublicAPI]
    public abstract ImmutableSortedSet<Type> Aliases { get; }

    /// <summary>
    ///     The set of classes to be renamed.
    /// </summary>
    [PublicAPI]
    public abstract ImmutableSortedSet<KeyValuePair<string, string>> Classes { get; }

    /// <summary>
    ///     The set of namespaces used.
    /// </summary>
    [PublicAPI]
    public abstract ImmutableSortedSet<string> Namespaces { get; }

    /// <summary>
    ///     The library used.
    /// </summary>
    protected abstract ILibrary Library { get; }

    protected static ImmutableSortedSet<Type> GetDefaultAliases()
    {
        return new SortedSet<Type>(TypeNameComparer.Instance)
            {
                typeof(CallingConvention),
                typeof(IntPtr)
            }
            .ToImmutableSortedSet(TypeNameComparer.Instance);
    }

    protected static ImmutableSortedSet<string> GetDefaultNamespaces()
    {
        return new SortedSet<string>
            {
                Constants.ImGuiNamespace,
                "System",
                "System.Collections.Concurrent",
                "System.Numerics",
                "System.Runtime.CompilerServices",
                "System.Runtime.InteropServices",
                "System.Security",
                "System.Text"
            }
            .ToImmutableSortedSet();
    }

    protected virtual void Process(ref string input)
    {
        // apply module-wide changes stuff first

        ProcessNamespaces(ref input);
        ProcessAliases(ref input);
        ProcessClasses(ref input);
        ProcessEnumerations(ref input);

        // apply fine-grained stuff

        ProcessVectors(ref input);
        ProcessPointers(ref input);
        ProcessSummaries(ref input);
        ProcessSymbols(ref input);
        ProcessVisibility(ref input);
        ProcessDefaultParameters(ref input);
    }

    private void ProcessAliases(ref string input)
    {
        foreach (var item in Aliases)
        {
            var name = item.Name;

            // remove using alias

            input = Regex.Replace(input,
                $@"^using __{name} = .*;\r?$\n",
                string.Empty,
                RegexOptions.Multiline
            );

            // remove double underscore prefix

            input = input.Replace($"__{name}", name);
        }
    }

    private void ProcessNamespaces(ref string input)
    {
        // simplify fully qualified names

        foreach (var item in Namespaces.Reverse())
        {
            input = input.Replace($"global::{item}.", string.Empty);
        }
    }

    private void ProcessClasses(ref string input)
    {
        // rename classes and references to their internals

        foreach (var (key, val) in Classes)
        {
            input = input.Replace($"class {key}", $"class {val}");
            input = input.Replace($"{key}()",     $"{val}()");
            input = input.Replace($"{key}._",     $"{val}._");
        }
    }

    private static void ProcessDefaultParameters(ref string input)
    {
        // fix invalid syntax 'string[] name = 0'

        input = Regex.Replace(input,
            @"string\s*\[\]\s+(\w+)\s*=\s*0\s*,",
            "string[] $1 = null,",
            RegexOptions.Multiline
        );
    }

    private static void ProcessEnumerations(ref string input)
    {
        // enumerations default values other than zero must be cast

        input = Regex.Replace(input,
            @"(?<!(?://\s*DEBUG:|///\s*<summary>).*)(\w+)\s+(\w+)\s+=\s+([-+]?\d+)(?=[,)])",
            @"$1 $2 = ($1)($3)",
            RegexOptions.Multiline
        );

        // enumerations cast to int while there's no reason to

        input = Regex.Replace(input,
            @"(?<type>\w+)\s+(\w+)\s+=\s+\(int\)\s*\k<type>\.(\w+)",
            "${type} $1 = ${type}.$2",
            RegexOptions.Multiline
        );
    }

    private static void ProcessPointers(ref string input)
    {
        // hide pointers to internal classes

        input = input.Replace(
            "public IntPtr __Instance { get; protected set; }",
            "internal IntPtr __Instance { get; set; }"
        );


        // fix pointer errors caused by use of TypeMapSizeT

        // cast won't work when it's a default parameter

        input = Regex.Replace(input,
            @"\(U?IntPtr\)\(0\)",
            @"default",
            RegexOptions.Multiline);

        // cast is needed when built from an integral type

        input = Regex.Replace(input,
            @"^(\s*)(U?IntPtr)(\s+\S+\s+=\s+)(\d+)(\s*;)",
            @"$1$2$3($2)$4$5",
            RegexOptions.Multiline);
    }

    private static void ProcessSummaries(ref string input)
    {
        // for some reason, summaries have wrong syntax, repair

        input = input.Replace("// <summary>", "/// <summary>");

        // add few inherit doc to reduce CS1591 warnings count

        input = Regex.Replace(
            input,
            @"^(\s+)(public void Dispose\(\))",
            @"$1/// <inheritdoc />$1$2",
            RegexOptions.Multiline
        );

        input = Regex.Replace(
            input,
            @"^(\s+)(~\w+\(\))",
            @"$1/// <inheritdoc />$1$2",
            RegexOptions.Multiline
        );
    }

    private static void ProcessSymbols(ref string text)
    {
        // hide public symbols that ought to be internal

        text = Regex.Replace(text,
            @"public\s+static\s+(\w+)\s+(_(?!_)\w+)",
            @"internal static $1 $2",
            RegexOptions.Multiline
        );
    }

    private static void ProcessVectors(ref string input)
    {
        // type maps aren't enough to pass vectors directly

        input = Regex.Replace(input,
            @"new ImVec(\d)\.__Internal\(\)",
            @"new Vector$1()",
            RegexOptions.Multiline
        );

        // make vectors as obsolete so it's crystal clear

        input = Regex.Replace(input,
            @"public (unsafe partial struct ImVec\d)",
            @"[Obsolete(null, true)] internal $1",
            RegexOptions.Multiline
        );

        // setting value type in type maps doesn't work

        input = Regex.Replace(input,
            @"__element is null \? new .*Vector\d.*;",
            @"__element;",
            RegexOptions.Multiline
        );
    }

    private static void ProcessVisibility(ref string input)
    {
        // hide internal structs and vectors

        input = Regex.Replace(input,
            @"public ((?:unsafe )?partial struct (?:__Internal|ImVec\d))",
            @"internal $1",
            RegexOptions.Multiline
        );

        // hide protected members to remove more CS1591

        input = Regex.Replace(input,
            @"(internal\s+)*protected",
            "private protected",
            RegexOptions.Multiline
        );
    }

    public static async Task Generate(string module, string directory, ConsoleGeneratorFactory factory, ConsoleGeneratorOutputs outputs, ConsoleGeneratorTransform? transform = null)
    {
        Console.WriteLine("Generation starting...");

        var tuples = new[]
        {
            (Architecture.X86, @".\x86"),
            (Architecture.X64, @".\x64")
        };

        foreach (var (arch, path) in tuples)
        {
            Console.WriteLine($"Generating for {arch} in {path}...");

            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            var dest = arch switch
            {
                Architecture.X86 => outputs.CsPathX32,
                Architecture.X64 => outputs.CsPathX64,
                _                => throw new NotSupportedException(arch.ToString())
            };

            await using (await FileHistory.CreateAsync(dest))
            {
                var generator = factory(arch, path);

                ConsoleDriver.Run(generator.Library);

                Console.WriteLine("Post-processing generated code...");

                var text = await File.ReadAllTextAsync(Path.Combine(directory, Path.ChangeExtension(module, ".cs")));

                generator.Process(ref text);

                await File.WriteAllTextAsync(dest, text);
            }
        }

        Console.WriteLine($"Generating for AnyCPU in {outputs.CsPathAny}...");

        var tree32 = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(outputs.CsPathX32));
        var tree64 = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(outputs.CsPathX64));

        var root32 = await tree32.GetRootAsync();
        var root64 = await tree64.GetRootAsync();

        var rewriter = new ConsoleGeneratorRewriter(root32, root64);

        var visit = rewriter.Visit(root32);

        var contents = visit.ToFullString();

        if (transform != null)
        {
            Console.WriteLine("Transforming AnyCPU...");
            contents = transform(contents);
        }

        await using (await FileHistory.CreateAsync(outputs.CsPathAny))
        {
            await File.WriteAllTextAsync(outputs.CsPathAny, contents);
        }

        Console.WriteLine("Generation complete.");
    }
}