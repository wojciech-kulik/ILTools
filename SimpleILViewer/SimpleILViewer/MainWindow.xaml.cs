using ILObfuscator;
using Microsoft.Win32;
using ILStructureParser;
using ILStructureParser.Models;
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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace SimpleILViewer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Assemblies
        private ObservableCollection<TreeUIItem> _assemblies = new ObservableCollection<TreeUIItem>();
        public ObservableCollection<TreeUIItem> Assemblies
        {
            get
            {
                return _assemblies;
            }
            set
            {
                if (value != _assemblies)
                {
                    _assemblies = value;
                    NotifyOfPropertyChanged();
                }
            }
        }
        #endregion

        private ILReader _ilReader = new ILReader();
        private UITreeGenerator _treeGenerator = new UITreeGenerator();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async Task LoadAssemblies(string[] fileNames)
        {
            await Task.Run(() =>
            {
                foreach (string fileName in fileNames)
                {
                    _ilReader.AddAssembly(fileName);
                }

                Stopwatch sw = new Stopwatch();
                sw.Start();
                _ilReader.ParseAssemblies();
                sw.Stop();
                File.AppendAllText("time.txt", "Parsing: " + sw.ElapsedMilliseconds.ToString() + "\r\n");
            });

            ShowAssemblies(_ilReader.Assemblies.Skip(_ilReader.Assemblies.Count - fileNames.Length));
        }

        private void RemoveAssembly(TreeUIItem item)
        {
            Assemblies.Remove(item);
            _treeGenerator.RemoveAssembly(item.Header);
            _ilReader.Assemblies.Remove(_ilReader.Assemblies.FirstOrDefault(x => x.FileName == item.Header));
        }

        private void RemoveDuplicates(string[] fileNames)
        {
            foreach (var assembly in Assemblies.ToList())
            {
                if (fileNames.Any(x => assembly.Header == System.IO.Path.GetFileName(x)))
                {
                    RemoveAssembly(assembly);
                }
            }
        }

        private void ShowAssemblies(IEnumerable<Assembly> assemblies)
        {
            var newCollection = Assemblies.Union(_treeGenerator.GenerateTree(assemblies)).OrderBy(x => x.Header);
            Assemblies = new ObservableCollection<TreeUIItem>(newCollection);
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Directory.Exists(ILReader.ILDirectory))
            {
                Directory.Delete(ILReader.ILDirectory, true);
            }
        }

        private async void btnObfuscate_Click(object sender, RoutedEventArgs e)
        {
            if (StructureTree.Items.Count == 0)
            {
                MessageBox.Show(this, "Load some assemblies first.", "No assemblies", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetProgress(true);
            try
            {
                await Task.Run(() => ObfuscateAssemblies());

                Assemblies.Clear();
                _treeGenerator.Clear();
                ShowAssemblies(_ilReader.Assemblies);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not obfuscate some assemblies:\r\n\r\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                SetProgress(false);
            }

            MessageBox.Show(this, "Obfuscation finished successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show(this, String.Format("Could not parse some assemblies:\r\n\r\n{0}\r\n\r\n{1}", ex.InnerException.Message, ex.InnerException.StackTrace), 
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetProgress(false);
                }
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

        private void StructureTree_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var obj = (DependencyObject)e.OriginalSource;

            while (obj != null && obj != StructureTree)
            {
                if (obj.GetType() == typeof(TreeViewItem))
                {
                    var ilClass = ((obj as TreeViewItem).DataContext as TreeUIItem).Parent as ILClass;
                    var ilUnit = ((obj as TreeViewItem).DataContext as TreeUIItem).Parent as ILUnit;

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
            var item = StructureTree.SelectedItem as TreeUIItem;
            if (e.Key == Key.Delete && item != null && item.ItemType == ItemType.Assembly)
            {
                e.Handled = true;
                RemoveAssembly(item);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyOfPropertyChanged([CallerMemberName]string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
