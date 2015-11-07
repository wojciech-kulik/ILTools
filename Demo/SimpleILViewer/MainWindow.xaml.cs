using Microsoft.Win32;
using ObfuscatorService;
using ObfuscatorService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleILViewer
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, ItemType> _unitKeyToItemTypeMap = new Dictionary<string, ItemType>()
        {
            { "Fields", ItemType.Field },
            { "Properties", ItemType.Property },
            { "Methods", ItemType.Method },
        };
        private Dictionary<string, Func<ILClass, List<ILUnit>>> _unitNameToCollectionMap = new Dictionary<string, Func<ILClass, List<ILUnit>>>()
        {
            { "Fields", x => x.Fields },
            { "Properties", x => x.Properties },
            { "Methods", x => x.Methods }
        };
        private Dictionary<string, TreeViewItem> _namespaces = new Dictionary<string, TreeViewItem>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private TreeViewItem GetTreeViewItemForNamespace(Assembly assembly, string _namespace, ItemCollection root)
        {
            string namespaceName = _namespace.Substring(0, _namespace.LastIndexOf('.'));
            string namespaceKey = String.Format("[{0}] {1}", assembly.FileName, namespaceName);

            if (!_namespaces.ContainsKey(namespaceKey))
            {
                _namespaces[namespaceKey] = new TreeViewItem() { Header = namespaceName, Tag = ItemType.Namespace };
                root.Add(_namespaces[namespaceKey]);
            }

            return _namespaces[namespaceKey];
        }

        private void DisplayStructure(IClassContainer classContainer, ItemCollection parent)
        {
            foreach (var ilClass in classContainer.Classes.OrderBy(x => x.Name))
            {
                TreeViewItem item = new TreeViewItem() { Header = ilClass.ShortName, DataContext = ilClass, Tag = ItemType.Class };

                if (!ilClass.Name.Contains('.'))
                {
                    parent.Add(item);
                }
                else
                {
                    GetTreeViewItemForNamespace(ilClass.ParentAssembly, ilClass.Name, parent).Items.Add(item);
                }

                foreach (var unitKey in _unitNameToCollectionMap.Keys)
                {
                    if (!_unitNameToCollectionMap[unitKey](ilClass).Any())
                    {
                        continue;
                    }

                    var unitItem = new TreeViewItem() { Header = unitKey, Tag = _unitKeyToItemTypeMap[unitKey] };
                    item.Items.Add(unitItem);

                    foreach (var ilUnit in _unitNameToCollectionMap[unitKey](ilClass))
                    {
                        var elem = new TreeViewItem() { Header = ilUnit.Name, DataContext = ilUnit, Tag = _unitKeyToItemTypeMap[unitKey] };
                        unitItem.Items.Add(elem);
                    }
                }

                if (ilClass.Classes.Any())
                {
                    var nestedClassesItem = new TreeViewItem() { Header = "Nested classes", Tag = ItemType.Class };
                    item.Items.Add(nestedClassesItem);
                    DisplayStructure(ilClass, nestedClassesItem.Items);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "Assemblies (*.exe;*.dll)|*.exe;*.dll",
                Multiselect = true
            };

            if (ofd.ShowDialog(this) == true)
            {
                ILReader ilReader = new ILReader();
                foreach (string filePath in ofd.FileNames)
                {
                    ilReader.AddAssembly(filePath);
                }
                ilReader.ParseAssemblies();

                foreach (var assembly in ilReader.Assemblies.OrderBy(x => x.FileName))
                {
                    var item = new TreeViewItem() { Header = assembly.FileName, Tag = ItemType.Assembly };
                    StructureTree.Items.Add(item);
                    DisplayStructure(assembly, item.Items);
                }
            }
        }

        private void StructureTree_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var obj = (DependencyObject)e.OriginalSource;

            while (obj != null && obj != StructureTree)
            {
                if (obj.GetType() == typeof(TreeViewItem))
                {
                    var ilClass = (obj as TreeViewItem).DataContext as ILClass;
                    var ilUnit = (obj as TreeViewItem).DataContext as ILUnit;

                    if (ilClass != null)
                    {
                        tbSourceCode.Text = ilClass.ParentAssembly.ILCode.Substring(ilClass.StartIndex, ilClass.EndIndex - ilClass.StartIndex);
                        tbSourceCode.ScrollToHome();
                        e.Handled = true;
                        break;
                    }
                    else if (ilUnit != null)
                    {
                        ilClass = ilUnit.ParentClass;
                        tbSourceCode.Text = ilClass.ParentAssembly.ILCode.Substring(ilClass.StartIndex, ilClass.EndIndex - ilClass.StartIndex);

                        int line = tbSourceCode.GetLineIndexFromCharacterIndex(ilUnit.NameStartIndex - ilClass.StartIndex);
                        tbSourceCode.ScrollToLine(line);

                        e.Handled = true;
                        break;
                    }
                }

                obj = VisualTreeHelper.GetParent(obj);
            }
        }

        private void StructureTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;

                if (StructureTree.SelectedItem != null)
                {
                    foreach (var key in _namespaces.Keys.ToList())
                    {
                        if (key.StartsWith(String.Format("[{0}]", (StructureTree.SelectedItem as TreeViewItem).Header as string)))
                        {
                            _namespaces.Remove(key);
                        }
                    }

                    StructureTree.Items.Remove(StructureTree.SelectedItem);
                }
            }
        }
    }
}
