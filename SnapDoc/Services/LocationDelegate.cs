using Shiny;
using Shiny.Locations;
using System;

namespace SnapDoc.Services;

public class LocationDelegate : IGpsDelegate
{
    public Task OnError(GpsError error)
    {
        Console.WriteLine($"GPS Error: {error}");
        return Task.CompletedTask;
    }

    public Task OnReading(GpsReading reading)
    {
        // Übergib den Standort an dein ViewModel
        GPSViewModel.Instance.OnGpsReading(reading);
        return Task.CompletedTask;
    }
}
