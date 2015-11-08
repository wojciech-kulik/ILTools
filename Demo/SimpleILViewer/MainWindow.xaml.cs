using ILObfuscator;
using Microsoft.Win32;
using ObfuscatorService;
using ObfuscatorService.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

        private ILReader _ilReader = new ILReader();


        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Directory.Exists("ILFiles"))
            {
                Directory.Delete("ILFiles", true);
            }
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

                // find a place for this class
                if (!ilClass.Name.Contains('.'))
                {
                    parent.Add(item);
                }
                else
                {
                    GetTreeViewItemForNamespace(ilClass.ParentAssembly, ilClass.Name, parent).Items.Add(item);
                }

                // add methods, properties and fields
                foreach (var unitKey in _unitNameToCollectionMap.Keys)
                {
                    if (!_unitNameToCollectionMap[unitKey](ilClass).Any())
                    {
                        continue;
                    }

                    // grouping item
                    var unitItem = new TreeViewItem() { Header = unitKey, Tag = _unitKeyToItemTypeMap[unitKey] };
                    item.Items.Add(unitItem);

                    // all methods/properties/fields
                    foreach (var ilUnit in _unitNameToCollectionMap[unitKey](ilClass))
                    {
                        var elem = new TreeViewItem() { Header = ilUnit.Name, DataContext = ilUnit, Tag = _unitKeyToItemTypeMap[unitKey] };
                        unitItem.Items.Add(elem);
                    }
                }
                
                // add nested classes
                if (ilClass.Classes.Any())
                {
                    var nestedClassesItem = new TreeViewItem() { Header = "Nested classes", Tag = ItemType.Class };
                    item.Items.Add(nestedClassesItem);
                    DisplayStructure(ilClass, nestedClassesItem.Items);
                }
            }
        }

        private async Task LoadAssemblies(string[] fileNames)
        {
            await Task.Run(() =>
            {
                foreach (string fileName in fileNames)
                {
                    _ilReader.AddAssembly(fileName);
                }
                _ilReader.ParseAssemblies();
            });
            ShowAssemblies(_ilReader.Assemblies.Skip(_ilReader.Assemblies.Count - fileNames.Length));
        }

        private void RemoveNamespacesForAssembly(string assembly)
        {
            foreach (var key in _namespaces.Keys.ToList())
            {
                if (key.StartsWith(String.Format("[{0}]", assembly)))
                {
                    _namespaces.Remove(key);
                }
            }
        }

        private void RemoveDuplicates(string[] fileNames)
        {
            foreach (var item in StructureTree.Items.OfType<TreeViewItem>().ToList())
            {
                if (fileNames.Any(x => item.Header as string == System.IO.Path.GetFileName(x)))
                {
                    StructureTree.Items.Remove(item);
                    RemoveNamespacesForAssembly(item.Header as string);
                    _ilReader.Assemblies.Remove(_ilReader.Assemblies.FirstOrDefault(x => x.FileName == item.Header as string));
                }
            }
        }

        private void ShowAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies.OrderBy(x => x.FileName))
            {
                var item = new TreeViewItem() { Header = assembly.FileName, Tag = ItemType.Assembly };
                DisplayStructure(assembly, item.Items);
                StructureTree.Items.Add(item);
            }
        }

        private void ObfuscateAssemblies()
        {
            new Obfuscator().Obfuscate(_ilReader.Assemblies);
            _ilReader.RefreshAssemblies();         
        }

        private void SetProgress(bool value)
        {
            if (value)
            {
                btnLoadAssemblies.IsEnabled = false;
                btnObfuscate.IsEnabled = false;
                LoadingIconStatus.Visibility = Visibility.Visible;
                LoadingStatus.Visibility = Visibility.Visible;
            }
            else
            {
                btnLoadAssemblies.IsEnabled = true;
                btnObfuscate.IsEnabled = true;
                LoadingIconStatus.Visibility = Visibility.Collapsed;
                LoadingStatus.Visibility = Visibility.Collapsed;
            }
        }

        #region UI event handlers

        private async void btnObfuscate_Click(object sender, RoutedEventArgs e)
        {
            SetProgress(true);
            try
            {
                await Task.Run(() => ObfuscateAssemblies());

                StructureTree.Items.Clear();
                _namespaces.Clear();
                ShowAssemblies(_ilReader.Assemblies);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not obfuscate some assemblies: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetProgress(false);
            } 
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            (FindResource("LoadingAnimation") as Storyboard).Begin();
            (FindResource("IconRotationAnimation") as Storyboard).Begin();
        }

        private async void btnLoadAssemblies_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "Assemblies (*.exe;*.dll)|*.exe;*.dll",
                Multiselect = true
            };

            if (ofd.ShowDialog(this) == true)
            {
                SetProgress(true);
                try
                {
                    RemoveDuplicates(ofd.FileNames);
                    await LoadAssemblies(ofd.FileNames);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not parse some assemblies: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetProgress(false);
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
                    string assemblyName = (StructureTree.SelectedItem as TreeViewItem).Header as string;
                    _ilReader.Assemblies.Remove(_ilReader.Assemblies.FirstOrDefault(x => x.FileName == assemblyName));
                    RemoveNamespacesForAssembly(assemblyName);
                    StructureTree.Items.Remove(StructureTree.SelectedItem);
                }
            }
        }

        #endregion
    }
}
