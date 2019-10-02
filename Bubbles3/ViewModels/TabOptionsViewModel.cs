using Bubbles3.Models;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bubbles3.ViewModels
{
    public class TabOptionsViewModel : PropertyChangedBase
    {
        TabOptions _params;
        private ObservableCollection<TabOptions> _savedParams = new ObservableCollection<TabOptions>();
        private String _selectedName;
        private String _saveName;

        public TabOptionsViewModel(TabOptions parameters, ObservableCollection<TabOptions> savedParams)
        {
            _params = parameters;
            _savedParams = savedParams;

        }
        public bool UseIvp
        {
            get { return _params.rememberView; }
            set { _params.rememberView = value; NotifyOfPropertyChange(() => UseIvp); }
        }
        public bool SaveIvp
        {
            get { return _params.saveIvps; }
            set { _params.saveIvps = value; NotifyOfPropertyChange(() => SaveIvp); }
        }
        public bool SavePageMarkers
        {
            get { return _params.savePageMarkers; }
            set { _params.savePageMarkers = value; NotifyOfPropertyChange(() => SavePageMarkers); }
        }
        public bool KeepZoom
        {
            get { return _params.keepZoom; }
            set { _params.keepZoom = value; NotifyOfPropertyChange(() => KeepZoom); }
        }
        public bool ZoomRectOnRightClick
        {
            get { return _params.zoomRectOnRightClick; }
            set { _params.zoomRectOnRightClick = value; NotifyOfPropertyChange(() => ZoomRectOnRightClick); }
        }
        public bool ReadBackwards
        {
            get { return _params.readBackwards; }
            set { _params.readBackwards = value; NotifyOfPropertyChange(() => ReadBackwards); }
        }
        public bool ShowScroll
        {
            get { return _params.showScroll; }
            set { _params.showScroll = value; NotifyOfPropertyChange(() => ShowScroll); }
        }
        public bool ShowPaging
        {
            get { return _params.showPaging; }
            set { _params.showPaging = value; NotifyOfPropertyChange(() => ShowPaging); }
        }

        public bool AnimScroll
        {
            get { return _params.animScroll; }
            set { _params.animScroll = value; NotifyOfPropertyChange(() => AnimScroll); }
        }

        public bool AnimIvp
        {
            get { return _params.animIVP; }
            set { _params.animIVP = value; NotifyOfPropertyChange(() => AnimIvp); }
        }
        public bool AnimZoom
        {
            get { return _params.animKeyZoom; }
            set { _params.animKeyZoom = value; NotifyOfPropertyChange(() => AnimZoom); }
        }
        public bool AnimRotation
        {
            get { return _params.animRotation; }
            set { _params.animRotation = value; NotifyOfPropertyChange(() => AnimRotation); }
        }

        public bool MouseWheelAction
        {
            get { return _params.mwScroll; }
            set { _params.mwScroll = value; NotifyOfPropertyChange(() => MouseWheelAction); }
        }
        public BindableCollection<string> ZoomMode
        {
            get
            {
                return new BindableCollection<string>(
                                 new string[] { "1:1", "Fit Width", "Fit Height", "Fit Best" });
            }
        }

        public string SelectedZoomMode
        {
            get
            {
                switch (_params.zoomMode)
                {
                    case BblZoomMode.Default: return "1:1";
                    case BblZoomMode.Fit: return "Fit Best";
                    case BblZoomMode.FitH: return "Fit Height";
                    case BblZoomMode.FitW: return "Fit Width";
                    default: return "1:1";
                }
            }
            set
            {
                if (value == "1:1") _params.zoomMode = BblZoomMode.Default;
                else if (value == "Fit Best") _params.zoomMode = BblZoomMode.Fit;
                else if (value == "Fit Height") _params.zoomMode = BblZoomMode.FitH;
                else if (value == "Fit Width") _params.zoomMode = BblZoomMode.FitW;

                NotifyOfPropertyChange(() => SelectedZoomMode);
            }
        }

        public String SaveName
        {
            get { return _saveName; }
            set
            {
                _saveName = value;
                NotifyOfPropertyChange(() => SaveName);
                NotifyOfPropertyChange(() => CanDeleteButton);
                NotifyOfPropertyChange(() => CanSaveButton);
            }
        }


        public ObservableCollection<TabOptions> SavedSettings { get { return _savedParams; } }
        public TabOptions SelectedSavedSettings
        {
            get { return (_savedParams.Where(x => x.name == _selectedName).FirstOrDefault()); }
            set
            {
                if (value == null) return;
                _selectedName = value.name;
                SaveName = value.name;
                _params = value;

                NotifyOfPropertyChange(() => UseIvp);
                NotifyOfPropertyChange(() => SaveIvp);
                NotifyOfPropertyChange(() => SavePageMarkers);

                NotifyOfPropertyChange(() => AnimIvp);
                NotifyOfPropertyChange(() => AnimRotation);
                NotifyOfPropertyChange(() => AnimScroll);
                NotifyOfPropertyChange(() => AnimZoom);

                NotifyOfPropertyChange(() => ShowPaging);
                NotifyOfPropertyChange(() => ShowScroll);

                NotifyOfPropertyChange(() => ZoomMode);
                NotifyOfPropertyChange(() => SelectedZoomMode);
                NotifyOfPropertyChange(() => KeepZoom);
                NotifyOfPropertyChange(() => ReadBackwards);
                NotifyOfPropertyChange(() => ZoomRectOnRightClick);

                NotifyOfPropertyChange(() => MouseWheelAction);

                NotifyOfPropertyChange(() => SaveName);
                NotifyOfPropertyChange(() => SavedSettings);
                NotifyOfPropertyChange(() => SelectedSavedSettings);

                NotifyOfPropertyChange(() => CanDeleteButton);
                NotifyOfPropertyChange(() => CanSaveButton);
            }
        }
        public void SaveButton()
        {

            _params.name = _saveName;
            TabOptions p = new TabOptions(_params);
            TabOptions exists = null;
            foreach (var sp in _savedParams)
            {
                if (sp.name == p.name)
                {
                    exists = sp;
                    break;
                }
            }
            if (exists != null) exists = p;
            else _savedParams.Add(p);

            //NotifyOfPropertyChange(() => SavedSettings);
        }
        public bool CanSaveButton
        {
            get
            {
                if (SaveName == "default") return false;
                return true;
            }
        }
        public void DeleteButton()
        {
            _savedParams.Remove(SelectedSavedSettings);
        }
        public bool CanDeleteButton
        {
            get
            {
                if (SaveName == "default") return false;
                return true;
            }
        }
        public void LoadButton()
        {
            _params = SelectedSavedSettings;
        }
    }
}
