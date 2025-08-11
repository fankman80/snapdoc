
namespace SnapDoc;

internal class Functions
{
    static double DecToSexAngle(double dec)
    {
        int deg = (int)Math.Floor(dec);
        int min = (int)Math.Floor((dec - deg) * 60);
        double sec = (((dec - deg) * 60) - min) * 60;

        return deg + (double)min / 100 + (double)sec / 10000;
    }

    static double SexAngleToSeconds(double dms)
    {
        double deg, min, sec;
        deg = Math.Floor(dms);
        min = Math.Floor((dms - deg) * 100);
        sec = (((dms - deg) * 100) - min) * 100;

        return sec + (double)min * 60 + (double)deg * 3600;
    }

    static double WGStoCHy(double lat, double lng)
    {
        lat = DecToSexAngle(lat);
        lng = DecToSexAngle(lng);

        lat = SexAngleToSeconds(lat);
        lng = SexAngleToSeconds(lng);

        double lat_aux = (lat - 169028.66) / 10000;
        double lng_aux = (lng - 26782.5) / 10000;

        double y = 600072.37 + 211455.93 * lng_aux - 10938.51 * lng_aux * lat_aux - 0.36 * lng_aux * Math.Pow(lat_aux, 2) - 44.54 * Math.Pow(lng_aux, 3);

        return y;
    }

    static double WGStoCHx(double lat, double lng)
    {
        lat = DecToSexAngle(lat);
        lng = DecToSexAngle(lng);

        lat = SexAngleToSeconds(lat);
        lng = SexAngleToSeconds(lng);

        double lat_aux = (lat - 169028.66) / 10000;
        double lng_aux = (lng - 26782.5) / 10000;

        double x = 200147.07 + 308807.95 * lat_aux + 3745.25 * Math.Pow(lng_aux, 2) + 76.63 * Math.Pow(lat_aux, 2) - 194.56 * Math.Pow(lng_aux, 2) * lat_aux + 119.79 * Math.Pow(lat_aux, 3);

        return x;
    }

    public static void LLtoSwissGrid(double Lat, double Long, out double SwissEasting, out double SwissNorthing)
    {
        SwissNorthing = 1000000 + WGStoCHx(Lat, Long);
        SwissEasting = 2000000 + WGStoCHy(Lat, Long);
    }
}
