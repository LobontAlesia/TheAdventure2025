using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TheAdventure.Scripting;

public class ScriptEngine : IDisposable
{
    private PortableExecutableReference[] _scriptReferences;
    private Dictionary<string, (IScript Script, Assembly Assembly)> _scripts = new Dictionary<string, (IScript, Assembly)>();
    private FileSystemWatcher? _watcher;
    private List<WeakReference> _loadedAssemblies = new List<WeakReference>();

    public ScriptEngine()
    {
        var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location) +
                     Path.DirectorySeparatorChar;
        var references = new string[]
        {
            #region .Net SDK

            rtPath + "System.Private.CoreLib.dll",
            rtPath + "System.Runtime.dll",
            rtPath + "System.Console.dll",
            rtPath + "netstandard.dll",
            rtPath + "System.Text.RegularExpressions.dll", 
            rtPath + "System.Linq.dll",
            rtPath + "System.Linq.Expressions.dll", 
            rtPath + "System.IO.dll",
            rtPath + "System.Net.Primitives.dll",
            rtPath + "System.Net.Http.dll",
            rtPath + "System.Private.Uri.dll",
            rtPath + "System.Reflection.dll",
            rtPath + "System.ComponentModel.Primitives.dll",
            rtPath + "System.Globalization.dll",
            rtPath + "System.Collections.Concurrent.dll",
            rtPath + "System.Collections.NonGeneric.dll",
            rtPath + "Microsoft.CSharp.dll",

            #endregion
            
            typeof(IScript).Assembly.Location
        };
        _scriptReferences = references.Select(x => MetadataReference.CreateFromFile(x)).ToArray();
    }

    public void ExecuteAll(Engine engine)
    {
        foreach (var script in _scripts)
        {
            script.Value.Script.Execute(engine);
        }
    }

    public void UnloadAll()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnScriptChanged;
            _watcher.Deleted -= OnScriptChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        foreach (var script in _scripts.Values)
        {
            if (script.Script is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        _scripts.Clear();
        
        // Force cleanup of loaded assemblies
        for (int i = _loadedAssemblies.Count - 1; i >= 0; i--)
        {
            if (!_loadedAssemblies[i].IsAlive)
            {
                _loadedAssemblies.RemoveAt(i);
            }
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private void AttachWatcher(string path)
    {
        _watcher = new FileSystemWatcher(path);
        _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.LastAccess |
                                NotifyFilters.Size;
        _watcher.Changed += OnScriptChanged;
        _watcher.Deleted += OnScriptChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnScriptChanged(object source, FileSystemEventArgs e)
    {
        if (!_scripts.ContainsKey(e.FullPath))
        {
            return;
        }
        
        Console.WriteLine($"Change detected for: {e.FullPath}");
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Changed:
                _scripts.Remove(e.FullPath, out _);
                Load(e.FullPath);
                break;
            case WatcherChangeTypes.Deleted:
                _scripts.Remove(e.FullPath, out _);
                break;
        }
    }

    private IScript? Load(string file)
    {
        Console.WriteLine($"Loading script {file}");
        FileInfo fileInfo = new FileInfo(file);
        var fileOutput = fileInfo.FullName.Replace(fileInfo.Extension, ".dll");
        
        // Delete existing DLL if it exists to avoid file locks
        if (File.Exists(fileOutput))
        {
            try
            {
                File.Delete(fileOutput);
            }
            catch (IOException)
            {
                // If file is locked, generate a new unique filename
                fileOutput = fileInfo.FullName.Replace(fileInfo.Extension, $"_{Guid.NewGuid()}.dll");
            }
        }

        var code = File.ReadAllText(fileInfo.FullName);
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            fileInfo.Name.Replace(fileInfo.Extension, string.Empty),
            new[] { syntaxTree },
            _scriptReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using (var compiledScriptAssembly = new FileStream(fileOutput, FileMode.Create))
        {
            var result = compilation.Emit(compiledScriptAssembly);
            if (!result.Success)
            {
                foreach (var diag in result.Diagnostics)
                {
                    if (diag.Severity == DiagnosticSeverity.Error)
                    {
                        Console.WriteLine($"{diag.Descriptor.MessageFormat} - {code.Substring(diag.Location.SourceSpan.Start, diag.Location.SourceSpan.Length)} - {diag.Descriptor.HelpLinkUri} - {diag.Location}");
                    }
                }
                throw new FileLoadException(file);
            }
        }

        Assembly assembly;
        try
        {
            // Load assembly from bytes to avoid file locks
            byte[] assemblyBytes = File.ReadAllBytes(fileOutput);
            assembly = Assembly.Load(assemblyBytes);
            _loadedAssemblies.Add(new WeakReference(assembly));

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAssignableTo(typeof(IScript)))
                {
                    var instance = (IScript?)type.GetConstructor(Type.EmptyTypes)?.Invoke(null);
                    if (instance != null)
                    {
                        instance.Initialize();
                        _scripts[file] = (instance, assembly);
                        return instance;
                    }
                }
            }
        }
        finally
        {
            // Clean up the temporary DLL file
            try
            {
                File.Delete(fileOutput);
            }
            catch { /* Ignore cleanup errors */ }
        }

        return null;
    }

    public void LoadAll(string scriptFolder)
    {
        AttachWatcher(scriptFolder);
        var dirInfo = new DirectoryInfo(scriptFolder);
        if (!dirInfo.Exists)
        {
            return;
        }

        foreach (var file in dirInfo.GetFiles())
        {
            if (!file.Name.EndsWith(".script.cs"))
            {
                continue;
            }

            try
            {
                Load(file.FullName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception trying to load {file.FullName}");
                Console.WriteLine(ex);
            }
        }
    }

    public void Dispose()
    {
        UnloadAll();
    }
}