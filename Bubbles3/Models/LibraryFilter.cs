using Bubbles3.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bubbles3.Models
{
    public class LibraryFilter
    {
        private enum Operation { And, Or, Sign, TestEquals, TestContains };

        private string _searchString;

        private List<LibraryFilter> _children = new List<LibraryFilter>();
        private LibraryFilter _parent;

        private bool _sign = true;
        private Operation _operation;
        private string _field;
        private string _value;

        public LibraryFilter(String searchString)
        {
            _parent = null;
            _searchString = searchString;
            Initialize();
        }

        private LibraryFilter(LibraryFilter parent, String searchString)
        {
            _parent = parent;
            _searchString = searchString;
            Initialize();
        }

        #region init
        private void Initialize()
        {
            String s = _searchString;
            s = s.ToLowerInvariant();

            s = s.Replace(" and ", "&");
            s = s.Replace(" or ", "|");

            s = s.Replace(" like ", "~");
            s = s.Replace(" equals ", "=");

            s = s.Replace(" not ", "-");

            var substrings = RemoveParentheses(ref s);

            // AND operation
            var array = s.Split('&');
            int i;
            for (i = 0; i < array.Length; i++) array[i] = array[i].Trim();
            if (array.Length > 1)
            {
                _operation = Operation.And;
                RestoreParentheses(ref array, substrings);
                foreach (var ss in array)
                {
                    var child = new LibraryFilter(this, ss);
                    _children.Add(child);
                }
                return;
            }

            // OR operation
            array = s.Split('|');
            for (i = 0; i < array.Length; i++) array[i] = array[i].Trim();
            if (array.Length > 1)
            {
                _operation = Operation.Or;
                RestoreParentheses(ref array, substrings);
                foreach (var ss in array)
                {
                    var child = new LibraryFilter(this, ss);
                    _children.Add(child);
                }
                return;
            }



            RestoreParentheses(ref array, substrings);
            s = array[0];

            //eliminate outward parentheses if any
            int par = s.IndexOf('(');
            if (par >= 0)
            {
                _sign = GetSign(ref s);
                if (!_sign) par = s.IndexOf('(');

                s = s.Remove(par, 1);

                int lpar = s.LastIndexOf(')');
                if (lpar < 0) return; //mismatched parentheses
                s = s.Remove(lpar, 1);

                s = s.Trim();

                _operation = Operation.Sign;
                var child = new LibraryFilter(this, s);
                _children.Add(child);
                return;
            }

            _sign = GetSign(ref s);

            // Equals Evaluation
            array = s.Split('=');
            if (array.Length > 2) return; // bad request
            if (array.Length == 2)
            {
                _operation = Operation.TestEquals;
                _field = array[0].Trim();
                _value = array[1].Trim();
                return;
            }

            // Contains Evaluation
            _operation = Operation.TestContains;

            array = s.Split('~');
            if (array.Length > 2) return; // bad request
            if (array.Length == 2)
            {
                _field = array[0].Trim();
                _value = array[1].Trim();
                return;
            }

            _field = "path";
            _value = s.Trim();
        }



        bool GetSign(ref string s)
        {
            if (s.IndexOf('-') == 0)
            {
                s = s.Remove(0, 1);
                s = s.Trim();
                return false;
            }
            return true;
        }

        string[] RemoveParentheses(ref String s)
        {
            var regex = new Regex(@"
                                    \(                    # Match (
                                    (
                                        [^()]+            # all chars except ()
                                        | (?<Level>\()    # or if ( then Level += 1
                                        | (?<-Level>\))   # or if ) then Level -= 1
                                    )+                    # Repeat (to go from inside to outside)
                                    (?(Level)(?!))        # zero-width negative lookahead assertion
                                    \)                    # Match )",
                RegexOptions.IgnorePatternWhitespace);

            List<String> substrings = new List<string>();
            int i = 0;
            foreach (Match c in regex.Matches(s))
            {
                string r = c.Value;
                substrings.Add(r);
                s = s.Replace(r, "[" + i++.ToString() + "]");
            }
            return substrings.ToArray();
        }

        void RestoreParentheses(ref string[] array, string[] substrings)
        {
            int i = 0;
            foreach (var ss in substrings)
            {
                string placeholder = "[" + i++.ToString() + "]";
                for (int j = 0; j < array.Length; j++)
                    array[j] = array[j].Replace(placeholder, ss);
            }
            substrings = null;
        }
        #endregion


        #region filter
        public bool Filter(BookViewModel obj)
        {
            switch (_operation)
            {
                case Operation.Sign: return Sign(obj);
                case Operation.And: return And(obj);
                case Operation.Or: return Or(obj);
                case Operation.TestContains: return TestContains(obj);
                case Operation.TestEquals: return TestEquals(obj);
            }
            return false;
        }

        private bool Sign(BookViewModel obj)
        {
            if (_children.Count != 1) throw new Exception("fuckup in filter processing builder");
            bool ret = _children[0].Filter(obj);
            return (_sign) ? ret : !ret;
        }
        private bool And(BookViewModel obj)
        {
            foreach (var child in _children)
                if (!child.Filter(obj)) return false;

            return true;
        }
        private bool Or(BookViewModel obj)
        {
            foreach (var child in _children)
                if (child.Filter(obj)) return true;
            return false;
        }
        private bool TestEquals(BookViewModel obj)
        {
            switch (_field)
            {
                case "path":
                    if (obj.Path.ToLowerInvariant() == _value) return (_sign) ? true : false;
                    break;
                default:
                    foreach (var tag in obj.Tags)
                        if (_field == tag.Key)
                            if (_value == tag.Value.ToLowerInvariant()) return (_sign) ? true : false;
                    break;
            }
            return (_sign) ? false : true;
        }
        private bool TestContains(BookViewModel obj)
        {
            switch (_field)
            {
                case "path":
                    if (obj.Path.ToLowerInvariant().Contains(_value))
                        return (_sign) ? true : false;
                    break;
                default:
                    foreach (var tag in obj.Tags)
                        if (_field == tag.Key.ToLowerInvariant())
                            if (tag.Value.ToLowerInvariant().Contains(_value))
                                return (_sign) ? true : false; ;
                    break;
            }
            return (_sign) ? false : true;
        }
        #endregion

        public override String ToString()
        {
            return _searchString;
        }
    }
}
