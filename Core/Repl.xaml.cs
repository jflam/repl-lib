using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;
using IronRuby.Builtins;

namespace Core {
    public partial class Repl : UserControl {
        private Dictionary<string, DlrEngine> _languageMap = new Dictionary<string, DlrEngine>();
        private string _currentEngine;
        private DlrEngine _rubyEngine; // always need a reference to this for our output rendering extensions

        public DlrEngine CurrentEngine {
            get { return _languageMap[_currentEngine]; }
        }

        public Repl() {
            InitializeComponent();
            LoadResources();
            InitializeScriptEngines();
            InitializePlugins();
        }

        private void LoadResources() {
            // TODO: load embedded ReplResources.xaml and write to disk if we don't have one in our current dir
            var resourceDictionary = (ResourceDictionary)XamlReader.Load(XmlReader.Create("ReplResources.xaml"));
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
        }

        private void InitializePlugins() {
            // TODO: remove hard-coded path
            CurrentEngine.Require(@"c:\teched\repl\Viewers\default.viewer.rb");
        }

        private void InitializeScriptEngines() {
            DlrEngine engine = new RubyEngine();
            _languageMap[engine.Name] = engine;
            _rubyEngine = engine;
            engine = new PythonEngine();
            _languageMap[engine.Name] = engine;
            _currentEngine = "ruby";
        }

        private void InsertTextBlock(List<Run> runs) {
            var block = new TextBlock();
            block.Inlines.AddRange(runs);
            
            var pos = MainRepl.CaretPosition.Paragraph;
            pos.Inlines.Add(new LineBreak());
            pos.Inlines.AddRange(runs);
        }

        private void InsertColorizedCode(string code) {
            InsertTextBlock(Colorizer.Colorize(CurrentEngine, code, null));
        }

        private void RenderError(string error) {
            InsertTextBlock(Colorizer.ColorizeErrors(error));
        }

        private void RenderInput(string code) {
            var pos = MainRepl.CaretPosition.Paragraph;
            var selected_text = MainRepl.Selection.Text;
            MainRepl.Selection.Text = String.Empty;
            var blocks = Colorizer.Colorize(CurrentEngine, code, null);
            pos.Inlines.AddRange(blocks);
        }

        private Dictionary<Type, bool> _viewers = new Dictionary<Type, bool>();

        private void LoadInspector(object obj) {
            if (obj != null) {
                var type = obj.GetType();
                if (!_viewers.ContainsKey(type)) {
                    string className = obj.GetType().FullName;

                    // TODO: exclusions and config
                    foreach (var extension in CurrentEngine.GetFileExtensions()) {
                        string viewerPath = String.Format(@"c:\dev\repl\Viewers\{0}.viewer{1}", className, extension);
                        if (File.Exists(viewerPath)) {
                            CurrentEngine.Require(viewerPath);
                        }
                    }
                }
            }
        }

        public void InsertInspectedResult(object obj) {
            LoadInspector(obj);
            object result = _rubyEngine.InvokeMember(obj, "as_xaml");
            if (result is MutableString) {
                InsertColorizedCode(result.ToString());
            } else {
                MainRepl.CaretPosition.Paragraph.Inlines.Add((UIElement)result);
            }
        }

        public void AddExternalObject(string name, object obj) {
            CurrentEngine.SetVariable(name, obj);
        }

        private void RenderOutput(object result) {
            var output = CurrentEngine.ReadStandardOutput();
            if (output != null)
                InsertColorizedCode(output.TrimEnd());
            InsertInspectedResult(result);
        }

        private void RenderCode(TextRange selection, string code) {
        }

        private object Execute(string code) {
            if (code.StartsWith("%")) {
                var pos = code.IndexOf('%');
                var language = code.Substring(pos + 1).Trim();
                if (_languageMap.ContainsKey(language)) {
                    _currentEngine = language;
                } else {
                    RenderOutput("uh oh - don't know that language");
                }
                return null;
            } else {
                return CurrentEngine.Execute(code);
            }
        }

        private void MainRepl_PreviewKeyDown(object sender, KeyEventArgs args) {
            if (args.IsCtrl(Key.E)) {
                try {
                    var code = MainRepl.Selection.Text;
                    RenderInput(code);
                    RenderOutput(Execute(code));
                    args.Handled = true;
                } catch (Exception e) {
                    RenderError(e.Message);
                }
            } else if (Keyboard.Modifiers == ModifierKeys.None && args.Key == Key.Return) {
                if (!MainRepl.CaretPosition.IsAtLineStartPosition) {
                    var pos = MainRepl.CaretPosition.InsertLineBreak();
                    MainRepl.CaretPosition = pos.GetNextContextPosition(LogicalDirection.Forward);
                    args.Handled = true;
                } else {
                    // remove the last line break in the paragraph's inlines collection
                    var current_run = MainRepl.CaretPosition.Parent as Run;
                    if (current_run != null) {
                        var last_line_break = ((Run)current_run).PreviousInline;
                        MainRepl.CaretPosition.Paragraph.Inlines.Remove(last_line_break);
                    }
                    MainRepl.CaretPosition.InsertParagraphBreak();
                }
            } else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.S) {
                using (var stream = File.OpenWrite(@"c:\temp\output.xml")) {
                    XamlWriter.Save(MainRepl.Document, stream);
                }
            } else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.Return) {
                var selection = new TextRange(MainRepl.CaretPosition.GetLineStartPosition(0), MainRepl.CaretPosition);
                var code = selection.Text;
                // TODO: replace the selection with formatted code
                RenderOutput(Execute(code));
            } else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.Space) {
                // change the style of the current Run that we are under to be the default text style
                var pos = MainRepl.CaretPosition;
                var run = pos.Parent as Run;
                var style = (Style)Application.Current.FindResource("None");
                if (run != null) {
                    run.Style = style;
                }
            }
        }
    }

    #region Extension Methods

    public static class ExtensionMethods {
        public static bool IsCtrl(this KeyEventArgs keyEvent, Key value) {
            return keyEvent.KeyboardDevice.Modifiers == ModifierKeys.Control
                && keyEvent.Key == value;
        }

        public static bool IsCtrlShift(this KeyEventArgs keyEvent, Key value) {
            return keyEvent.KeyboardDevice.Modifiers == ModifierKeys.Control
                && keyEvent.KeyboardDevice.Modifiers == ModifierKeys.Shift
                && keyEvent.Key == value;
        }
    }

    #endregion
}
