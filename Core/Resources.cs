using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace Core {
    public static class Core {
        private static ResourceDictionary _resourceDictionary;

        private static void LoadResources() {
            _resourceDictionary = (ResourceDictionary)XamlReader.Load(XmlReader.Create("ReplResources.xaml"));
        }

        public static object FindResource(string key) {
            if (_resourceDictionary == null)
                LoadResources();
            return _resourceDictionary[key];
        }
    }
}