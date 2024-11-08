#nullable disable

using System.ComponentModel;
using bsm24.Models;

namespace bsm24.Services;
public partial class SettingsService : INotifyPropertyChanged
{
    private static SettingsService _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private SettingsService()
    {
        Theme = Theme.System;
        ImageQuality = "300";
    }

    private Theme _theme;
    public Theme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            OnPropertyChanged(_theme.DisplayName);
        }
    }

    private string _imageQuality;
    public string ImageQuality
    {
        get => _imageQuality;
        set
        {
            if (_imageQuality == value) return;
            _imageQuality = value;
            OnPropertyChanged(_imageQuality);
        }
    }

    private string _flyoutHeaderTitle = "";
    public string FlyoutHeaderTitle
    {
        get => _flyoutHeaderTitle;
        set
        {
            if (_flyoutHeaderTitle != value)
            {
                _flyoutHeaderTitle = value;
                OnPropertyChanged(nameof(FlyoutHeaderTitle));
            }
        }
    }

    private string _flyoutHeaderDesc = "BSM 24 by EBBE";
    public string FlyoutHeaderDesc
    {
        get => _flyoutHeaderDesc;
        set
        {
            if (_flyoutHeaderDesc != value)
            {
                _flyoutHeaderDesc = value;
                OnPropertyChanged(nameof(FlyoutHeaderDesc));
            }
        }
    }

    private string _flyoutHeaderImage = "banner_thumbnail.png";
    public string FlyoutHeaderImage
    {
        get => _flyoutHeaderImage;
        set
        {
            if (_flyoutHeaderImage != value)
            {
                _flyoutHeaderImage = value;
                OnPropertyChanged(nameof(FlyoutHeaderImage));
            }
        }
    }

    private string _isPlanRotateLocked = "false";
    public string IsPlanRotateLocked
    {
        get => _isPlanRotateLocked;
        set
        {
            if (_isPlanRotateLocked != value)
            {
                _isPlanRotateLocked = value;
                OnPropertyChanged(nameof(IsPlanRotateLocked));
            }
        }
    }

    private string _pinScaleLimit = "0,5";
    public string PinScaleLimit
    {
        get => _pinScaleLimit;
        set
        {
            if (_pinScaleLimit != value)
            {
                _pinScaleLimit = Math.Round(Convert.ToDouble(value), 1).ToString();
                OnPropertyChanged(nameof(PinScaleLimit));
            }
        }
    }

    private string _pdfQuality = "300";
    public string PdfQuality
    {
        get => _pdfQuality;
        set
        {
            if (_pdfQuality != value)
            {
                _pdfQuality = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(PdfQuality));
            }
        }
    }

    #region
    // Export Settings

    private string _imageExportQuality = "90";
    public string ImageExportQuality
    {
        get => _imageExportQuality;
        set
        {
            if (_imageExportQuality != value)
            {
                _imageExportQuality = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(ImageExportQuality));
            }
        }
    }

    private string _isPlanExport = "true";
    public string IsPlanExport
    {
        get => _isPlanExport;
        set
        {
            if (_isPlanExport != value)
            {
                _isPlanExport = value;
                OnPropertyChanged(nameof(IsPlanExport));
            }
        }
    }

    private string _isPosImageExport = "true";
    public string IsPosImageExport
    {
        get => _isPosImageExport;
        set
        {
            if (_isPosImageExport != value)
            {
                _isPosImageExport = value;
                OnPropertyChanged(nameof(IsPosImageExport));
            }
        }
    }

    private string _isImageExport = "true";
    public string IsImageExport
    {
        get => _isImageExport;
        set
        {
            if (_isImageExport != value)
            {
                _isImageExport = value;
                OnPropertyChanged(nameof(IsImageExport));
            }
        }
    }

    private string _isPinIconExport = "true";
    public string IsPinIconExport
    {
        get => _isPinIconExport;
        set
        {
            if (_isPinIconExport != value)
            {
                _isPinIconExport = value;
                OnPropertyChanged(nameof(IsPinIconExport));
            }
        }
    }
    private string _imageExportSize = "40";
    public string ImageExportSize
    {
        get => _imageExportSize;
        set
        {
            if (_imageExportSize != value)
            {
                _imageExportSize = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(ImageExportSize));
            }
        }
    }


    private string _imageExportScale = "0,1";
    public string ImageExportScale
    {
        get => _imageExportScale;
        set
        {
            if (_imageExportScale != value)
            {
                _imageExportScale = Math.Round(Convert.ToDouble(value), 2).ToString();
                OnPropertyChanged(nameof(ImageExportScale));
            }
        }
    }

    private string _posImageExportSize = "600";
    public string PosImageExportSize
    {
        get => _posImageExportSize;
        set
        {
            if (_posImageExportSize != value)
            {
                _posImageExportSize = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(PosImageExportSize));
            }
        }
    }

    private string _posImageExportScale = "0,5";
    public string PosImageExportScale
    {
        get => _posImageExportScale;
        set
        {
            if (_posImageExportScale != value)
            {
                _posImageExportScale = Math.Round(Convert.ToDouble(value), 2).ToString();
                OnPropertyChanged(nameof(PosImageExportScale));
            }
        }
    }
    #endregion

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}