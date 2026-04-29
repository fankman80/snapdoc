using Font = Microsoft.Maui.Graphics.Font;

namespace SnapDoc.Controls
{
    public class VerticalZoomSliderDrawable : IDrawable
    {
        public Color TrackColor { get; set; } = Colors.White;
        public Color ActiveTrackColor { get; set; } = Colors.Yellow;
        public Color ThumbColor { get; set; } = Colors.Yellow;
        public Color TextColor { get; set; } = Colors.Yellow;
        public float TrackWidth { get; set; } = 4f;
        public float ThumbRadius { get; set; } = 10f;
        public float FontSize { get; set; } = 14f;
        public double Minimum { get; set; } = 1.0;
        public double Maximum { get; set; } = 10.0;
        public double CurrentValue { get; set; } = 1.0;
        public Font FontStyle { get; set; } = Font.Default;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.Antialias = true;

            float padding = ThumbRadius + 5;
            float trackTop = dirtyRect.Top + padding;
            float trackBottom = dirtyRect.Bottom - padding;
            float trackHeight = trackBottom - trackTop;
            float sliderPercent = (float)((CurrentValue - Minimum) / (Maximum - Minimum));
            float thumbY = trackBottom - (sliderPercent * trackHeight);
            float drawX = dirtyRect.Right - ThumbRadius - 5;

            canvas.StrokeColor = TrackColor;
            canvas.StrokeSize = TrackWidth;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(drawX, trackTop, drawX, trackBottom);
            canvas.StrokeColor = ActiveTrackColor;
            canvas.DrawLine(drawX, trackBottom, drawX, thumbY);
            canvas.FillColor = ThumbColor;
            canvas.FillCircle(drawX, thumbY, ThumbRadius);

            string labelText = $"{CurrentValue:F1}x";
            canvas.FontColor = TextColor;
            canvas.FontSize = FontSize;
            canvas.Font = FontStyle;

            float textWidth = 60f; // Genug Platz für "10.0x"
            float textHeight = FontSize + 5;
            float textX = drawX - ThumbRadius - 10 - textWidth; // 10px Abstand zum Daumen
            float textY = thumbY - (textHeight / 2); // Vertikal zentrieren

            canvas.DrawString(labelText,
                              textX,
                              textY,
                              textWidth,
                              textHeight,
                              HorizontalAlignment.Right,
                              VerticalAlignment.Center);
        }
    }
}