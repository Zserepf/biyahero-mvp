namespace BiyaHero.Api.Features.Heatmap;

/// <summary>
/// AOT-safe static geohash encoder providing encoding, bounding-box decoding,
/// neighbor computation, and bbox-coverage utilities for heatmap tile aggregation.
/// 
/// Default precision 6 produces ~1.2 km × 0.6 km cells, suitable for heatmap tiles.
/// </summary>
public static class GeohashEncoder
{
    private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";

    private static readonly int[] CharToIndex = BuildCharToIndex();

    private static int[] BuildCharToIndex()
    {
        var table = new int[128];
        Array.Fill(table, -1);
        for (int i = 0; i < Base32Chars.Length; i++)
        {
            table[Base32Chars[i]] = i;
        }
        return table;
    }

    /// <summary>
    /// Encode a (latitude, longitude) pair into a geohash string at the given precision.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180).</param>
    /// <param name="precision">Number of characters in the resulting geohash (default 6, ~1.2km × 0.6km).</param>
    /// <returns>A geohash string of the specified length.</returns>
    public static string Encode(double latitude, double longitude, int precision = 6)
    {
        if (precision <= 0)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be positive.");

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;

        Span<char> result = stackalloc char[precision];
        int bit = 0;
        int charIndex = 0;
        int currentChar = 0;
        bool isLng = true;

        while (charIndex < precision)
        {
            if (isLng)
            {
                double mid = (lngMin + lngMax) / 2.0;
                if (longitude >= mid)
                {
                    currentChar |= (1 << (4 - bit));
                    lngMin = mid;
                }
                else
                {
                    lngMax = mid;
                }
            }
            else
            {
                double mid = (latMin + latMax) / 2.0;
                if (latitude >= mid)
                {
                    currentChar |= (1 << (4 - bit));
                    latMin = mid;
                }
                else
                {
                    latMax = mid;
                }
            }

            isLng = !isLng;
            bit++;

            if (bit == 5)
            {
                result[charIndex] = Base32Chars[currentChar];
                charIndex++;
                bit = 0;
                currentChar = 0;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Decode a geohash string into its bounding box.
    /// </summary>
    /// <param name="geohash">A valid geohash string.</param>
    /// <returns>A tuple (minLat, minLon, maxLat, maxLon) representing the cell's bounding box.</returns>
    public static (double minLat, double minLon, double maxLat, double maxLon) DecodeBbox(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
            throw new ArgumentException("Geohash must not be null or empty.", nameof(geohash));

        double latMin = -90.0, latMax = 90.0;
        double lngMin = -180.0, lngMax = 180.0;
        bool isLng = true;

        for (int i = 0; i < geohash.Length; i++)
        {
            char c = geohash[i];
            if (c >= 128 || CharToIndex[c] < 0)
                throw new ArgumentException($"Invalid geohash character: '{c}'.", nameof(geohash));

            int charValue = CharToIndex[c];

            for (int bit = 4; bit >= 0; bit--)
            {
                if (isLng)
                {
                    double mid = (lngMin + lngMax) / 2.0;
                    if ((charValue & (1 << bit)) != 0)
                        lngMin = mid;
                    else
                        lngMax = mid;
                }
                else
                {
                    double mid = (latMin + latMax) / 2.0;
                    if ((charValue & (1 << bit)) != 0)
                        latMin = mid;
                    else
                        latMax = mid;
                }

                isLng = !isLng;
            }
        }

        return (latMin, lngMin, latMax, lngMax);
    }

    /// <summary>
    /// Returns the 8 neighboring geohash cells (N, NE, E, SE, S, SW, W, NW).
    /// </summary>
    /// <param name="geohash">A valid geohash string.</param>
    /// <returns>An array of 8 geohash strings representing the neighbors.</returns>
    public static string[] GetNeighbors(string geohash)
    {
        if (string.IsNullOrEmpty(geohash))
            throw new ArgumentException("Geohash must not be null or empty.", nameof(geohash));

        var (minLat, minLon, maxLat, maxLon) = DecodeBbox(geohash);

        double latCenter = (minLat + maxLat) / 2.0;
        double lonCenter = (minLon + maxLon) / 2.0;
        double latSize = maxLat - minLat;
        double lonSize = maxLon - minLon;

        int precision = geohash.Length;

        // Offsets for N, NE, E, SE, S, SW, W, NW
        (double dLat, double dLon)[] offsets =
        [
            (latSize, 0),          // N
            (latSize, lonSize),    // NE
            (0, lonSize),          // E
            (-latSize, lonSize),   // SE
            (-latSize, 0),         // S
            (-latSize, -lonSize),  // SW
            (0, -lonSize),         // W
            (latSize, -lonSize),   // NW
        ];

        var neighbors = new string[8];
        for (int i = 0; i < 8; i++)
        {
            double neighborLat = latCenter + offsets[i].dLat;
            double neighborLon = lonCenter + offsets[i].dLon;

            // Clamp latitude to valid range
            neighborLat = Math.Clamp(neighborLat, -90.0 + 1e-10, 90.0 - 1e-10);

            // Wrap longitude to [-180, 180)
            if (neighborLon >= 180.0)
                neighborLon -= 360.0;
            else if (neighborLon < -180.0)
                neighborLon += 360.0;

            neighbors[i] = Encode(neighborLat, neighborLon, precision);
        }

        return neighbors;
    }

    /// <summary>
    /// Returns all geohash cells at the given precision that overlap with the specified bounding box.
    /// Used for querying heatmap tiles within a map viewport.
    /// </summary>
    /// <param name="minLat">Minimum latitude of the bounding box.</param>
    /// <param name="minLon">Minimum longitude of the bounding box.</param>
    /// <param name="maxLat">Maximum latitude of the bounding box.</param>
    /// <param name="maxLon">Maximum longitude of the bounding box.</param>
    /// <param name="precision">Geohash precision (default 6).</param>
    /// <returns>A list of geohash strings that overlap with the bounding box.</returns>
    public static List<string> GetGeohashesInBbox(double minLat, double minLon, double maxLat, double maxLon, int precision = 6)
    {
        if (precision <= 0)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be positive.");

        var result = new List<string>();
        var visited = new HashSet<string>();

        // Compute approximate cell size at this precision
        int totalBits = 5 * precision;
        int lngBits = (totalBits + 1) / 2;
        int latBits = totalBits / 2;

        double latCellSize = 180.0 / (1 << latBits);
        double lngCellSize = 360.0 / (1 << lngBits);

        // Step through the bbox with half-cell steps to ensure full coverage
        double latStep = latCellSize * 0.5;
        double lngStep = lngCellSize * 0.5;

        // Clamp to valid ranges
        double clampedMinLat = Math.Max(minLat, -90.0);
        double clampedMaxLat = Math.Min(maxLat, 90.0);
        double clampedMinLon = Math.Max(minLon, -180.0);
        double clampedMaxLon = Math.Min(maxLon, 180.0);

        double lat = clampedMinLat;
        while (lat <= clampedMaxLat)
        {
            double lng = clampedMinLon;
            while (lng <= clampedMaxLon)
            {
                double encodeLat = Math.Min(lat, 90.0 - 1e-10);
                double encodeLng = Math.Min(lng, 180.0 - 1e-10);

                string hash = Encode(encodeLat, encodeLng, precision);
                if (visited.Add(hash))
                {
                    // Verify the cell actually intersects the query bbox
                    var (cellMinLat, cellMinLon, cellMaxLat, cellMaxLon) = DecodeBbox(hash);
                    if (Intersects(minLat, minLon, maxLat, maxLon, cellMinLat, cellMinLon, cellMaxLat, cellMaxLon))
                    {
                        result.Add(hash);
                    }
                }

                lng += lngStep;
            }
            lat += latStep;
        }

        // Check corners to ensure boundary cells are included
        AddIfIntersects(minLat, minLon, precision, minLat, minLon, maxLat, maxLon, visited, result);
        AddIfIntersects(minLat, maxLon, precision, minLat, minLon, maxLat, maxLon, visited, result);
        AddIfIntersects(maxLat, minLon, precision, minLat, minLon, maxLat, maxLon, visited, result);
        AddIfIntersects(maxLat, maxLon, precision, minLat, minLon, maxLat, maxLon, visited, result);

        return result;
    }

    private static bool Intersects(
        double aMinLat, double aMinLon, double aMaxLat, double aMaxLon,
        double bMinLat, double bMinLon, double bMaxLat, double bMaxLon)
    {
        return aMinLat <= bMaxLat
            && aMaxLat >= bMinLat
            && aMinLon <= bMaxLon
            && aMaxLon >= bMinLon;
    }

    private static void AddIfIntersects(
        double lat, double lon, int precision,
        double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon,
        HashSet<string> visited, List<string> result)
    {
        double clampedLat = Math.Clamp(lat, -90.0, 90.0 - 1e-10);
        double clampedLon = Math.Clamp(lon, -180.0, 180.0 - 1e-10);

        string hash = Encode(clampedLat, clampedLon, precision);
        if (visited.Add(hash))
        {
            var (cellMinLat, cellMinLon, cellMaxLat, cellMaxLon) = DecodeBbox(hash);
            if (Intersects(bboxMinLat, bboxMinLon, bboxMaxLat, bboxMaxLon, cellMinLat, cellMinLon, cellMaxLat, cellMaxLon))
            {
                result.Add(hash);
            }
        }
    }
}
