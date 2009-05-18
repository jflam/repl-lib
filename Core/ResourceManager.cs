using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

public static class ReplResourceManager {
    public static Stream Load(string name) {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var dir = Path.GetDirectoryName(assemblyPath);
        var resourcePath = Path.Combine(dir, name);
        if (File.Exists(resourcePath)) {
            return File.OpenRead(resourcePath);
        } else {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Core." + name);
            var length = (int)stream.Length;
            try {
                using (var outputStream = File.OpenWrite(resourcePath)) {
                    var buffer = new byte[length];
                    stream.Read(buffer, 0, length);
                    outputStream.Write(buffer, 0, length);
                }
            } catch (IOException) {
                // write failures are OK
            }
            stream.Position = 0;
            return stream;
        }
    }
}