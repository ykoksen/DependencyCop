# DependencyCop

> **This package is a fork of the [original Liversen.DependencyCop](https://www.nuget.org/packages/Liversen.DependencyCop) by Lars Iversen, enhanced with automatic code fixing capabilities.**
>
> **Migrating from Liversen.DependencyCop?** All rule IDs remain unchanged (DC1001, DC1002, DC1003, DC1004), so your existing configurations will continue to work without modification.

This package contains a number of Roslyn analyzer rules using the .NET Compiler Platform. The rules enforce certain restrictions on dependencies between code in different namespaces.

## Rules

[Rule DC1001: Using namespace statements must not reference disallowed namespaces](https://github.com/ykoksen/DependencyCop/blob/main/Documentation/DC1001.md)

[Rule DC1002: Code must not refer code in descendant namespaces](https://github.com/ykoksen/DependencyCop/blob/main/Documentation/DC1002.md)

[Rule DC1003: Code must not contain namespace cycles](https://github.com/ykoksen/DependencyCop/blob/main/Documentation/DC1003.md)

[Rule DC1004: Rule DC1001 is not configured](https://github.com/ykoksen/DependencyCop/blob/main/Documentation/DC1004.md)

---

📖 **For complete documentation, rationale, and details about the fork**, see the [main repository README](https://github.com/ykoksen/DependencyCop#readme).