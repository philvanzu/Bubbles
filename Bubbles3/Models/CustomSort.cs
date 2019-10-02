using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bubbles3.Models
{
    [Serializable]
    public class CustomSort
    {

        public Dictionary<int, SortUnit> Value = new Dictionary<int, SortUnit>();

        public CustomSort() { }

        public CustomSort(CustomSort copy)
        {
            Value = new Dictionary<int, SortUnit>(copy.Value);
        }

        public CustomSort(String field, bool direction = true)
        {
            Value.Add(0, new SortUnit(field, direction));
        }

        public String Name { get { return ToString(); } }

        public ObservableCollection<String> Fields
        {
            get
            {
                ObservableCollection<string> ret = new ObservableCollection<string>();
                for (int i = 0; i < Value.Count; i++)
                {
                    if (Value[i].Field != null && Value[i].Field != "") ret.Add(Value[i].Field);
                    else break;
                }
                return ret;
            }
        }

        public override string ToString()
        {
            string retval = "";
            for (int i = 0; i < Value.Count; i++)
            {
                retval += Value[i].Field;
                if (!Value[i].Direction) retval += " Desc";
                if (i < Value.Count - 1) retval += " / ";
            }
            return retval;
        }
    }

    [Serializable]
    public struct SortUnit
    {
        public String Field;
        public bool Direction;
        public SortUnit(string field, bool direction = true)
        {
            Field = field;
            Direction = direction;
        }
    }
}
