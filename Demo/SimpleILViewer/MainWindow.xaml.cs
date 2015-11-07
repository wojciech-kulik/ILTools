using Microsoft.Win32;
using ObfuscatorService;
using ObfuscatorService.Models;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace SimpleILViewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void PrepareClassStructure(IClassContainer classContainer, ItemCollection parent)
        {
            foreach (var ilClass in classContainer.Classes.OrderBy(x => x.Name))
            {
                TreeViewItem item = new TreeViewItem() { Header = ilClass.Name, DataContext = ilClass };
                parent.Add(item);

                var units = new Dictionary<string, List<ILUnit>>()
                {
                    { "Fields", ilClass.Fields },
                    { "Properties", ilClass.Properties },
                    { "Methods", ilClass.Methods }
                };

                foreach (var unitKey in units.Keys)
                {
                    var unitItem = new TreeViewItem() { Header = unitKey };
                    item.Items.Add(unitItem);

                    foreach (var ilUnit in units[unitKey])
                    {
                        var elem = new TreeViewItem() { Header = ilUnit.Name, DataContext = ilUnit };
                        unitItem.Items.Add(elem);
                    }
                }
                if (ilClass.Classes.Any())
                {
                    var nestedClassesItem = new TreeViewItem() { Header = "Nested classes" };
                    item.Items.Add(nestedClassesItem);
                    PrepareClassStructure(ilClass, nestedClassesItem.Items);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "Assemblies (*.exe;*.dll)|*.*;*.exe;*.dll",
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

                StructureTree.Items.Clear();
                foreach (var assembly in ilReader.Assemblies.OrderBy(x => x.FileName))
                {
                    var item = new TreeViewItem() { Header = assembly.FileName };
                    StructureTree.Items.Add(item);
                    PrepareClassStructure(assembly, item.Items);
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
                    ILClass ilClass = (obj as TreeViewItem).DataContext as ILClass;
                    if (ilClass != null)
                    {
                        tbSourceCode.Text = ilClass.ParentAssembly.ILCode.Substring(ilClass.StartIndex, ilClass.EndIndex - ilClass.StartIndex);
                        e.Handled = true;
                        break;
                    } 
                }

                obj = VisualTreeHelper.GetParent(obj);
            }
        }
    }
}
