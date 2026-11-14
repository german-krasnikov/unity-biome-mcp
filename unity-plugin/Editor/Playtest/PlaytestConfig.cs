using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    [CreateAssetMenu(menuName = "🧬MCP/Playtest Config")]
    public class PlaytestConfig : ScriptableObject, IAliasSource
    {
        [Header("Character Movement")]
        public string characterPath = "";
        public string moveComponent = "";
        public string moveMethod = "";
        public string isMovingField = "IsMoving";

        [Header("Movement Arrival")]
        public string arrivalOp = "==";
        public string arrivalValue = "False";

        [Header("Time Scale")]
        public string timeScaleClass = "";
        public string timeScaleProperty = "";

        [Header("CTA")]
        public string ctaPath;

        [Header("Aliases")]
        public List<QueryAlias> aliases = new();

        // IAliasSource — decouples PlaytestParser.ResolveQuery from this ScriptableObject
        // (D06/D09). Was `QueryAlias FindAlias(string)`; changed in place per the plan's own
        // verified "exactly one call site" audit (PlaytestParser.Subroutines.cs's ResolveQuery).
        public AliasMatch? FindAlias(string name)
        {
            var a = aliases.Find(x => x.alias == name);
            return a != null ? new AliasMatch(a.path, a.component, a.field) : (AliasMatch?)null;
        }
    }

    public enum AliasType
    {
        ValPath    = 0,  // VAL $name /path|Component|field  (backward-compat default)
        ValConst   = 1,  // VAL $name some_literal_value
        VarRuntime = 2,  // VAR $name @/path|Component|field (C#-DSL only)
    }

    [Serializable]
    public class QueryAlias
    {
        public string alias;
        public AliasType type = AliasType.ValPath;
        public string path;
        public string component;
        public string field;
        public string constValue;
    }
}
