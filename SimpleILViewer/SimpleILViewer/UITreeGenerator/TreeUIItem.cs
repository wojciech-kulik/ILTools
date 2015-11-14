using ILStructureParser.Models;
using System.Collections.Generic;

namespace SimpleILViewer
{
    public class TreeUIItem
    {
        public string Header { get; set; }

        public ILUnit Parent { get; set; }

        public ItemType ItemType { get; set; }

        public LinkedList<TreeUIItem> Items { get; set; } = new LinkedList<TreeUIItem>();
    }
}
