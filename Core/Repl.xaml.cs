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
        private Dictionary<Type, bool> _viewers = new Dictionary<Type, bool>();
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

        #region Component initialization 

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

        #endregion 

        #region deprecated code

        //private void InsertTextBlock(List<Run> runs) {
        //    var block = new TextBlock();
        //    block.Inlines.AddRange(runs);

        //    var pos = MainRepl.CaretPosition.Paragraph;
        //    pos.Inlines.Add(new LineBreak());
        //    pos.Inlines.AddRange(runs);
        //}

        //private void RenderInput(string code) {
        //    var pos = MainRepl.CaretPosition.Paragraph;
        //    var selected_text = MainRepl.Selection.Text;
        //    MainRepl.Selection.Text = String.Empty;
        //    var blocks = Colorizer.Colorize(CurrentEngine, code, null);
        //    pos.Inlines.AddRange(blocks);
        //}

        #endregion

        private object Execute(string code) {
            if (code.StartsWith("%")) {
                var pos = code.IndexOf('%');
                var language = code.Substring(pos + 1).Trim();
                if (_languageMap.ContainsKey(language)) {
                    _currentEngine = language;
                } else {
                    throw new ApplicationException("Unknown language requested: " + language);
                }
                return "Switched to " + language;
            } else {
                return CurrentEngine.Execute(code);
            }
        }

        private Inline GetInlineUnderPosition(TextPointer position) {
            var result = position.Parent as Inline;
            if (result == null)
                throw new ApplicationException("text pointer is not pointing to an Inline??!!");
            return result;
        }

        private Paragraph GetParagraph(Inline position) {
            var paragraph = position.Parent as Paragraph;
            if (paragraph == null)
                throw new ApplicationException("is it possible for a Run to not have a Paragraph as a perent??");
            return paragraph;
        }

        private Inline InsertInline(Inline position, Inline element) {
            var paragraph = GetParagraph(position);
            paragraph.Inlines.InsertAfter(position, element);
            return element;
        }

        private Inline InsertLineBreak(Inline position) {
            return InsertInline(position, new LineBreak());
        }

        private Inline InsertElements(Inline position, List<Run> runs) {
            var paragraph = GetParagraph(position);
            var end = runs[runs.Count - 1];
            for (int i = runs.Count - 1; i >= 0; i--) {
                paragraph.Inlines.InsertAfter(position, runs[i]);
            }
            return end;
        }

        private Inline InsertColorizedCode(Inline position, string code) {
            return InsertElements(position, Colorizer.Colorize(CurrentEngine, code, null));
        }

        private Inline RenderError(Inline position, string error) {
            return InsertElements(position, Colorizer.ColorizeErrors(error));
        }

        private Inline ColorizeSelection(TextRange selection) {
            var position = GetInlineUnderPosition(selection.End);
            var code = selection.Text;
            return InsertColorizedCode(position, code);
        }

        public Inline InsertInspectedResult(Inline position, object obj) {
            LoadInspector(obj);
            object result = _rubyEngine.InvokeMember(obj, "as_xaml");
            if (result is MutableString) {
                return InsertColorizedCode(position, result.ToString());
            } else {
                throw new NotImplementedError("do not have mechanism to insert UIElement in middle of flow document yet");
            }
        }

        private Inline RenderOutput(Inline position, object result) {
            var output = CurrentEngine.ReadStandardOutput();
            return output != null ? InsertColorizedCode(position, output.TrimEnd())
                                  : InsertInspectedResult(position, result);
        }

        private void MainRepl_PreviewKeyDown(object sender, KeyEventArgs args) {
            if (args.IsCtrl(Key.E)) {
                RunSelection(MainRepl.Selection);
                args.Handled = true;
            } else if (Keyboard.Modifiers == ModifierKeys.None && args.Key == Key.Return) {
                InsertSmartLineBreak();
                args.Handled = true;
            } else if (args.IsCtrl(Key.S)) {
                SaveDocument();
                args.Handled = true;
            } else if (args.IsCtrl(Key.Return)) {
                RunCurrentLine();
                args.Handled = true;
            } else if (args.IsCtrl(Key.Space)) {
                ChangeRunUnderCursorToDefaultTextStyle();
                args.Handled = true;
            }
        }

        private void SaveDocument() {
            using (var stream = File.OpenWrite(@"c:\temp\output.xml")) {
                XamlWriter.Save(MainRepl.Document, stream);
            }
        }

        private Inline InsertSmartLineBreak() {
            var position = GetInlineUnderPosition(MainRepl.CaretPosition);
            var run1 = InsertLineBreak(position);
            var run2 = InsertInline(run1, new Run(String.Empty));
            MainRepl.CaretPosition = run2.ElementStart;
            return run2;
        }

        private Inline RunSelection(TextRange selection) {
            try {
                var code = selection.Text;
                var run1 = ColorizeSelection(selection);
                var run2 = InsertLineBreak(run1);
                var run3 = RenderOutput(run2, Execute(code));
                var run4 = InsertLineBreak(run3);
                var run5 = InsertInline(run4, new Run(String.Empty));
                selection.Text = String.Empty;
                MainRepl.CaretPosition = run5.ElementStart;
                return run5;
            } catch (Exception e) {
                return RenderError(GetInlineUnderPosition(MainRepl.Selection.End), e.Message);
            } 
        }

        private Inline RunCurrentLine() {
            var selection = new TextRange(MainRepl.CaretPosition.GetLineStartPosition(0), MainRepl.CaretPosition);
            return RunSelection(selection);
        }

        private void ChangeRunUnderCursorToDefaultTextStyle() {
            var run = GetInlineUnderPosition(MainRepl.CaretPosition);
            run.Style = (Style)Application.Current.FindResource("None");
        }

        public void AddExternalObject(string name, object obj) {
            CurrentEngine.SetVariable(name, obj);
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
