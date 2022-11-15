﻿using CppSharp.Passes;

namespace im.NET.Generator.Passes;

public abstract class ImBasePass : TranslationUnitPass
{
    protected const string Indent = "    "; // Microsoft Visual Studio Debug Console sucks

    protected ImBasePass(GeneratorType generatorType)
    {
        GeneratorType = generatorType;
    }

    protected GeneratorType GeneratorType { get; }

    protected static ConsoleColorScope GetConsoleColorScope(ConsoleColor? backgroundColor = ConsoleColor.Red, ConsoleColor? foregroundColor = ConsoleColor.White)
    {
        return new ConsoleColorScope(backgroundColor, foregroundColor);
    }
}