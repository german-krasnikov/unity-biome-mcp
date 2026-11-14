using System.Text;

namespace UnityMCP.Editor
{
    public static partial class JsonHelper
    {
        // DSL gap analysis #6: every wire response is the same {id, ok, data?,
        // err?} envelope. This is the single place that builds the "id"/"ok"
        // header -- each Format* method below only appends its own extra
        // fields on top, instead of re-concatenating the envelope by hand.
        // Covered by server/tests/test_wire_envelope_contract.py (Python side
        // has no separate copy of the header to drift -- it just reads the keys).
        private static StringBuilder AppendEnvelopeHeader(string id, bool ok)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":").Append(ok ? "true" : "false");
            return sb;
        }

        internal static string FormatResponse(string id, bool ok, string data, string error)
        {
            var sb = AppendEnvelopeHeader(id, ok);
            if (ok)
                sb.Append(",\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            else
                sb.Append(",\"err\":\"").Append(EscapeJson(error ?? "")).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        internal static string FormatFileResponse(string id, string filePath) =>
            FormatFileResponseWithData(id, filePath, "");

        internal static string FormatFileResponseWithData(string id, string filePath, string data)
        {
            var sb = AppendEnvelopeHeader(id, true);
            sb.Append(",\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            sb.Append(",\"file\":\"").Append(EscapeJson(filePath)).Append("\"}");
            return sb.ToString();
        }

        internal static string FormatBusyResponse(string id, string message, int retryMs)
        {
            var sb = AppendEnvelopeHeader(id, false);
            sb.Append(",\"err\":\"").Append(EscapeJson(message ?? "")).Append("\"");
            sb.Append(",\"retry\":").Append(retryMs).Append("}");
            return sb.ToString();
        }

        // Boundary hardening Phase 4.2: for callers whose extra fields don't fit
        // FormatResponse/FormatFileResponse/FormatBusyResponse's fixed shapes
        // (e.g. client_hello's helloVersion+version+projectPath+projectId, or
        // status's compile+port+bind_failed+port_fallback). extraFieldsJson is
        // raw already-valid JSON key:value pairs (no leading/trailing comma).
        internal static string FormatResponseExtra(string id, bool ok, string extraFieldsJson)
        {
            var sb = AppendEnvelopeHeader(id, ok);
            if (!string.IsNullOrEmpty(extraFieldsJson)) sb.Append(",").Append(extraFieldsJson);
            sb.Append("}");
            return sb.ToString();
        }

        // T15: inject receipt before closing brace — backward-compat (old Python ignores key).
        internal static string FormatResponseWithReceipt(
            string id, bool ok, string data, string error, string receiptJson)
        {
            var base_ = FormatResponse(id, ok, data, error);
            if (string.IsNullOrEmpty(receiptJson)) return base_;
            return base_.Substring(0, base_.Length - 1) + "," + receiptJson + "}";
        }
    }
}
