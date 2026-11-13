namespace CoupledSim.Maps
{
    /// <summary>
    /// Dutch national grid RD New (EPSG:28992) to and from WGS84 latitude/longitude, with the
    /// polynomials of Schreutelkamp &amp; Strang van Hees (accurate to about 0.25 m in the Netherlands).
    /// Used for 3D BAG building models.
    /// </summary>
    public static class RdProjection
    {
        const double X0 = 155000.0, Y0 = 463000.0, Phi0 = 52.15517440, Lam0 = 5.38720621;
        static readonly double[,] K = { { 0, 1, 3235.65389 }, { 2, 0, -32.58297 }, { 0, 2, -0.24750 }, { 2, 1, -0.84978 }, { 0, 3, -0.06550 }, { 2, 2, -0.01709 }, { 1, 0, -0.00738 }, { 4, 0, 0.00530 }, { 2, 3, -0.00039 }, { 4, 1, 0.00033 }, { 1, 1, -0.00012 } };
        static readonly double[,] L = { { 1, 0, 5260.52916 }, { 1, 1, 105.94684 }, { 1, 2, 2.45656 }, { 3, 0, -0.81885 }, { 1, 3, 0.05594 }, { 3, 1, -0.05607 }, { 0, 1, 0.01199 }, { 3, 2, -0.00256 }, { 1, 4, 0.00128 }, { 0, 2, 0.00022 }, { 2, 0, -0.00022 }, { 5, 0, 0.00026 } };
        static readonly double[,] R = { { 0, 1, 190094.945 }, { 1, 1, -11832.228 }, { 2, 1, -114.221 }, { 0, 3, -32.391 }, { 1, 0, -0.705 }, { 3, 1, -2.340 }, { 1, 3, -0.608 }, { 0, 2, -0.008 }, { 2, 3, 0.148 } };
        static readonly double[,] S = { { 1, 0, 309056.544 }, { 0, 2, 3638.893 }, { 2, 0, 73.077 }, { 1, 2, -157.984 }, { 3, 0, 59.788 }, { 0, 1, 0.433 }, { 2, 2, -6.439 }, { 1, 1, -0.032 }, { 0, 4, 0.092 }, { 1, 4, -0.054 } };

        static double Sum(double[,] t, double a, double b)
        {
            double s = 0;
            for (int i = 0; i < t.GetLength(0); i++) s += t[i, 2] * System.Math.Pow(a, t[i, 0]) * System.Math.Pow(b, t[i, 1]);
            return s;
        }

        public static void ToWgs(double x, double y, out double lat, out double lon)
        {
            double dx = (x - X0) * 1e-5, dy = (y - Y0) * 1e-5;
            lat = Phi0 + Sum(K, dx, dy) / 3600.0;
            lon = Lam0 + Sum(L, dx, dy) / 3600.0;
        }

        public static void FromWgs(double lat, double lon, out double x, out double y)
        {
            double dp = 0.36 * (lat - Phi0), dl = 0.36 * (lon - Lam0);
            x = X0 + Sum(R, dp, dl);
            y = Y0 + Sum(S, dp, dl);
        }
    }
}
