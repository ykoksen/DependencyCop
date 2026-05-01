# DependencyCop

> **Note:** This is a fork of the [original DependencyCop analyzer](https://github.com/larsiverpp/DependencyCop) by Lars Iversen. The major enhancement in this fork is the addition of an **automatic code fixer for rule DC1001**, which can automatically refactor code to remove disallowed `using` statements and replace them with qualified type names.
>
> **Rule IDs remain unchanged** (DC1001, DC1002, DC1003, DC1004), ensuring full backward compatibility with existing `.editorconfig` files, ruleset configurations, and suppression attributes when migrating from `Liversen.DependencyCop`.

This repository contains an implementation of a number of Roslyn analyzer rules using the .NET Compiler Platform. The rules enforce certain restrictions on dependencies between code in different namespaces.

For an overview of the rules, see [README.md](./Lindhart.DependencyCop.Package/README.md).

## What's New in This Fork

### Automatic Code Fix for DC1001 ✨

The key difference from the original analyzer is the inclusion of a **CodeFixProvider** for rule DC1001. When the analyzer detects a violation (use of a disallowed namespace in a `using` statement), you can now:

- **Apply automatic fixes** via IDE quick actions (Ctrl+. in Visual Studio)
- **Fix all occurrences** in a document, project, or solution in one click
- The fixer automatically:
  - Removes the violating `using` statement
  - Replaces all references with appropriately qualified type names (using only the necessary namespace parts relative to the current context)
  - Handles complex scenarios including generics, arrays, async methods, extension methods, and more

This makes it significantly easier to adopt and enforce the DC1001 coding style across large codebases.

## Rationale

> Identifier naming is important when you write code. That applies to all kinds of identifiers such as namespaces, classes, functions and variables. Identifiers that only exist in smaller contexts such as local variables inside small functions can be short and require less consideration than other names... Namespace names exist in very large contexts, thus you should take great care when you name them.

The above quote is from [Namespace Naming](https://www.linkedin.com/pulse/namespace-naming-lars-iversen/). The key point is that good programming starts with well-chosen names including namespace names. And those namespaces have to be carefully structured as they are an important part of many (low level) software architectures.

The rules in this repository aim at helping getting those namespace structures right by applying some restrictions to what can be done. As soon as a software project grows and the number of namespaces increases, these rules are a first-line defence against poor architecture. 

## Using DependencyCop

The severity of individual rules may be configured using [rule set files](https://docs.microsoft.com/en-us/visualstudio/code-quality/using-rule-sets-to-group-code-analysis-rules).

Rule [DC1001](https://github.com/ykoksen/DependencyCop/blob/main/Documentation/DC1001.md) requires additional configuration to be enabled, see the documentation for that rule for further info.

## Installation

The analyzers can be installed using the NuGet command line or the NuGet Package Manager in Visual Studio.

Install using the command line:

    Install-Package Lindhart.DependencyCop

