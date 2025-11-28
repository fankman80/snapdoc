#nullable disable

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Layouts;
using MR.Gestures;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public string PinIcon;
    public string ImgSource = null;
    private int lineWidth = 6;
    private Color selectedColor = new(255, 0, 0);
    private float selectedOpacity = 0.5f;
    private bool isCleared = false;
    private bool hasFittedImage = false;
    private readonly TransformViewModel photoContainer;

    private readonly double density = DeviceDisplay.MainDisplayInfo.Density;
    private string drawMode = "none"; // "free" oder "poly"
    private CombinedDrawable combinedDrawable;
    private SKCanvasView drawingView;
    private int? activeIndex = null;
    private DateTime? lastClickTime = null;
    private SKPoint? lastClickPosition = null;

    public ImageViewPage()
    {
        InitializeComponent();
        photoContainer = new TransformViewModel();
        BindingContext = photoContainer;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        PhotoContainer.SizeChanged += ImageViewContainer_SizeChanged;
        DrawView.LineWidth = lineWidth;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        PhotoContainer.SizeChanged -= ImageViewContainer_SizeChanged;
    }

    private void ImageViewContainer_SizeChanged(object sender, EventArgs e)
    {
        if (hasFittedImage)
            return;

        if (PhotoContainer.Width < 10 || PhotoContainer.Height < 10)
            return;

        hasFittedImage = true;

        Dispatcher.Dispatch(() =>
        {
            ImageFit(null, null);
        });
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;

        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        if (query.TryGetValue("pinIcon", out object value3))
            PinIcon = value3 as string;

        if (query.TryGetValue("imgSource", out object value4))
        {
            ImgSource ??= value4 as string;

            string imgPath;
            if (ImgSource == "showTitle")
            {
                imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                this.Title = "Titelbild";
            }
            else
            {
                imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
                var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
                string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
                this.Title = formattedDate;
            }
            PhotoImage.Source = imgPath;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e)
    {
        ImageFit(null, null);
    }

    public void OnPinching(object sender, PinchEventArgs e)
    {
        photoContainer.IsPanningEnabled = false;

        if (combinedDrawable != null && drawMode == "poly")
        {
            combinedDrawable.PolyDrawable.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius * density / photoContainer.Scale);
            combinedDrawable.PolyDrawable.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius * density / photoContainer.Scale);
            drawingView.InvalidateSurface();
        }
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        photoContainer.IsPanningEnabled = true;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (photoContainer.IsPanningEnabled)
        {
            var scaleSpeed = 1 / PhotoContainer.Scale;
            double angle = PhotoContainer.Rotation * Math.PI / 180.0;
            double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
            double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);
            photoContainer.TranslationX += deltaX * scaleSpeed;
            photoContainer.TranslationY += deltaY * scaleSpeed;
            photoContainer.AnchorX = 1 / PhotoContainer.Width * ((this.Width / 2) - PhotoContainer.TranslationX);
            photoContainer.AnchorY = 1 / PhotoContainer.Height * ((this.Height / 2) - PhotoContainer.TranslationY);
        }
    }

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        var mousePos = e.Center;

        // Dynamischer Zoomfaktor basierend auf der aktuellen Skalierung
        double zoomFactor;
        if (photoContainer.Scale > 2) // Sehr stark vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.05 : 0.95;  // Sehr langsame Zoom-Änderung
        else if (photoContainer.Scale > 1) // Moderat vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9;  // Langsame Zoom-Änderung
        else // Wenig vergrößert oder sehr klein
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.15 : 0.85;  // Moderate Zoom-Änderung

        double targetScale = photoContainer.Scale * zoomFactor; ;
        double newAnchorX = 1 / PhotoContainer.Width * (mousePos.X - photoContainer.TranslationX);
        double newAnchorY = 1 / PhotoContainer.Height * (mousePos.Y - photoContainer.TranslationY);
        double deltaTranslationX = (PhotoContainer.Width * (newAnchorX - photoContainer.AnchorX)) * (targetScale / photoContainer.Scale - 1);
        double deltaTranslationY = (PhotoContainer.Height * (newAnchorY - photoContainer.AnchorY)) * (targetScale / photoContainer.Scale - 1);

        photoContainer.AnchorX = newAnchorX;
        photoContainer.AnchorY = newAnchorY;
        photoContainer.TranslationX -= deltaTranslationX;
        photoContainer.TranslationY -= deltaTranslationY;
        photoContainer.Scale = targetScale;

        if (combinedDrawable != null && drawMode == "poly")
        {
            combinedDrawable.PolyDrawable.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius * density / photoContainer.Scale);
            combinedDrawable.PolyDrawable.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius * density / photoContainer.Scale);
            drawingView.InvalidateSurface();
        }
    }

    private void ImageFit(object sender, EventArgs e)
    {
        var scale = Math.Min(this.Width / PhotoContainer.Width, this.Height / PhotoContainer.Height);
        photoContainer.Scale = scale;
        photoContainer.TranslationX = (this.Width - PhotoContainer.Width) / 2;
        photoContainer.TranslationY = (this.Height - PhotoContainer.Height) / 2;
        photoContainer.AnchorX = 1 / PhotoContainer.Width * ((this.Width / 2) - PhotoContainer.TranslationX);
        photoContainer.AnchorY = 1 / PhotoContainer.Height * ((this.Height / 2) - PhotoContainer.TranslationY);
    }

    private void OnDrawing(object sender, EventArgs e)
    {
        isCleared = false;
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Bild wirklich löschen?");
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            if (ImgSource == "showTitle")
            {
                // delete original image
                string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                if (File.Exists(file))
                    File.Delete(file);

                // delete thumbnail
                file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);
                if (File.Exists(file))
                    File.Delete(file);

                GlobalJson.Data.TitleImage = "banner_thumbnail.png";

                // save data to file
                GlobalJson.SaveToFile();

                await Shell.Current.GoToAsync($"..");
            }
            else
            {
                // delete original image
                string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
                if (File.Exists(file))
                    File.Delete(file);

                // delete overlay image
                file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
                if (File.Exists(file))
                    File.Delete(file);

                // delete thumbnail
                file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
                if (File.Exists(file))
                    File.Delete(file);

                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Remove(ImgSource);

                // save data to file
                GlobalJson.SaveToFile();

                await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}");
            }
        }
    }

    private void DrawingClicked(object sender, EventArgs e)
    {
        DrawBtn.IsVisible = false;
        ToolBtns.IsVisible = true;

        photoContainer.Rotation = 0;
        SettingsService.Instance.IsPlanRotateLocked = true;

        combinedDrawable = new CombinedDrawable
        {
            FreeDrawable = new InteractiveFreehandDrawable
            {
                LineColor = selectedColor.ToSKColor(),
                LineThickness = (float)(lineWidth * density)
            },
            PolyDrawable = new InteractivePolylineDrawable
            {
                FillColor = selectedColor.WithAlpha(selectedOpacity).ToSKColor(),
                LineColor = selectedColor.ToSKColor(),
                PointColor = SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
                StartPointColor = SKColor.Parse(SettingsService.Instance.PolyLineStartHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
                LineThickness = (float)(lineWidth * density),
                HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius * density / photoContainer.Scale),
                PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius * density / photoContainer.Scale)
            },
        };

        drawingView = new SKCanvasView
        {
            BackgroundColor = Colors.Transparent,
            EnableTouchEvents = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        drawingView.PaintSurface += OnPaintSurface;
        drawingView.Touch += OnTouch;

        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PhotoContainer");
        absoluteLayout.Children.Add(drawingView);
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutBounds(drawingView, new Rect(0, 0, 1, 1));
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutFlags(drawingView, AbsoluteLayoutFlags.All);

        Dispatcher.Dispatch(() =>
        {
            drawingView.InvalidateMeasure();
            drawingView.InvalidateSurface();
        });
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        combinedDrawable?.Draw(canvas);
    }

    private void OnTouch(object sender, SKTouchEventArgs e)
    {
        var p = e.Location;

        if (e.ActionType == SKTouchAction.Pressed)
            OnStartInteraction(p);
        else if (e.ActionType == SKTouchAction.Moved)
            OnDragInteraction(p);
        else if (e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled)
            OnEndInteraction();

        e.Handled = true;
    }

    private void OnStartInteraction(SKPoint p)
    {
        isCleared = false;

        if (drawMode == "poly")
        {
            var poly = combinedDrawable.PolyDrawable;

            // Doppelklick prüfen
            var now = DateTime.Now;
            if (lastClickTime.HasValue &&
                (now - lastClickTime.Value).TotalMilliseconds <= SettingsService.Instance.DoubleClickThresholdMs &&
                lastClickPosition.HasValue &&
                Distance(p, lastClickPosition.Value) <= SettingsService.Instance.PolyLineHandleRadius)
            {
                // Doppelklick erkannt → Punkt löschen
                DeletePointAt(p);
                lastClickTime = null;
                lastClickPosition = null;
                drawingView.InvalidateSurface();
                return;
            }

            // Kein Doppelklick → Klick merken
            lastClickTime = now;
            lastClickPosition = p;

            activeIndex = poly.FindPointIndex(p.X, p.Y);

            if (poly.TryClosePolygon(p.X, p.Y))
            {
                drawingView.InvalidateSurface();
                return;
            }

            if (activeIndex == null && !poly.IsClosed)
            {
                poly.Points.Add(p);
                drawingView.InvalidateSurface();
            }
        }
        else if (drawMode == "free")
        {
            var free = combinedDrawable.FreeDrawable;
            free.StartStroke();
            free.AddPoint(p);
            drawingView.InvalidateSurface();
        }
    }

    private void OnDragInteraction(SKPoint p)
    {
        if (drawMode == "poly")
        {
            if (activeIndex != null)
            {
                combinedDrawable.PolyDrawable.Points[(int)activeIndex] = p;
                drawingView.InvalidateSurface();
            }
        }
        else if (drawMode == "free")
        {
            combinedDrawable.FreeDrawable.AddPoint(p);
            drawingView.InvalidateSurface();
        }
    }

    private void OnEndInteraction()
    {
        if (drawMode == "poly")
        {
            activeIndex = null;
        }
        else if (drawMode == "free")
        {
            combinedDrawable.FreeDrawable.EndStroke();
        }
    }

    private void DeletePointAt(SKPoint p)
    {
        var poly = combinedDrawable.PolyDrawable;

        // Punkt unter Klick suchen und löschen
        for (int i = 0; i < poly.Points.Count; i++)
        {
            if (Distance(p, poly.Points[i]) <= poly.HandleRadius)
            {
                poly.Points.RemoveAt(i);

                // Wenn danach <=2 Punkte übrig, alles löschen
                if (poly.Points.Count <= 2)
                {
                    poly.Reset(); // Löscht alle Punkte und setzt IsClosed zurück
                }
                return;
            }
        }
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private void DrawFreeClicked(object sender, EventArgs e)
    {
        if (drawMode == "poly" || drawMode == "none")
        {
            photoContainer.IsPanningEnabled = false;
            drawMode = "free";
            DrawPolyBtn.BorderWidth = 0;
            DrawFreeBtn.BorderWidth = 2;
            combinedDrawable.PolyDrawable.DisplayHandles = false;
            drawingView.InvalidateSurface();
        }
        else
        {
            photoContainer.IsPanningEnabled = true;
            drawMode = "none";
            DrawFreeBtn.BorderWidth = 0;
            combinedDrawable.PolyDrawable.DisplayHandles = false;
            drawingView.InvalidateSurface();
        }
    }

    private void DrawPolyClicked(object sender, EventArgs e)
    {
        if (drawMode == "free" || drawMode == "none")
        {
            photoContainer.IsPanningEnabled = false;
            drawMode = "poly";
            DrawPolyBtn.BorderWidth = 2;
            DrawFreeBtn.BorderWidth = 0;
            combinedDrawable.PolyDrawable.DisplayHandles = true;
            drawingView.InvalidateSurface();
        }
        else
        {
            photoContainer.IsPanningEnabled = true;
            drawMode = "none";
            DrawPolyBtn.BorderWidth = 0;
            combinedDrawable.PolyDrawable.DisplayHandles = false;
            drawingView.InvalidateSurface();
        }
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        drawMode = "none";
        DrawPolyBtn.BorderWidth = 0;
        DrawFreeBtn.BorderWidth = 0;
        combinedDrawable.Reset();   // setzt beide Modi zurück
        drawingView.InvalidateSurface();  // neu rendern

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
        {
            isCleared = true;
            PhotoImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (drawingView != null && !IsDrawingViewEmpty() || isCleared == true)
        {
            var imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
            var origPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
            var thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, ImgSource);

            if (isCleared)
            {
                if (File.Exists(imgPath))
                    File.Delete(imgPath);
                File.Move(origPath, imgPath);

                //  
                imgPath = await FileRenamer(imgPath);
                thumbPath = await FileRenamer(thumbPath);
                _ = await FileRenamer(origPath);

                PhotoImage.Source = imgPath;

                Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
            }
            else
            {
                if (!Directory.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals")))
                    Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals"));

                if (!File.Exists(origPath))
                    File.Copy(imgPath, origPath);
                await SavePhotoWithOverlay(imgPath, imgPath);

                // Dateinamen anpassen
                imgPath = await FileRenamer(imgPath);
                thumbPath = await FileRenamer(thumbPath);
                _ = await FileRenamer(origPath);

                PhotoImage.Source = imgPath;

                Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = true;
            }

            // ändere Json-Key
            var fotos = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos;
            if (fotos.TryGetValue(ImgSource, out var value))
            {
                fotos.Remove(ImgSource);
                fotos[Path.GetFileName(imgPath)] = value;
                ImgSource = Path.GetFileName(imgPath);
            }

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File = Path.GetFileName(imgPath);

            // save data to file
            GlobalJson.SaveToFile();
        }
        RemoveDrawingView();

        drawMode = "none";
        DrawPolyBtn.BorderWidth = 0;
        DrawFreeBtn.BorderWidth = 0;
        photoContainer.IsPanningEnabled = true;
        ToolBtns.IsVisible = false;
        DrawBtn.IsVisible = true;
    }

    private static async Task<string> FileRenamer(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath).Split('_');
        var onlyPath = Path.GetDirectoryName(filePath);
        string newFileName;
        var extension = Path.GetExtension(filePath);

        if (name.Length == 3)
            newFileName = $"{name[0]}_{name[1]}_{name[2]}_{1}" + extension;
        else
        {
            if (Int32.TryParse(name[3], out int i))
                newFileName = $"{name[0]}_{name[1]}_{name[2]}_{i + 1}" + extension;
            else
                newFileName = $"{name[0]}_{name[1]}_{name[2]}_{0}" + extension;
        }

        if (File.Exists(Path.Combine(filePath)))
        {
            File.Move(filePath, Path.Combine(onlyPath, newFileName));
            return Path.Combine(onlyPath, newFileName);
        }
        else
            return filePath;
    }

    private void RemoveDrawingView()
    {
        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PhotoContainer");
        if (drawingView != null && absoluteLayout != null)
            absoluteLayout.Children.Remove(drawingView);
    }

    private bool IsDrawingViewEmpty()
    {
        if (combinedDrawable == null)
            return true;

        // Prüfe PolyDrawables
        if (combinedDrawable.PolyDrawable != null && combinedDrawable.PolyDrawable.Points.Count > 0)
            return false;

        // Prüfe FreeDrawables
        if (combinedDrawable.FreeDrawable != null && combinedDrawable.FreeDrawable.Strokes.Count > 0)
            return false;

        return true; // Nichts gezeichnet
    }

    public async Task SavePhotoWithOverlay(string photoPath, string outputPath)
    {
        using var photoStream = File.OpenRead(photoPath);
        using var photoBitmap = SKBitmap.Decode(photoStream);

        int width = photoBitmap.Width;
        int height = photoBitmap.Height;

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        canvas.DrawBitmap(photoBitmap, new SKPoint(0, 0));

        using (var overlaySurface = SKSurface.Create(info))
        {
            var overlayCanvas = overlaySurface.Canvas;

            overlayCanvas.Clear(SKColors.Transparent);

            float scaleX = width / drawingView.CanvasSize.Width;
            float scaleY = height / drawingView.CanvasSize.Height;

            overlayCanvas.Scale(scaleX, scaleY);

            combinedDrawable.PolyDrawable.DisplayHandles = false;
            combinedDrawable?.Draw(overlayCanvas);
            combinedDrawable.PolyDrawable.DisplayHandles = true;

            overlayCanvas.Flush();

            using var overlayImage = overlaySurface.Snapshot();
            var destRect = new SKRect(0, 0, width, height);

            canvas.DrawImage(overlayImage, destRect);
        }

        canvas.Flush();

        using var finalImage = surface.Snapshot();
        using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var output = File.Create(outputPath);
        data.SaveTo(output);
    }


    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(lineWidth, selectedColor, fillOpacity: (byte)(selectedOpacity * 255), lineWidthVisibility: true, fillOpacityVisibility: true);
        var result = await this.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            selectedColor = Color.FromArgb(result.Result.PenColorHex);
            selectedOpacity = 1f / 255f * result.Result.FillOpacity;
            lineWidth = result.Result.PenWidth;

            if (drawingView != null)
            {
                // Freihand aktualisieren
                combinedDrawable.FreeDrawable.LineColor = selectedColor.ToSKColor();
                combinedDrawable.FreeDrawable.LineThickness = (float)(lineWidth * density);

                // Polylinie aktualisieren
                combinedDrawable.PolyDrawable.LineColor = selectedColor.ToSKColor();
                combinedDrawable.PolyDrawable.FillColor = selectedColor.WithAlpha(selectedOpacity).ToSKColor();
                combinedDrawable.PolyDrawable.LineThickness = (float)(lineWidth * density);

                drawingView.InvalidateSurface();  // neu rendern
            }
        }
    }
}
