using Bubbles3.Controls;
using Bubbles3.Models;
using Bubbles3.Utils;
using Caliburn.Micro;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Bubbles3.ViewModels
{
    public class PageViewModel : PropertyChangedBase, IViewModel<BblPage>, IVirtualizableItem
    {
        public PageViewModel(BblPage model, BookViewModel book)
        {
            Book = book;
            _model = model;
            _filename = _model.Filename;
        }

        public BookViewModel Book { get; set; }
        BblPage _model;
        public BblPage Model
        {
            get => _model;
            set { _model = value; NotifyOfPropertyChange(() => Model); }
        }


        public int Index => Model.Index;
        private string _filename;
        public string Filename
        {
            get => (_filename != null)?_filename : Model.Filename;
            set { _filename = value; NotifyOfPropertyChange(() => Filename); }
        }

        public string Path => Model.Path;
        public string PageNumber => (Book?.PagesCV!= null)?(Book.PagesCV.IndexOf(this) + 1).ToString() : "-1";
        public BitmapSource Thumbnail => Model.Thumbnail;
        

        public DateTime CreationTime => Model.CreationTime;
        public DateTime LastWriteTime => Model.LastWriteTime;

        bool _isRealized = false;
        public bool IsRealized {
            get => _isRealized;
            set
            {
                if(value != _isRealized)
                {
                    _isRealized = value;
                    NotifyOfPropertyChange(() => IsRealized);
                    if (value == true)
                    {
                        if(! Model.IsThumbnailLoaded) Model.LoadImageAsync(IsSelected ? 1 : 2);
                    }
                    else
                    {
                        if (!IsSelected) Unload();
                    }
                }
            }
        }

        bool _isSelected;
        public bool IsSelected
        {
            get =>_isSelected;
            set
            {
                if (_isSelected != value)
                {
                    if (value == true)
                    { 
                        if (!IsRealized)
                            Book.ScrollToPage(this);
                    }
                    else
                    {
                        if (!IsRealized) Unload();
                    }
                    _isSelected = value;
                    NotifyOfPropertyChange(() => IsSelected);
                }
            }
        }

        public bool IsViewModelOf(BblPage model)
        {
            return _model == model;
        }

        public void OnThumbnailLoaded(object sender, EventArgs args)
        {
            if (Thumbnail == null) return;
            try {
                PixelHeight = (int)(Model.Thumbnail.PixelHeight * 1.171875f);
                PixelWidth = (int)( Model.Thumbnail.PixelWidth * 1.171875f);

                _thumbTop = (150f - PixelHeight) / 2f;
                _thumbLeft = (150f - PixelWidth) / 2f;

                
                double innerTop = Utility.Clamp(Model.Ivp.t, 0f, 1f) * PixelHeight;
                double innerLeft = Utility.Clamp(Model.Ivp.l, 0f, 1f) * PixelWidth;
                if(Model.Ivp.isReset)
                {

                }
                double innerBottom = (!Model.Ivp.isReset) ? Utility.Clamp(Model.Ivp.b, 0f, 1f) * PixelHeight : PixelHeight;
                double innerRight = (!Model.Ivp.isReset) ? Utility.Clamp(Model.Ivp.r, 0f, 1f) * PixelWidth : PixelWidth;
                
                _ivpTop = _thumbTop + innerTop;
                _ivpLeft = _thumbLeft + innerLeft;
                _ivpHeight = innerBottom - innerTop;
                _ivpWidth = innerRight - innerLeft;

                NotifyOfPropertyChange(() => Thumbnail);
                NotifyOfPropertyChange(() => PixelWidth);
                NotifyOfPropertyChange(() => PixelHeight);
                NotifyOfPropertyChange(() => ThumbRect);
                NotifyOfPropertyChange(() => IvpRect);

            }
            catch
            {

            }
        }

        public void Unload()
        {
            Model.ReleaseAllData();
            NotifyOfPropertyChange(() => Thumbnail);
        }

        public void RefreshView()
        {
            NotifyOfPropertyChange(() => Index);
            NotifyOfPropertyChange(() => Thumbnail);
            NotifyOfPropertyChange(() => Filename);
            NotifyOfPropertyChange(() => PageNumber);
            NotifyOfPropertyChange(() => LastWriteTime);
            NotifyOfPropertyChange(() => CreationTime);
        }

        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        public double _thumbTop;
        public double _thumbLeft;
        public double _ivpTop;
        public double _ivpLeft;
        public double _ivpHeight;
        public double _ivpWidth;

        public Rect ThumbRect => new Rect(_thumbLeft, _thumbTop, PixelWidth, PixelHeight);
        public Rect IvpRect => new Rect(_ivpLeft, _ivpTop, _ivpWidth, _ivpHeight);

        public Visibility ShowIvp => Book.ShowIvp ? Visibility.Visible : Visibility.Collapsed;
        public void OnShowIvpToggled() { NotifyOfPropertyChange(() => ShowIvp); }

        #region Rename / Delete
        public void MoveFile(string parentDirectory)
        {
            MessageBoxResult r = MessageBox.Show(string.Format("Move {0} \nto {1} ?", Model.Path, parentDirectory), "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                Model.MoveFile(parentDirectory);
            }
        }
        public bool CanMoveFile => Model.CanMoveFile;
        public void DeleteFile(bool silent = false)
        {
            System.Windows.MessageBoxResult r = MessageBoxResult.No;
            if (!silent)
            {
                r = System.Windows.MessageBox.Show(string.Format("Send {0} to the Recycle Bin?", Model.Path), "Confirmation",
                    System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
            }
            else r = MessageBoxResult.Yes;
            if (r == System.Windows.MessageBoxResult.Yes)
            {
                Model.DeleteFile();
            }
        }
        public bool CanDeleteFile => Model.CanDeleteFile;

        private bool _renaming;
        public bool Renaming
        {
            get { return _renaming; }
            set { _renaming = value; NotifyOfPropertyChange(() => Renaming); }
        }

        public bool CanStartRenaming => Model.CanRenameFile;
        public void StartRenaming() { Renaming = true; }
        public void RenamePageTextBoxKeyDown(ActionExecutionContext context)
        {
            var keyArgs = context.EventArgs as KeyEventArgs;

            if (keyArgs != null && keyArgs.Key == Key.Enter)
            {
                Model.RenameFile(_filename);
                Renaming = false;
                keyArgs.Handled = true;
            }
            if (keyArgs != null && keyArgs.Key == Key.Escape)
            {
                Filename = Model.Filename;
                Renaming = false;
                keyArgs.Handled = true;
            }
        }
        public void RenamePageTextBoxLostFocus()
        {
            if (Renaming == true)
            {
                if (Filename != Model.Filename)
                {
                    Model.RenameFile(_filename);
                    Filename = Model.Filename;
                }
                Renaming = false;
            }
        }
        #endregion


    }
}
