using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Management.Automation.Subsystem.Prediction;
using System.Reflection;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void Inline_RenderSuggestion()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);
            _mockedMethods.ClearPredictionFields();

            // No matching history entry
            SetHistory("echo -bar", "eca -zoo");
            Test("a", Keys(
                'a', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                _.RightArrow, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'a')),
                CheckThat(() => AssertCursorLeftIs(1))
            ));

            // The 'OnXXXAccepted' callback won't be hit when 'History' is the only source.
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.Null(_mockedMethods.commandHistory);

            // Different matches as more input coming
            SetHistory("echo -bar", "eca -zoo");
            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.InlinePrediction, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.InlinePrediction, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.InlinePrediction, "o -bar")),
                // Once accepted, the suggestion text should be blanked out.
                _.Enter, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech"))
            ));

            // The 'OnXXXAccepted' callback won't be hit when 'History' is the only source.
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.Null(_mockedMethods.commandHistory);

            // Accept all or partial suggestion text with 'ForwardChar' and 'ForwardWord'
            SetHistory("echo -bar", "eca -zoo");
            Test("eca ", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.InlinePrediction, "ca -zoo")),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.InlinePrediction, "ca -zoo")),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.InlinePrediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.InlinePrediction, "-zoo"))
            ));

            // The 'OnXXXAccepted' callback won't be hit when 'History' is the only source.
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.Null(_mockedMethods.commandHistory);
        }

        [SkippableFact]
        public void Inline_CustomKeyBindingsToAcceptSuggestion()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Alt+g", PSConsoleReadLine.AcceptSuggestion),
                      new KeyHandler("Alt+f", PSConsoleReadLine.AcceptNextSuggestionWord));
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            SetHistory("echo -bar", "eca -zoo");
            Test("eca ", Keys(
                "ec", CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.InlinePrediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_g, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.InlinePrediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.InlinePrediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.InlinePrediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4)),

                // Revert back to 'ec'.
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(2)),
                // Move cursor to column 0.
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_g, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.InlinePrediction, "a -zoo")),
                CheckThat(() => AssertCursorLeftIs(2)),
                // Move cursor to column 0 again.
                _.LeftArrow, _.LeftArrow,
                CheckThat(() => AssertCursorLeftIs(0)),
                _.Alt_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "eca",
                        TokenClassification.None, ' ',
                        TokenClassification.InlinePrediction, "-zoo")),
                CheckThat(() => AssertCursorLeftIs(4))
            ));
        }

        [SkippableFact]
        public void Inline_AcceptNextSuggestionWordCanAcceptMoreThanOneWords()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord),
                      new KeyHandler("Alt+f", PSConsoleReadLine.AcceptNextSuggestionWord));
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            SetHistory("abc def ghi jkl");
            Test("abc def ghi jkl", Keys(
                'a', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.InlinePrediction, "bc def ghi jkl")),
                _.Alt_3, _.Ctrl_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi ",
                        TokenClassification.InlinePrediction, "jkl")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.InlinePrediction, "bc def ghi jkl")),
                _.Alt_3, _.Alt_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi ",
                        TokenClassification.InlinePrediction, "jkl")),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'a',
                        TokenClassification.InlinePrediction, "bc def ghi jkl")),
                _.Alt_8, _.Alt_f,
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "abc",
                        TokenClassification.None, " def ghi jkl"))
            ));
        }

        [SkippableFact]
        public void Inline_AcceptSuggestionWithSelection()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            SetHistory("git diff --cached", "git diff");
            Test("git diff", Keys(
                "git", CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.InlinePrediction, " diff")),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff")),
                _.Ctrl_z, CheckThat(() => AssertCursorLeftIs(3)),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff",
                        TokenClassification.InlinePrediction, " --cached")),

                // Perform visual selection and then accept suggestion.
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.InlinePrediction, " diff")),
                _.LeftArrow, _.Shift_RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(2, start);
                    Assert.Equal(1, length);
                }),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff")),

                // Perform visual selection and then accept next suggestion word.
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.InlinePrediction, " diff")),
                _.LeftArrow, _.Shift_RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(2, start);
                    Assert.Equal(1, length);
                }),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "git",
                        TokenClassification.None, " diff",
                        TokenClassification.InlinePrediction, " --cached"))
            ));
        }

        [SkippableFact]
        public void Inline_DisablePrediction()
        {
            TestSetup(KeyMode.Cmd);
            SetHistory("echo -bar", "eca -zoo");
            using var disp = SetPrediction(PredictionSource.None, PredictionViewStyle.InlineView);

            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, 'e')),
                'c', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ec")),
                'h', CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ech")),
                _.Enter, CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "ech"))
            ));
        }

        [SkippableFact]
        public void Inline_SetPredictionColor()
        {
            TestSetup(KeyMode.Cmd);
            var predictionColor = MakeCombinedColor(ConsoleColor.DarkYellow, ConsoleColor.Yellow);
            var predictionColorToCheck = Tuple.Create(ConsoleColor.DarkYellow, ConsoleColor.Yellow);

            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);
            PSConsoleReadLine.SetOptions(new SetPSReadLineOption { Colors = new Hashtable() { { "InlinePrediction", predictionColor } } });

            SetHistory("echo -bar", "eca -zoo");
            Test("ech", Keys(
                'e', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, 'e',
                        predictionColorToCheck, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, "ec",
                        predictionColorToCheck, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1, 
                        TokenClassification.Command, "ech",
                        predictionColorToCheck, "o -bar")),
                // Once accepted, the suggestion text should be blanked out.
                _.Enter, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech"))
            ));
        }

        [SkippableFact]
        public void Inline_HistoryEditsCanUndoProperly()
        {
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            SetHistory("git checkout -b branch origin/bbbb");
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            // Accept partial suggestion.
            Test("git checkout ", Keys(
                "git ch", CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " ch",
                    TokenClassification.InlinePrediction, "eckout -b branch origin/bbbb")),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout ",
                    TokenClassification.InlinePrediction, "-b branch origin/bbbb")),
                _.Enter, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout "))
            ));

            // Get the last command line from history, and revert the line.
            // 'RevertLine' will undo all edits of the history command.
            Test("", Keys(
                _.UpArrow, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " checkout ")),
                _.Escape));
        }

        [SkippableFact]
        public void Inline_AcceptSuggestionInVIMode()
        {
            TestSetup(KeyMode.Vi);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            SetHistory("echo -bar", "eca -zoo");
            Test("echo", Keys(
                'e', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, 'e',
                        TokenClassification.InlinePrediction, "ca -zoo")),
                'c', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ec",
                        TokenClassification.InlinePrediction, "a -zoo")),
                'h', CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.InlinePrediction, "o -bar")),
                CheckThat(() => AssertCursorLeftIs(3)),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "echo",
                        TokenClassification.None, ' ',
                        TokenClassification.Parameter, "-bar")),
                CheckThat(() => AssertCursorLeftIs(9)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech",
                        TokenClassification.InlinePrediction, "o -bar")),
                CheckThat(() => AssertCursorLeftIs(3)),

                // Suggestion should be cleared when switching to the command mode.
                _.Escape, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "ech")),
                CheckThat(() => AssertCursorLeftIs(2)),

                'i', _.RightArrow, 'o',
                CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "echo",
                        TokenClassification.InlinePrediction, " -bar")),
                CheckThat(() => AssertCursorLeftIs(4)),

                _.RightArrow, _.Escape,
                CheckThat(() => AssertCursorLeftIs(8)),
                _.Ctrl_z, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "echo")),
                CheckThat(() => AssertCursorLeftIs(3))
            ));
        }

        [SkippableFact]
        public void ViDefect2408()
        {
            TestSetup(KeyMode.Vi);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            SetHistory("echo -bar");
            Test("ech", Keys(
                "abcd",
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "abcd")),
                _.Escape, 'S',
                CheckThat(() => AssertLineIs(string.Empty)),
                CheckThat(() => AssertCursorLeftIs(0)),
                "ech",
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "ech",
                    TokenClassification.InlinePrediction, "o -bar")),
                _.RightArrow,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "echo",
                    TokenClassification.None, ' ',
                    TokenClassification.Parameter, "-bar")),
                CheckThat(() => AssertCursorLeftIs(9)),
                _.Ctrl_z,
                CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "ech",
                    TokenClassification.InlinePrediction, "o -bar"))));
        }

        private const uint MiniSessionId = 56;
        private static readonly Guid predictorId_1 = Guid.Parse("b45b5fbe-90fa-486c-9c87-e7940fdd6273");
        private static readonly Guid predictorId_2 = Guid.Parse("74a86463-033b-44a3-b386-41ee191c94be");

        /// <summary>
        /// Mocked implementation of 'PredictInput'.
        /// </summary>
        internal static List<PredictionResult> MockedPredictInput(Ast ast, Token[] tokens)
        {
            var ctor = typeof(PredictionResult).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(Guid), typeof(string), typeof(uint), typeof(List<PredictiveSuggestion>) }, null);

            var input = ast.Extent.Text;
            if (input == "netsh")
            {
                return null;
            }

            var suggestions_1 = new List<PredictiveSuggestion>
            {
                new PredictiveSuggestion($"SOME TEXT BEFORE {input}"),
                new PredictiveSuggestion($"{input} SOME TEXT AFTER"),
            };
            var suggestions_2 = new List<PredictiveSuggestion>
            {
                new PredictiveSuggestion($"SOME NEW TEXT"),
            };

            return new List<PredictionResult>
            {
                (PredictionResult)ctor.Invoke(
                    new object[] { predictorId_1, "TestPredictor", MiniSessionId, suggestions_1 }),
                (PredictionResult)ctor.Invoke(
                    new object[] { predictorId_2, "LongNamePredictor", MiniSessionId, suggestions_2 }),
            };
        }

        [SkippableFact]
        public void Inline_PluginSource_Acceptance()
        {
            // Using the 'Plugin' source will make PSReadLine get prediction from the plugin only.
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            using var disp = SetPrediction(PredictionSource.Plugin, PredictionViewStyle.InlineView);
            _mockedMethods.ClearPredictionFields();

            // When plugin returns any results, it will be used for inline view.
            SetHistory("git diff");
            Test("git SOME TEXT AFTER", Keys(
                "git", CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.InlinePrediction, " SOME TEXT AFTER")),
                // `OnSuggestionDisplayed` should be fired for only one predictor because we are in 'inline' view.
                CheckThat(() => AssertDisplayedSuggestions(count: 1, predictorId_1, MiniSessionId, -1)),
                CheckThat(() => _mockedMethods.ClearPredictionFields()),
                // 'ctrl+f' will trigger 'OnSuggestionAccepted'.
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME ",
                    TokenClassification.InlinePrediction, "TEXT AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(predictorId_1, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Equal("git SOME TEXT AFTER", _mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
                CheckThat(() => _mockedMethods.ClearPredictionFields()),
                // Subsequent 'ctrl+f' or 'rightarrow' on the same suggestion won't trigger 'OnSuggestionAccepted' again.
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME TEXT ",
                    TokenClassification.InlinePrediction, "AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME TEXT AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory))
            ));

            // 'Enter' will trigger 'OnCommandLineAccepted'.
            Assert.Empty(_mockedMethods.displayedSuggestions);
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.NotNull(_mockedMethods.commandHistory);
            Assert.Equal(2, _mockedMethods.commandHistory.Count);
            Assert.Equal("git diff", _mockedMethods.commandHistory[0]);
            Assert.Equal("git SOME TEXT AFTER", _mockedMethods.commandHistory[1]);

            // When plugin doesn't return any results, no suggestion will be shown for inline view because history is not used.
            // The mocked implementation of 'PredictInput' treats 'netsh' as the hint to return nothing.
            _mockedMethods.ClearPredictionFields();
            SetHistory("netsh show me");
            Test("netsh", Keys(
                "netsh", CheckThat(() => AssertScreenIs(1, TokenClassification.Command, "netsh"))
            ));

            // 'Enter' will trigger 'OnCommandLineAccepted', because plugin is in use.
            // Also, we still have `OnSuggestionDisplayed` fired, from the typing of each character of `nets`.
            AssertDisplayedSuggestions(count: 1, predictorId_1, MiniSessionId, -1);
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.NotNull(_mockedMethods.commandHistory);
            Assert.Equal(2, _mockedMethods.commandHistory.Count);
            Assert.Equal("netsh show me", _mockedMethods.commandHistory[0]);
            Assert.Equal("netsh", _mockedMethods.commandHistory[1]);
        }

        [SkippableFact]
        public void Inline_HistoryAndPluginSource_Acceptance()
        {
            // Using the 'HistoryAndPlugin' source will make PSReadLine get prediction from the plugin and history,
            // and plugin takes precedence.
            TestSetup(KeyMode.Cmd,
                      new KeyHandler("Ctrl+f", PSConsoleReadLine.ForwardWord));
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.InlineView);
            _mockedMethods.ClearPredictionFields();

            // When plugin returns any results, it will be used for inline view.
            SetHistory("git diff");
            Test("git SOME TEXT AFTER", Keys(
                "git", CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.InlinePrediction, " SOME TEXT AFTER")),
                // `OnSuggestionDisplayed` should be fired for only one predictor because we are in 'inline' view.
                CheckThat(() => AssertDisplayedSuggestions(count: 1, predictorId_1, MiniSessionId, -1)),
                CheckThat(() => _mockedMethods.ClearPredictionFields()),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME ",
                    TokenClassification.InlinePrediction, "TEXT AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(predictorId_1, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Equal("git SOME TEXT AFTER", _mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
                CheckThat(() => _mockedMethods.ClearPredictionFields()),
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME TEXT ",
                    TokenClassification.InlinePrediction, "AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "git",
                    TokenClassification.None, " SOME TEXT AFTER")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory))
            ));

            Assert.Empty(_mockedMethods.displayedSuggestions);
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.NotNull(_mockedMethods.commandHistory);
            Assert.Equal(2, _mockedMethods.commandHistory.Count);
            Assert.Equal("git diff", _mockedMethods.commandHistory[0]);
            Assert.Equal("git SOME TEXT AFTER", _mockedMethods.commandHistory[1]);

            // When plugin doesn't return any results, history will be used for inline view.
            // The mocked implementation of 'PredictInput' treats 'netsh' as the hint to return nothing.
            _mockedMethods.ClearPredictionFields();
            SetHistory("netsh show me");
            Test("netsh show me", Keys(
                "netsh", CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "netsh",
                    TokenClassification.InlinePrediction, " show me")),
                // Yeah, we still have `OnSuggestionDisplayed` fired, from the typing of each character of `nets`.
                CheckThat(() => AssertDisplayedSuggestions(count: 1, predictorId_1, MiniSessionId, -1)),
                CheckThat(() => _mockedMethods.ClearPredictionFields()),
                // 'ctrl+f' won't trigger 'OnSuggestionAccepted' as the suggestion is from history.
                _.Ctrl_f, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "netsh",
                    TokenClassification.None, " show ",
                    TokenClassification.InlinePrediction, "me")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory)),
                // 'rightarrow' won't trigger 'OnSuggestionAccepted' as the suggestion is from history.
                _.RightArrow, CheckThat(() => AssertScreenIs(1,
                    TokenClassification.Command, "netsh",
                    TokenClassification.None, " show me")),
                CheckThat(() => Assert.Empty(_mockedMethods.displayedSuggestions)),
                CheckThat(() => Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId)),
                CheckThat(() => Assert.Null(_mockedMethods.acceptedSuggestion)),
                CheckThat(() => Assert.Null(_mockedMethods.commandHistory))
            ));

            Assert.Empty(_mockedMethods.displayedSuggestions);
            Assert.Equal(Guid.Empty, _mockedMethods.acceptedPredictorId);
            Assert.Null(_mockedMethods.acceptedSuggestion);
            Assert.NotNull(_mockedMethods.commandHistory);
            Assert.Equal(1, _mockedMethods.commandHistory.Count);
            Assert.Equal("netsh show me", _mockedMethods.commandHistory[0]);
        }

        [SkippableFact]
        public void Inline_NoneSource_ExecutionStatus()
        {
            TestSetup(KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.None, PredictionViewStyle.InlineView);

            // The last accepted command line would be "yay" after this.
            Test("yay", Keys("yay"));
            _mockedMethods.ClearPredictionFields();

            // We always pass in 'true' as the execution status of the last command line,
            // and that feedback will be reported when
            //  1. the plugin source is in use;
            //  2. the last accepted command is not a whitespace string.
            Test("   ", Keys(
                // Since we set the prediction source to be 'None', this feedback won't be reported.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "   "));

            Test("abc", Keys(
                // The prediction source is 'None', and the last accepted command is a whitespace string,
                // so this feedback won't be reported.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "abc"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);
        }

        [SkippableFact]
        public void Inline_HistorySource_ExecutionStatus()
        {
            TestSetup(KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            // The last accepted command line would be "yay" after this.
            Test("yay", Keys("yay"));
            _mockedMethods.ClearPredictionFields();

            // We always pass in 'true' as the execution status of the last command line,
            // and that feedback will be reported when
            //  1. the plugin source is in use;
            //  2. the last accepted command is not a whitespace string.
            Test("   ", Keys(
                // Since we set the prediction source to be 'History', this feedback won't be reported.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "   "));

            Test("abc", Keys(
                // The plugin source is in use, but the last accepted command is a whitespace string.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "abc"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);
        }

        [SkippableFact]
        public void Inline_PluginSource_ExecutionStatus()
        {
            TestSetup(KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.Plugin, PredictionViewStyle.InlineView);

            // The last accepted command line would be an empty string after this.
            Test("", Keys(_.Enter));
            _mockedMethods.ClearPredictionFields();

            // We always pass in 'true' as the execution status of the last command line,
            // and that feedback will be reported when
            //  1. the plugin source is in use;
            //  2. the last accepted command is not a whitespace string.
            Test("yay", Keys(
                // The plugin source is in use, but the last accepted command is an empty string.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "yay"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);

            // The last accepted command line would be a whitespace string with 3 space characters after this.
            Test("   ", Keys(
                // The plugin source is in use, and the last accepted command is "yay".
                CheckThat(() => Assert.True(_mockedMethods.lastCommandRunStatus)),
                "   "));

            Assert.True(_mockedMethods.lastCommandRunStatus);
            _mockedMethods.ClearPredictionFields();

            Test("abc", Keys(
                // The plugin source is in use, but the last accepted command is a whitespace string.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "abc"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);
        }

        [SkippableFact]
        public void Inline_HistoryAndPluginSource_ExecutionStatus()
        {
            TestSetup(KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.HistoryAndPlugin, PredictionViewStyle.InlineView);

            // The last accepted command line would be an empty string after this.
            Test("", Keys(_.Enter));
            _mockedMethods.ClearPredictionFields();

            // We always pass in 'true' as the execution status of the last command line,
            // and that feedback will be reported when
            //  1. the plugin source is in use;
            //  2. the last accepted command is not a whitespace string.
            Test("yay", Keys(
                // The plugin source is in use, but the last accepted command is an empty string.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "yay"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);

            // The last accepted command line would be a whitespace string with 3 space characters after this.
            Test("   ", Keys(
                // The plugin source is in use, and the last accepted command is "yay".
                CheckThat(() => Assert.True(_mockedMethods.lastCommandRunStatus)),
                "   "));

            Assert.True(_mockedMethods.lastCommandRunStatus);
            _mockedMethods.ClearPredictionFields();

            Test("abc", Keys(
                // The plugin source is in use, but the last accepted command is a whitespace string.
                CheckThat(() => Assert.Null(_mockedMethods.lastCommandRunStatus)),
                "abc"));

            Assert.Null(_mockedMethods.lastCommandRunStatus);
        }

        [SkippableFact]
        public void Inline_TruncateVeryLongSuggestion()
        {
            TestSetup(new TestConsole(width: 10, height: 2, keyboardLayout: _), KeyMode.Cmd);
            using var disp = SetPrediction(PredictionSource.History, PredictionViewStyle.InlineView);

            // Truncate long suggestion to make sure the user input is not scrolled up-off the console buffer.
            SetHistory(new string('v', 25));
            Test("vv", Keys(
                'v', CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, 'v',
                        TokenClassification.InlinePrediction, new string('v', 9),
                        TokenClassification.InlinePrediction, new string('v', 6) + "...")),
                'v', CheckThat(() => AssertScreenIs(2,
                        TokenClassification.Command, "vv",
                        TokenClassification.InlinePrediction, new string('v', 8),
                        TokenClassification.InlinePrediction, new string('v', 6) + "...")),
                // Once accepted, the suggestion text should be blanked out.
                _.Enter, CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "vv"))
            ));
        }
    }
}
