// Generated from dsl/decisions/verdict.logic
// source-sha256=19b8bf67b026382459d636d81e58f5c321b78cee4ea78b8caf3e5988469b967a
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Decisions.Verdict {
public static class Policy {
    public interface IPassesInputs { double LedgerFailed(); bool LedgerHasFailed(); double LedgerSteps(); double LedgerStepsNotOk(); bool LedgerTeardownOk(); bool ReportBlank(); string ReportFormat(); string ReportPlane(); bool TextHasAborted(); bool TextHasBlocked(); bool TextHasConsoleErr(); bool TextHasError(); bool TextHasFail(); bool TextHasTimeout(); string TextHeader(); double TextPassed(); double TextTotal(); }
    public readonly struct PassesInput { public double LedgerFailed { get; } public bool LedgerHasFailed { get; } public double LedgerSteps { get; } public double LedgerStepsNotOk { get; } public bool LedgerTeardownOk { get; } public bool ReportBlank { get; } public string ReportFormat { get; } public string ReportPlane { get; } public bool TextHasAborted { get; } public bool TextHasBlocked { get; } public bool TextHasConsoleErr { get; } public bool TextHasError { get; } public bool TextHasFail { get; } public bool TextHasTimeout { get; } public string TextHeader { get; } public double TextPassed { get; } public double TextTotal { get; } public PassesInput(double LedgerFailed, bool LedgerHasFailed, double LedgerSteps, double LedgerStepsNotOk, bool LedgerTeardownOk, bool ReportBlank, string ReportFormat, string ReportPlane, bool TextHasAborted, bool TextHasBlocked, bool TextHasConsoleErr, bool TextHasError, bool TextHasFail, bool TextHasTimeout, string TextHeader, double TextPassed, double TextTotal) { this.LedgerFailed = LedgerFailed; this.LedgerHasFailed = LedgerHasFailed; this.LedgerSteps = LedgerSteps; this.LedgerStepsNotOk = LedgerStepsNotOk; this.LedgerTeardownOk = LedgerTeardownOk; this.ReportBlank = ReportBlank; this.ReportFormat = ReportFormat; this.ReportPlane = ReportPlane; this.TextHasAborted = TextHasAborted; this.TextHasBlocked = TextHasBlocked; this.TextHasConsoleErr = TextHasConsoleErr; this.TextHasError = TextHasError; this.TextHasFail = TextHasFail; this.TextHasTimeout = TextHasTimeout; this.TextHeader = TextHeader; this.TextPassed = TextPassed; this.TextTotal = TextTotal; } }
    public static bool Passes(IPassesInputs input) {
        var value7 = input.LedgerFailed();
        if (double.IsNaN(value7) || double.IsInfinity(value7) || System.Math.Truncate(value7) != value7 || System.Math.Abs(value7) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.failed", "LOGIC_INPUT_CONTRACT");
        var value6 = input.LedgerHasFailed();
        var value8 = input.LedgerSteps();
        if (double.IsNaN(value8) || double.IsInfinity(value8) || value8 < 0d || value8 > 1000000d || System.Math.Truncate(value8) != value8 || System.Math.Abs(value8) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.steps", "LOGIC_INPUT_CONTRACT");
        var value9 = input.LedgerStepsNotOk();
        if (double.IsNaN(value9) || double.IsInfinity(value9) || value9 < 0d || value9 > 1000000d || System.Math.Truncate(value9) != value9 || System.Math.Abs(value9) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.stepsNotOk", "LOGIC_INPUT_CONTRACT");
        var value5 = input.LedgerTeardownOk();
        var value2 = input.ReportBlank();
        var value1 = input.ReportFormat();
        if (value1 is null || (value1 != "json" && value1 != "text")) throw new System.ArgumentOutOfRangeException("report.format", "LOGIC_INPUT_CONTRACT");
        var value0 = input.ReportPlane();
        if (value0 is null || (value0 != "server" && value0 != "editor")) throw new System.ArgumentOutOfRangeException("report.plane", "LOGIC_INPUT_CONTRACT");
        var value19 = input.TextHasAborted();
        var value17 = input.TextHasBlocked();
        var value16 = input.TextHasConsoleErr();
        var value15 = input.TextHasError();
        var value14 = input.TextHasFail();
        var value18 = input.TextHasTimeout();
        var value11 = input.TextHeader();
        if (value11 is null || (value11 != "none" && value11 != "first" && value11 != "later")) throw new System.ArgumentOutOfRangeException("text.header", "LOGIC_INPUT_CONTRACT");
        var value12 = input.TextPassed();
        if (double.IsNaN(value12) || double.IsInfinity(value12) || value12 < 0d || value12 > 1000000d || System.Math.Truncate(value12) != value12 || System.Math.Abs(value12) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.passed", "LOGIC_INPUT_CONTRACT");
        var value13 = input.TextTotal();
        if (double.IsNaN(value13) || double.IsInfinity(value13) || value13 < 0d || value13 > 1000000d || System.Math.Truncate(value13) != value13 || System.Math.Abs(value13) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.total", "LOGIC_INPUT_CONTRACT");
        return Decision_passes.Evaluate(value7, value6, value8, value9, value5, value2, value1, value0, value19, value17, value16, value15, value14, value18, value11, value12, value13);
    }
    public static bool Passes(PassesInput input) {
        var value7 = input.LedgerFailed;
        if (double.IsNaN(value7) || double.IsInfinity(value7) || System.Math.Truncate(value7) != value7 || System.Math.Abs(value7) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.failed", "LOGIC_INPUT_CONTRACT");
        var value6 = input.LedgerHasFailed;
        var value8 = input.LedgerSteps;
        if (double.IsNaN(value8) || double.IsInfinity(value8) || value8 < 0d || value8 > 1000000d || System.Math.Truncate(value8) != value8 || System.Math.Abs(value8) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.steps", "LOGIC_INPUT_CONTRACT");
        var value9 = input.LedgerStepsNotOk;
        if (double.IsNaN(value9) || double.IsInfinity(value9) || value9 < 0d || value9 > 1000000d || System.Math.Truncate(value9) != value9 || System.Math.Abs(value9) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.stepsNotOk", "LOGIC_INPUT_CONTRACT");
        var value5 = input.LedgerTeardownOk;
        var value2 = input.ReportBlank;
        var value1 = input.ReportFormat;
        if (value1 is null || (value1 != "json" && value1 != "text")) throw new System.ArgumentOutOfRangeException("report.format", "LOGIC_INPUT_CONTRACT");
        var value0 = input.ReportPlane;
        if (value0 is null || (value0 != "server" && value0 != "editor")) throw new System.ArgumentOutOfRangeException("report.plane", "LOGIC_INPUT_CONTRACT");
        var value19 = input.TextHasAborted;
        var value17 = input.TextHasBlocked;
        var value16 = input.TextHasConsoleErr;
        var value15 = input.TextHasError;
        var value14 = input.TextHasFail;
        var value18 = input.TextHasTimeout;
        var value11 = input.TextHeader;
        if (value11 is null || (value11 != "none" && value11 != "first" && value11 != "later")) throw new System.ArgumentOutOfRangeException("text.header", "LOGIC_INPUT_CONTRACT");
        var value12 = input.TextPassed;
        if (double.IsNaN(value12) || double.IsInfinity(value12) || value12 < 0d || value12 > 1000000d || System.Math.Truncate(value12) != value12 || System.Math.Abs(value12) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.passed", "LOGIC_INPUT_CONTRACT");
        var value13 = input.TextTotal;
        if (double.IsNaN(value13) || double.IsInfinity(value13) || value13 < 0d || value13 > 1000000d || System.Math.Truncate(value13) != value13 || System.Math.Abs(value13) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.total", "LOGIC_INPUT_CONTRACT");
        return Decision_passes.Evaluate(value7, value6, value8, value9, value5, value2, value1, value0, value19, value17, value16, value15, value14, value18, value11, value12, value13);
    }
    public interface IOutcomeInputs { double LedgerFailed(); bool LedgerFailedMatchesSteps(); bool LedgerHasFailed(); bool LedgerReadable(); double LedgerSteps(); double LedgerStepsNotOk(); bool LedgerTeardownOk(); bool ReportBlank(); string ReportFormat(); string ReportPlane(); bool ReportZeroOfZero(); bool TextHasAborted(); bool TextHasBlocked(); bool TextHasConsoleErr(); bool TextHasError(); bool TextHasFail(); bool TextHasTimeout(); string TextHeader(); double TextPassed(); double TextTotal(); }
    public static class OutcomeValues { public const string Error = "error"; public const string Fail = "fail"; public const string Pass = "pass"; }
    public readonly struct OutcomeInput { public double LedgerFailed { get; } public bool LedgerFailedMatchesSteps { get; } public bool LedgerHasFailed { get; } public bool LedgerReadable { get; } public double LedgerSteps { get; } public double LedgerStepsNotOk { get; } public bool LedgerTeardownOk { get; } public bool ReportBlank { get; } public string ReportFormat { get; } public string ReportPlane { get; } public bool ReportZeroOfZero { get; } public bool TextHasAborted { get; } public bool TextHasBlocked { get; } public bool TextHasConsoleErr { get; } public bool TextHasError { get; } public bool TextHasFail { get; } public bool TextHasTimeout { get; } public string TextHeader { get; } public double TextPassed { get; } public double TextTotal { get; } public OutcomeInput(double LedgerFailed, bool LedgerFailedMatchesSteps, bool LedgerHasFailed, bool LedgerReadable, double LedgerSteps, double LedgerStepsNotOk, bool LedgerTeardownOk, bool ReportBlank, string ReportFormat, string ReportPlane, bool ReportZeroOfZero, bool TextHasAborted, bool TextHasBlocked, bool TextHasConsoleErr, bool TextHasError, bool TextHasFail, bool TextHasTimeout, string TextHeader, double TextPassed, double TextTotal) { this.LedgerFailed = LedgerFailed; this.LedgerFailedMatchesSteps = LedgerFailedMatchesSteps; this.LedgerHasFailed = LedgerHasFailed; this.LedgerReadable = LedgerReadable; this.LedgerSteps = LedgerSteps; this.LedgerStepsNotOk = LedgerStepsNotOk; this.LedgerTeardownOk = LedgerTeardownOk; this.ReportBlank = ReportBlank; this.ReportFormat = ReportFormat; this.ReportPlane = ReportPlane; this.ReportZeroOfZero = ReportZeroOfZero; this.TextHasAborted = TextHasAborted; this.TextHasBlocked = TextHasBlocked; this.TextHasConsoleErr = TextHasConsoleErr; this.TextHasError = TextHasError; this.TextHasFail = TextHasFail; this.TextHasTimeout = TextHasTimeout; this.TextHeader = TextHeader; this.TextPassed = TextPassed; this.TextTotal = TextTotal; } }
    public static string Outcome(IOutcomeInputs input) {
        var value7 = input.LedgerFailed();
        if (double.IsNaN(value7) || double.IsInfinity(value7) || System.Math.Truncate(value7) != value7 || System.Math.Abs(value7) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.failed", "LOGIC_INPUT_CONTRACT");
        var value10 = input.LedgerFailedMatchesSteps();
        var value6 = input.LedgerHasFailed();
        var value4 = input.LedgerReadable();
        var value8 = input.LedgerSteps();
        if (double.IsNaN(value8) || double.IsInfinity(value8) || value8 < 0d || value8 > 1000000d || System.Math.Truncate(value8) != value8 || System.Math.Abs(value8) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.steps", "LOGIC_INPUT_CONTRACT");
        var value9 = input.LedgerStepsNotOk();
        if (double.IsNaN(value9) || double.IsInfinity(value9) || value9 < 0d || value9 > 1000000d || System.Math.Truncate(value9) != value9 || System.Math.Abs(value9) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.stepsNotOk", "LOGIC_INPUT_CONTRACT");
        var value5 = input.LedgerTeardownOk();
        var value2 = input.ReportBlank();
        var value1 = input.ReportFormat();
        if (value1 is null || (value1 != "json" && value1 != "text")) throw new System.ArgumentOutOfRangeException("report.format", "LOGIC_INPUT_CONTRACT");
        var value0 = input.ReportPlane();
        if (value0 is null || (value0 != "server" && value0 != "editor")) throw new System.ArgumentOutOfRangeException("report.plane", "LOGIC_INPUT_CONTRACT");
        var value3 = input.ReportZeroOfZero();
        var value19 = input.TextHasAborted();
        var value17 = input.TextHasBlocked();
        var value16 = input.TextHasConsoleErr();
        var value15 = input.TextHasError();
        var value14 = input.TextHasFail();
        var value18 = input.TextHasTimeout();
        var value11 = input.TextHeader();
        if (value11 is null || (value11 != "none" && value11 != "first" && value11 != "later")) throw new System.ArgumentOutOfRangeException("text.header", "LOGIC_INPUT_CONTRACT");
        var value12 = input.TextPassed();
        if (double.IsNaN(value12) || double.IsInfinity(value12) || value12 < 0d || value12 > 1000000d || System.Math.Truncate(value12) != value12 || System.Math.Abs(value12) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.passed", "LOGIC_INPUT_CONTRACT");
        var value13 = input.TextTotal();
        if (double.IsNaN(value13) || double.IsInfinity(value13) || value13 < 0d || value13 > 1000000d || System.Math.Truncate(value13) != value13 || System.Math.Abs(value13) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.total", "LOGIC_INPUT_CONTRACT");
        return Decision_outcome.Evaluate(value7, value10, value6, value4, value8, value9, value5, value2, value1, value0, value3, value19, value17, value16, value15, value14, value18, value11, value12, value13);
    }
    public static string Outcome(OutcomeInput input) {
        var value7 = input.LedgerFailed;
        if (double.IsNaN(value7) || double.IsInfinity(value7) || System.Math.Truncate(value7) != value7 || System.Math.Abs(value7) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.failed", "LOGIC_INPUT_CONTRACT");
        var value10 = input.LedgerFailedMatchesSteps;
        var value6 = input.LedgerHasFailed;
        var value4 = input.LedgerReadable;
        var value8 = input.LedgerSteps;
        if (double.IsNaN(value8) || double.IsInfinity(value8) || value8 < 0d || value8 > 1000000d || System.Math.Truncate(value8) != value8 || System.Math.Abs(value8) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.steps", "LOGIC_INPUT_CONTRACT");
        var value9 = input.LedgerStepsNotOk;
        if (double.IsNaN(value9) || double.IsInfinity(value9) || value9 < 0d || value9 > 1000000d || System.Math.Truncate(value9) != value9 || System.Math.Abs(value9) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("ledger.stepsNotOk", "LOGIC_INPUT_CONTRACT");
        var value5 = input.LedgerTeardownOk;
        var value2 = input.ReportBlank;
        var value1 = input.ReportFormat;
        if (value1 is null || (value1 != "json" && value1 != "text")) throw new System.ArgumentOutOfRangeException("report.format", "LOGIC_INPUT_CONTRACT");
        var value0 = input.ReportPlane;
        if (value0 is null || (value0 != "server" && value0 != "editor")) throw new System.ArgumentOutOfRangeException("report.plane", "LOGIC_INPUT_CONTRACT");
        var value3 = input.ReportZeroOfZero;
        var value19 = input.TextHasAborted;
        var value17 = input.TextHasBlocked;
        var value16 = input.TextHasConsoleErr;
        var value15 = input.TextHasError;
        var value14 = input.TextHasFail;
        var value18 = input.TextHasTimeout;
        var value11 = input.TextHeader;
        if (value11 is null || (value11 != "none" && value11 != "first" && value11 != "later")) throw new System.ArgumentOutOfRangeException("text.header", "LOGIC_INPUT_CONTRACT");
        var value12 = input.TextPassed;
        if (double.IsNaN(value12) || double.IsInfinity(value12) || value12 < 0d || value12 > 1000000d || System.Math.Truncate(value12) != value12 || System.Math.Abs(value12) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.passed", "LOGIC_INPUT_CONTRACT");
        var value13 = input.TextTotal;
        if (double.IsNaN(value13) || double.IsInfinity(value13) || value13 < 0d || value13 > 1000000d || System.Math.Truncate(value13) != value13 || System.Math.Abs(value13) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("text.total", "LOGIC_INPUT_CONTRACT");
        return Decision_outcome.Evaluate(value7, value10, value6, value4, value8, value9, value5, value2, value1, value0, value3, value19, value17, value16, value15, value14, value18, value11, value12, value13);
    }
    public interface IFileStateInputs { bool FileErrorText(); double FileExpected(); double FilePassed(); }
    public static class FileStateValues { public const string Error = "error"; public const string Fail = "fail"; public const string NoVerdict = "no_verdict"; public const string Pass = "pass"; }
    public readonly struct FileStateInput { public bool FileErrorText { get; } public double FileExpected { get; } public double FilePassed { get; } public FileStateInput(bool FileErrorText, double FileExpected, double FilePassed) { this.FileErrorText = FileErrorText; this.FileExpected = FileExpected; this.FilePassed = FilePassed; } }
    public static string FileState(IFileStateInputs input) {
        var value22 = input.FileErrorText();
        var value21 = input.FileExpected();
        if (double.IsNaN(value21) || double.IsInfinity(value21) || value21 < 0d || value21 > 1000000d || System.Math.Truncate(value21) != value21 || System.Math.Abs(value21) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("file.expected", "LOGIC_INPUT_CONTRACT");
        var value20 = input.FilePassed();
        if (double.IsNaN(value20) || double.IsInfinity(value20) || value20 < 0d || value20 > 1000000d || System.Math.Truncate(value20) != value20 || System.Math.Abs(value20) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("file.passed", "LOGIC_INPUT_CONTRACT");
        return Decision_fileState.Evaluate(value22, value21, value20);
    }
    public static string FileState(FileStateInput input) {
        var value22 = input.FileErrorText;
        var value21 = input.FileExpected;
        if (double.IsNaN(value21) || double.IsInfinity(value21) || value21 < 0d || value21 > 1000000d || System.Math.Truncate(value21) != value21 || System.Math.Abs(value21) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("file.expected", "LOGIC_INPUT_CONTRACT");
        var value20 = input.FilePassed;
        if (double.IsNaN(value20) || double.IsInfinity(value20) || value20 < 0d || value20 > 1000000d || System.Math.Truncate(value20) != value20 || System.Math.Abs(value20) > 9007199254740991d) throw new System.ArgumentOutOfRangeException("file.passed", "LOGIC_INPUT_CONTRACT");
        return Decision_fileState.Evaluate(value22, value21, value20);
    }
    public interface ISuiteInnerInputs { bool SuiteAllNoVerdict(); bool SuiteAnyError(); bool SuiteAnyFail(); }
    public static class SuiteInnerValues { public const string INNERFAIL = "INNER_FAIL"; public const string INNERPASS = "INNER_PASS"; public const string NOVERDICT = "NO_VERDICT"; }
    public readonly struct SuiteInnerInput { public bool SuiteAllNoVerdict { get; } public bool SuiteAnyError { get; } public bool SuiteAnyFail { get; } public SuiteInnerInput(bool SuiteAllNoVerdict, bool SuiteAnyError, bool SuiteAnyFail) { this.SuiteAllNoVerdict = SuiteAllNoVerdict; this.SuiteAnyError = SuiteAnyError; this.SuiteAnyFail = SuiteAnyFail; } }
    public static string SuiteInner(ISuiteInnerInputs input) {
        var value25 = input.SuiteAllNoVerdict();
        var value24 = input.SuiteAnyError();
        var value23 = input.SuiteAnyFail();
        return Decision_suiteInner.Evaluate(value25, value24, value23);
    }
    public static string SuiteInner(SuiteInnerInput input) {
        var value25 = input.SuiteAllNoVerdict;
        var value24 = input.SuiteAnyError;
        var value23 = input.SuiteAnyFail;
        return Decision_suiteInner.Evaluate(value25, value24, value23);
    }
    public interface ISuiteOuterInputs { bool SuiteOuterFailed(); }
    public static class SuiteOuterValues { public const string OUTERFAIL = "OUTER_FAIL"; public const string OUTERPASS = "OUTER_PASS"; }
    public readonly struct SuiteOuterInput { public bool SuiteOuterFailed { get; } public SuiteOuterInput(bool SuiteOuterFailed) { this.SuiteOuterFailed = SuiteOuterFailed; } }
    public static string SuiteOuter(ISuiteOuterInputs input) {
        var value26 = input.SuiteOuterFailed();
        return Decision_suiteOuter.Evaluate(value26);
    }
    public static string SuiteOuter(SuiteOuterInput input) {
        var value26 = input.SuiteOuterFailed;
        return Decision_suiteOuter.Evaluate(value26);
    }
    public interface IInputs : IPassesInputs, IOutcomeInputs, IFileStateInputs, ISuiteInnerInputs, ISuiteOuterInputs { }

        private static class Decision_passes
        {
            public static bool Evaluate(double value7, bool value6, double value8, double value9, bool value5, bool value2, string value1, string value0, bool value19, bool value17, bool value16, bool value15, bool value14, bool value18, string value11, double value12, double value13)
            {
                // rule: blank (priority 10)
                if (value2 == true) return false;
                // rule: textAborted (priority 11)
                if ((value1 == "text") && (value19 == true)) return false;
                // rule: textNoHeader (priority 20)
                if ((value1 == "text") && (value11 == "none")) return false;
                // rule: textNoAssertions (priority 21)
                if ((value1 == "text") && (value13 == 0)) return false;
                // rule: textPartial (priority 22)
                if ((value1 == "text") && (value12 != value13)) return false;
                // rule: textFailureMarker (priority 23)
                if ((value1 == "text") && (value0 == "server") && ((value14 == true) || (value15 == true) || (value16 == true) || (value17 == true) || (value18 == true))) return false;
                // rule: textPass (priority 24)
                if (value1 == "text") return true;
                // rule: ledgerTeardown (priority 30)
                if (!(value5 == true)) return false;
                // rule: ledgerAggregate (priority 31)
                if ((!(value6 == true)) || (value7 != 0)) return false;
                // rule: ledgerEmpty (priority 32)
                if (value8 == 0) return false;
                // rule: ledgerBadStep (priority 33)
                if (value9 > 0) return false;
                // rule: ledgerPass (priority 34)
                if (value1 == "json") return true;
                return false;
            }
        }

        private static class Decision_outcome
        {
            public static string Evaluate(double value7, bool value10, bool value6, bool value4, double value8, double value9, bool value5, bool value2, string value1, string value0, bool value3, bool value19, bool value17, bool value16, bool value15, bool value14, bool value18, string value11, double value12, double value13)
            {
                // rule: pass (priority 10)
                if (Decision_passes.Evaluate(value7, value6, value8, value9, value5, value2, value1, value0, value19, value17, value16, value15, value14, value18, value11, value12, value13) == true) return "pass";
                // rule: nothing (priority 20)
                if ((value2 == true) || (value3 == true)) return "error";
                // rule: unreadable (priority 30)
                if ((value1 == "json") && (!(value4 == true))) return "error";
                // rule: noSteps (priority 31)
                if ((value1 == "json") && (value8 == 0)) return "error";
                // rule: contradicts (priority 32)
                if ((value1 == "json") && (!(value10 == true))) return "error";
                return "fail";
            }
        }

        private static class Decision_fileState
        {
            public static string Evaluate(bool value22, double value21, double value20)
            {
                // rule: noVerdict (priority 10)
                if (value21 == 0) return "no_verdict";
                // rule: passed (priority 20)
                if (value20 == value21) return "pass";
                // rule: errored (priority 30)
                if (value22 == true) return "error";
                return "fail";
            }
        }

        private static class Decision_suiteInner
        {
            public static string Evaluate(bool value25, bool value24, bool value23)
            {
                // rule: noVerdict (priority 10)
                if (value25 == true) return "NO_VERDICT";
                // rule: failed (priority 20)
                if ((value23 == true) || (value24 == true)) return "INNER_FAIL";
                return "INNER_PASS";
            }
        }

        private static class Decision_suiteOuter
        {
            public static string Evaluate(bool value26)
            {
                // rule: failed (priority 10)
                if (value26 == true) return "OUTER_FAIL";
                return "OUTER_PASS";
            }
        }

}
}
