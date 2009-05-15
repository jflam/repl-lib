using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IronPython.Runtime;
using IronRuby;
using IronRuby.Builtins;
using IronRuby.Compiler;
using IronRuby.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;

namespace Core {

    #region Abstract class for targeting DLR hosting interfaces

    public abstract class DlrEngine {
        protected ScriptEngine _engine;
        protected ScriptScope _scope;
        protected MemoryStream _outputStream;
        protected CompilerOptions _compilerOptions;

        protected virtual void ResetOutputStream() {
            _outputStream = new MemoryStream();
            _engine.Runtime.IO.SetOutput(_outputStream, Encoding.UTF8);
        }

        protected ScriptSource CreateScriptSourceFromString(string code) {
            return _engine.CreateScriptSourceFromString(code, SourceCodeKind.InteractiveCode);
        }

        protected SourceUnit CreateSourceUnit(string code) {
            var context = HostingHelpers.GetLanguageContext(_engine);
            return context.CreateSnippet(code, SourceCodeKind.InteractiveCode);
        }

        public virtual ScriptScope CreateScriptScope() {
            return _engine.Runtime.CreateScope();
        }

        public abstract void Reset(ScriptScope scope);

        public virtual object Execute(string code) {
            var scriptSource = CreateScriptSourceFromString(code);
            var compiledCode = scriptSource.Compile();

            if (_scope == null) {
                return compiledCode.Execute();
            } else {
                var result = compiledCode.Execute(_scope);
                _scope.SetVariable("_", result);
                return result;
            }
        }

        public abstract object InvokeMember(object target, string method);

        public virtual List<TokenInfo> GetTokenInfos(string code) {
            var tokenizer = new Tokenizer(true);
            tokenizer.Initialize(CreateSourceUnit(code));
            return new List<TokenInfo>(tokenizer.ReadTokens(code.Length));
        }

        public abstract bool Require(string module);

        public virtual string ReadStandardOutput() {
            if (_outputStream.Position == 0)
                return null;

            _outputStream.Seek(0, SeekOrigin.Begin);
            StreamReader reader = new StreamReader(_outputStream, Encoding.UTF8);
            var result = reader.ReadToEnd();
            ResetOutputStream();
            return result;
        }

        public virtual string GetFileExtension() {
            return _engine.Runtime.Setup.LanguageSetups[0].FileExtensions[0];
        }

        public virtual IList<string> GetFileExtensions() {
            // HACK
            return _engine.Runtime.Setup.LanguageSetups[0].FileExtensions;
        }

        public virtual void SetVariable(string name, object value) {
            _scope.SetVariable(name, value);
        }

        public abstract string Name { get; }
    }

    #endregion

    #region Concrete classes for targeting specific DLR languages

    public class PythonEngine : DlrEngine {
        public PythonEngine() : this(null) { }

        public PythonEngine(ScriptScope scriptScope) {
            Reset(scriptScope);
            ResetOutputStream();
        }

        public override void Reset(ScriptScope scope) {
            var setup = new ScriptRuntimeSetup();
            var ls = new LanguageSetup(typeof(PythonContext).AssemblyQualifiedName, "Python", new[] { "py" }, new[] { ".py" });
            setup.LanguageSetups.Add(ls);
            var runtime = new ScriptRuntime(setup);
            _engine = runtime.GetEngine("py");
            _scope = scope == null ? _engine.Runtime.CreateScope() : scope;
        }

        public override bool Require(string module) {
            throw new NotImplementedException();
        }

        public override object InvokeMember(object target, string method) {
            throw new NotImplementedException();
        }

        public override string Name {
            get { return "python"; }
        }
    }

    // TODO: redesign this to accept config via a delegate
    public class RubyEngine : DlrEngine {
        private const string BasePath = @"C:\dev\ironruby\Merlin\External.LCA_RESTRICTED\Languages\Ruby\redist-libs";
        private const string MerlinPath = @"c:\dev\ironruby\merlin\main\languages\ruby";
        private IronRuby.Builtins.Binding _topLevelBinding;

        public RubyEngine() : this(null) { }

        public RubyEngine(ScriptScope scriptScope) {
            Reset(scriptScope);
            ResetOutputStream();
        }

        public override void Reset(ScriptScope scope) {
            _engine = Ruby.CreateEngine((setup) =>
            {
                setup.Options["InterpretedMode"] = true;
                setup.Options["SearchPaths"] = new[] { MerlinPath + @"\libs", BasePath + @"\ruby\site_ruby\1.8", BasePath + @"\ruby\site_ruby", BasePath + @"\ruby\1.8" };
            });

            _scope = scope == null ? _engine.Runtime.CreateScope() : scope;
            _topLevelBinding = (IronRuby.Builtins.Binding)_engine.Execute("binding", _scope);
        }

        public override object Execute(string code) {
            return RubyUtils.Evaluate(MutableString.Create(code), _topLevelBinding.LocalScope, _topLevelBinding.LocalScope.SelfObject, null, null, 0);
        }

        public override object InvokeMember(object target, string method) {
            return _engine.Operations.InvokeMember(target, method);
        }

        public override bool Require(string module) {
            return _engine.RequireRubyFile(module);
        }

        public override void SetVariable(string name, object value) {
            _engine.Runtime.Globals.SetVariable(name, value);
        }

        public override string Name {
            get { return "ruby"; }
        }
    }

    #endregion
}