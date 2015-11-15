using ILStructureParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleILViewer
{
    public class UITreeGenerator
    {
        private Dictionary<string, TreeUIItem> _namespaces = new Dictionary<string, TreeUIItem>();
        private Dictionary<string, ItemType> _unitKeyToItemTypeMap = new Dictionary<string, ItemType>()
        {
            { "Fields", ItemType.Field },
            { "Events", ItemType.Event },
            { "Properties", ItemType.Property },
            { "Methods", ItemType.Method },
        };
        private Dictionary<string, Func<ILClass, LinkedList<ILUnit>>> _unitNameToCollectionMap = new Dictionary<string, Func<ILClass, LinkedList<ILUnit>>>()
        {
            { "Fields", x => x.Fields },
            { "Events", x => x.Events },
            { "Properties", x => x.Properties },
            { "Methods", x => x.Methods },
        };

        private TreeUIItem GetTreeUIItemForNamespace(Assembly assembly, string _namespace, LinkedList<TreeUIItem> root)
        {
            string namespaceName = _namespace.Substring(0, _namespace.LastIndexOf('.'));
            string namespaceKey = String.Format("[{0}] {1}", assembly.FileName, namespaceName);

            if (!_namespaces.ContainsKey(namespaceKey))
            {
                _namespaces[namespaceKey] = new TreeUIItem() { Header = namespaceName, ItemType = ItemType.Namespace };
                root.AddLast(_namespaces[namespaceKey]);
            }

            return _namespaces[namespaceKey];
        }

        private void GenerateStructure(IClassContainer classContainer, LinkedList<TreeUIItem> parent)
        {
            foreach (var ilClass in classContainer.Classes.OrderBy(x => x.Name))
            {
                var item = new TreeUIItem() { Header = ilClass.ShortName, Parent = ilClass, ItemType = ItemType.Class };

                // find a place for this class
                if (!ilClass.Name.Contains('.'))
                {
                    parent.AddLast(item);
                }
                else
                {
                    GetTreeUIItemForNamespace(ilClass.ParentAssembly, ilClass.Name, parent).Items.AddLast(item);
                }

                // add methods, properties and fields
                foreach (var unitKey in _unitNameToCollectionMap.Keys)
                {
                    var collection = _unitNameToCollectionMap[unitKey](ilClass);
                    if (!collection.Any())
                    {
                        continue;
                    }

                    var itemType = _unitKeyToItemTypeMap[unitKey];

                    // grouping item
                    var unitItem = new TreeUIItem() { Header = unitKey, ItemType = itemType };
                    item.Items.AddLast(unitItem);

                    // all methods/properties/fields
                    foreach (var ilUnit in collection)
                    {
                        unitItem.Items.AddLast(new TreeUIItem() { Header = ilUnit.Name, Parent = ilUnit, ItemType = itemType });
                    }
                }

                // add nested classes
                if (ilClass.Classes.Any())
                {
                    var nestedClassesItem = new TreeUIItem() { Header = "Nested classes", ItemType = ItemType.Class };
                    item.Items.AddLast(nestedClassesItem);
                    GenerateStructure(ilClass, nestedClassesItem.Items); // recursive call for nested classes
                }
            }
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

        public IList<TreeUIItem> GenerateTree(IEnumerable<Assembly> assemblies)
        {
            var items = new List<TreeUIItem>();
            foreach (var assembly in assemblies.OrderBy(x => x.FileName))
            {
                var item = new TreeUIItem() { Header = assembly.FileName, ItemType = ItemType.Assembly };
                GenerateStructure(assembly, item.Items);
                items.Add(item);
            }

            return items;
        }

        public void RemoveAssembly(string assembly)
        {
            RemoveNamespacesForAssembly(assembly);
        }

        public void Clear()
        {
            _namespaces.Clear();
        }
    }
}
