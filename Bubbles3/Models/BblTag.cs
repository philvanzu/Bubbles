using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;

namespace Bubbles3.Models
{
    [Serializable]
    public class BblTag
    {
        private bool _dirty = false;
        private string _key;
        private string _value;


        public String Key
        {
            get { return _key; }
            set
            {
                _key = value;
                _dirty = true;
            }
        }

        public String Value
        {
            get { return _value; }
            set
            {
                _value = value;
                _dirty = true;
            }
        }
        [XmlIgnore]
        public bool Append { get; set; }



        public BblTag()
        {
            _key = "";
            _value = "";
            Append = false;
        }

        public BblTag(BblTag copy)
        {
            _key = copy._key;
            _value = copy._value;
            Append = copy.Append;
            _dirty = false;
        }

        public BblTag(String key, String value, bool append = false)
        {
            _key = key;
            _value = value;
            Append = append;
        }
        public bool IsDirty() { return _dirty; }
        public void SetDirty() { _dirty = true; }
        public void SetClean() { _dirty = false; }

        public void OnKeyTest(ActionExecutionContext context)
        {
        }
    }

    public class BblTagCollection
    {
        public List<BblTag> Collection { get; set; }

        public BblTagCollection()
        { Collection = new List<BblTag>(); }

        public BblTagCollection(ObservableCollection<BblTag> collection)
        {
            Collection = collection.ToList();
        }

    }
}
