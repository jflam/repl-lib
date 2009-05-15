using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Microsoft.Scripting;

namespace Core {
    public static class Colorizer {
        private static Dictionary<TokenCategory, string> _colorizationStyles = new Dictionary<TokenCategory, string>() {
            { TokenCategory.NumericLiteral, "NumericLiteral" },
            { TokenCategory.Keyword, "Keyword" },
            { TokenCategory.Identifier, "Identifier" },
            { TokenCategory.StringLiteral, "StringLiteral" },
            { TokenCategory.Comment, "Comment" },
            { TokenCategory.Error, "Error" },
            { TokenCategory.None, "None" },
        };

        private static Run CreateTextRun(string code, TokenInfo token) {
            var text = code.Substring(token.SourceSpan.Start.Index, token.SourceSpan.Length);
            var result = new Run(text);
            var style = _colorizationStyles.ContainsKey(token.Category) ? _colorizationStyles[token.Category] : _colorizationStyles[TokenCategory.None];
            result.Style = Application.Current.FindResource(style) as Style;
            return result;
        }

        private static Run CreateLeadingWhitespaceRun(string code, int position, TokenInfo token) {
            var text = code.Substring(position, token.SourceSpan.Start.Index - position);
            return new Run(text);
        }

        public static List<Run> Colorize(DlrEngine engine, string code, Action<Run, TokenInfo> proc) {
            var result = new List<Run>();
            int position = 0;
            foreach (TokenInfo token in engine.GetTokenInfos(code)) {
                result.Add(CreateLeadingWhitespaceRun(code, position, token));
                var run = CreateTextRun(code, token);
                if (proc != null)
                    proc(run, token);
                result.Add(run);
                position = token.SourceSpan.Start.Index + token.SourceSpan.Length;
            }
            return result;
        }

        public static List<Run> ColorizeErrors(string error) {
            var result = new List<Run>();
            var run = new Run(error);
            run.Style = Application.Current.FindResource("Error") as Style;
            result.Add(run);
            return result;
        }
    }
}