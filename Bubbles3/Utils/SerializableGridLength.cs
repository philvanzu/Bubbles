using System;
using System.Windows;

namespace Bubbles3.Utils
{
    [Serializable]
    public struct SerializableGridLength
    {
        private double _value;
        private GridUnitType _unitType;

        public SerializableGridLength(GridLength value)
        {
            _value = value.Value;
            _unitType = value.GridUnitType;
        }

        public GridLength ToGridLength
        {
            get { return new GridLength(_value, _unitType); }
            set { _value = value.Value; _unitType = value.GridUnitType; }
        }
    }
}
