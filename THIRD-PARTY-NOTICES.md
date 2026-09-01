# Third-Party Notices

The base Unity Biome MCP plugin (distributed through the UPM package `unity-plugin/`) does not include or depend on third-party components.

This document covers third-party components provided **only in the optional Mutation Mode provider package**, installed separately by the user via UPM git dependency. The base plugin functions without this provider.

## FastScriptReload (optional provider package)

**Project:** FastScriptReload  
**Source:** https://github.com/german-krasnikov/FastScriptReload  
**Pinned fork branch:** `biome-mcp-fat-blob-qualification` (commit b90a5c3f)  
**License:** MIT  
**Copyright:** Copyright (c) 2020–2022 Chris Handzlik  
**Role:** Optional provider for Mutation Mode (in-memory method patching without domain reload)

**License text:**

```
MIT License

Copyright (c) 2020 Chris Handzlik

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Harmony (vendored within FastScriptReload)

**Project:** Harmony  
**Source:** https://github.com/pardeike/Harmony  
**License:** MIT  
**Copyright:** Copyright (c) 2017 Andreas Pardeike  
**Role:** Runtime method patching library, vendored inside the FastScriptReload package

**License text:**

```
MIT License

Copyright (c) 2017 Andreas Pardeike

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ImmersiveVrToolsCommon (vendored within FastScriptReload)

**Project:** ImmersiveVrToolsCommon  
**License:** MIT (inherits from FastScriptReload)  
**Role:** Utility library vendored inside the FastScriptReload package for convenience

This component is included as part of the FastScriptReload package and operates under the same MIT license.

---

**Note:** To use Mutation Mode, you must explicitly install the optional provider package into your project's `Packages/manifest.json`. The base Unity Biome MCP plugin does not bundle or depend on any of these components.
