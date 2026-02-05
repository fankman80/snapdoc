#nullable disable

using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FFImageLoading.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using Svg.Skia;
using static SnapDoc.Helper;

namespace SnapDoc.Views;

public partial class PopupSettings : Popup, IQueryAttributable
{
    public SvgCachedImage PinSvgImage;
    //private SKSvg _svg = new SKSvg();

    public PopupSettings()
    {
        InitializeComponent();

        //string hexColor = ((Color)Application.Current.Resources["Primary"]).ToRgbaHex();
        //svgIcon.Source = LoadSvgWithColor("customcolor.svg", "#999999", hexColor);

        //var svgSource = LoadSvgWithColor("customcolor.svg", "#999999", hexColor);
        //_svg.Load(svgSource);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("fileType", out var value))
        {
            if (value as string == "Prg")
                SettingsService.Instance.LoadSettings();
            if (value as string == "Doc")
                GlobalJson.LoadFromFile(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile));
            if (value as string == "Icon")
            {
                // Icon-Daten einlesen
                Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
                SettingsService.Instance.IconCategories = iconCategories;
                IconLookup.Initialize(Settings.IconData);
            }
        }
    }

    //private void OnPaintCanvas(object sender, SKPaintSurfaceEventArgs e)
    //{
    //    var canvas = e.Surface.Canvas;
    //    canvas.Clear(SKColors.Transparent);

    //    if (_svg?.Picture != null)
    //    {
    //        float canvasWidth = e.Info.Width;
    //        float canvasHeight = e.Info.Height;
    //        float svgWidth = _svg.Picture.CullRect.Width;
    //        float svgHeight = _svg.Picture.CullRect.Height;

    //        if (svgWidth <= 0 || svgHeight <= 0)
    //            return;

    //        float scaleX = canvasWidth / svgWidth;
    //        float scaleY = canvasHeight / svgHeight;
    //        float scale = Math.Min(scaleX, scaleY);
    //        float left = (canvasWidth - svgWidth * scale) / 2f;
    //        float top = (canvasHeight - svgHeight * scale) / 2f;

    //        canvas.Save();
    //        canvas.Translate(left, top);
    //        canvas.Scale(scale);
    //        canvas.DrawPicture(_svg.Picture);
    //        canvas.Restore();
    //    }
    //}

    private void OnOkClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.SaveSettings();
        CloseAsync();
    }

    private async void OpenPrgEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Prg");
        }
    }

    private async void OpenDocEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile);
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Doc");
        }
    }

    private async void OpenIconEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Icon");
        }
    }

    private async void ResetPrg(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.standardeinstellungen_laden);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
            if (File.Exists(filePath))
                File.Delete(filePath);

            SettingsService.Instance.ResetSettingsToDefaults();
            SettingsService.Instance.SaveSettings();
        }
    }

    private async void ResetIcon(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.standardeinstellungen_laden);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            var filePath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
            if (File.Exists(filePath))
                File.Delete(filePath);

            await Helper.CopyFileFromResourcesAsync("IconData.xml", Path.Combine(Settings.TemplateDirectory, "IconData.xml"));

            // Icon-Daten einlesen
            Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            IconLookup.Initialize(Settings.IconData);
        }
    }
}
