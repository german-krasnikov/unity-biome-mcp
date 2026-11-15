// Generated from dsl/protocol.biome
// source-sha256=352117f8394d358b392f15dab91f0159c771fe0ca2fba08a9e7a60f297dc6bd5
// Edit the source, then run biome generate.

using System.Globalization;
using System.Text;

namespace UnityMCP.Editor
{
    using int_opt = System.Nullable<int>;
    using float1_opt = System.Nullable<double>;
    using bool_opt = System.Nullable<bool>;
    using float1 = System.Double;
    using word = System.String;
    using text = System.String;
    using word_opt = System.String;
    using text_opt = System.String;

    // Printed from dsl/protocol.biome: the wire records. Never edit.
    internal static class WireRecords
    {
    internal static string FormatSyncStatus(
        int epoch,
        word state,
        text_opt err= default,
        float1_opt dur= default,
        text_opt stamp= default,
        bool_opt iscompiling= default,
        bool_opt cn_active= default,
        bool_opt started= default,
        bool_opt stamp_frozen= default,
        int _end = 0)
    {
        var sb = new StringBuilder();
        sb.Append(sb.Length == 0 ? "" : "|").Append("epoch=").Append(Wire.w_int(epoch));
        sb.Append(sb.Length == 0 ? "" : "|").Append("state=").Append(Wire.w_word(state));
        if (err != null) sb.Append(sb.Length == 0 ? "" : "|").Append("err=").Append(Wire.w_text(err));
        if (dur != null) sb.Append(sb.Length == 0 ? "" : "|").Append("dur=").Append(Wire.w_float1(dur));
        if (stamp != null) sb.Append(sb.Length == 0 ? "" : "|").Append("stamp=").Append(Wire.w_text(stamp));
        if (iscompiling != null) sb.Append(sb.Length == 0 ? "" : "|").Append("iscompiling=").Append(Wire.w_bool(iscompiling));
        if (cn_active != null) sb.Append(sb.Length == 0 ? "" : "|").Append("cn_active=").Append(Wire.w_bool(cn_active));
        if (started != null) sb.Append(sb.Length == 0 ? "" : "|").Append("started=").Append(Wire.w_bool(started));
        if (stamp_frozen != null) sb.Append(sb.Length == 0 ? "" : "|").Append("stamp_frozen=").Append(Wire.w_bool(stamp_frozen));
        return sb.ToString();
    }
    internal static string FormatSyncAck(
        int epoch,
        bool will_compile,
        int _end = 0)
    {
        var sb = new StringBuilder();
        sb.Append("sync_ack");
        sb.Append(sb.Length == 0 ? "" : "|").Append("epoch=").Append(Wire.w_int(epoch));
        sb.Append(sb.Length == 0 ? "" : "|").Append("will_compile=").Append(Wire.w_bool(will_compile));
        return sb.ToString();
    }
    internal static string FormatTestsStarted(
        word request_id,
        word run_id,
        word utf_guid,
        word state,
        int _end = 0)
    {
        var sb = new StringBuilder();
        sb.Append("tests-started");
        sb.Append(sb.Length == 0 ? "" : "|").Append("request_id=").Append(Wire.w_word(request_id));
        sb.Append(sb.Length == 0 ? "" : "|").Append("run_id=").Append(Wire.w_word(run_id));
        sb.Append(sb.Length == 0 ? "" : "|").Append("utf_guid=").Append(Wire.w_word(utf_guid));
        sb.Append(sb.Length == 0 ? "" : "|").Append("state=").Append(Wire.w_word(state));
        return sb.ToString();
    }
    }
}
