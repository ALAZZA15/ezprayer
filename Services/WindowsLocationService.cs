using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace EzPrayer.Services
{
    public sealed class WindowsLocationService
    {
        public async Task<PrayerLocation> DetectAsync()
        {
            GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                throw new UnauthorizedAccessException(
                    "Izin lokasi Windows tidak diberikan. Aktifkan layanan lokasi dan izinkan aplikasi desktop mengakses lokasi.");
            }

            var geolocator = new Geolocator { DesiredAccuracyInMeters = 1000 };
            Geoposition position;
            try
            {
                position = await geolocator.GetGeopositionAsync(
                    TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    "Windows tidak memperoleh lokasi dalam 15 detik. Periksa layanan lokasi dan coba lagi.", ex);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException(
                    "Layanan lokasi Windows tidak tersedia atau aksesnya dinonaktifkan.", ex);
            }

            double latitude = position.Coordinate.Point.Position.Latitude;
            double longitude = position.Coordinate.Point.Position.Longitude;
            return new PrayerLocation(
                "Lokasi otomatis", latitude, longitude, TimeZoneInfo.Local.Id);
        }
    }
}
