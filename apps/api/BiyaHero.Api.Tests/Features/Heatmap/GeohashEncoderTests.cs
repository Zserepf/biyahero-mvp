using BiyaHero.Api.Features.Heatmap;
using Xunit;

namespace BiyaHero.Api.Tests.Features.Heatmap;

public class GeohashEncoderTests
{
    // ─── Encode Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Encode_DefaultPrecision_Returns6Characters()
    {
        string hash = GeohashEncoder.Encode(14.5995, 120.9842);
        Assert.Equal(6, hash.Length);
    }

    [Fact]
    public void Encode_KnownLocation_Manila()
    {
        // Manila at precision 6
        string hash = GeohashEncoder.Encode(14.5995, 120.9842, 6);
        Assert.Equal(6, hash.Length);
        Assert.StartsWith("wdw5", hash);
    }

    [Fact]
    public void Encode_OriginPoint()
    {
        string hash = GeohashEncoder.Encode(0.0, 0.0, 5);
        Assert.Equal("s0000", hash);
    }

    [Fact]
    public void Encode_NegativeCoordinates()
    {
        string hash = GeohashEncoder.Encode(-45.0, -90.0, 6);
        Assert.Equal(6, hash.Length);
        const string validChars = "0123456789bcdefghjkmnpqrstuvwxyz";
        Assert.All(hash, c => Assert.Contains(c, validChars));
    }

    [Fact]
    public void Encode_InvalidPrecision_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeohashEncoder.Encode(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeohashEncoder.Encode(0, 0, -1));
    }

    [Fact]
    public void Encode_SameInput_SameOutput()
    {
        string hash1 = GeohashEncoder.Encode(14.5995, 120.9842);
        string hash2 = GeohashEncoder.Encode(14.5995, 120.9842);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Encode_UsesOnlyValidBase32Characters()
    {
        const string validChars = "0123456789bcdefghjkmnpqrstuvwxyz";
        string hash = GeohashEncoder.Encode(14.5995, 120.9842, 12);
        Assert.All(hash, c => Assert.Contains(c, validChars));
    }

    // ─── DecodeBbox Tests ─────────────────────────────────────────────────

    [Fact]
    public void DecodeBbox_ValidGeohash_ReturnsBoundingBox()
    {
        var (minLat, minLon, maxLat, maxLon) = GeohashEncoder.DecodeBbox("wdw5e0");
        Assert.True(minLat < maxLat);
        Assert.True(minLon < maxLon);
    }

    [Fact]
    public void DecodeBbox_OriginGeohash_ContainsOrigin()
    {
        var (minLat, minLon, maxLat, maxLon) = GeohashEncoder.DecodeBbox("s0000");
        Assert.True(minLat <= 0.0 && maxLat >= 0.0);
        Assert.True(minLon <= 0.0 && maxLon >= 0.0);
    }

    [Fact]
    public void DecodeBbox_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeohashEncoder.DecodeBbox(null!));
        Assert.Throws<ArgumentException>(() => GeohashEncoder.DecodeBbox(""));
    }

    [Fact]
    public void DecodeBbox_InvalidCharacter_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeohashEncoder.DecodeBbox("abcde"));
    }

    // ─── Round-trip Tests ─────────────────────────────────────────────────

    [Theory]
    [InlineData(14.5995, 120.9842, 6)]
    [InlineData(-33.8688, 151.2093, 6)]
    [InlineData(51.5074, -0.1278, 6)]
    [InlineData(0.0, 0.0, 5)]
    [InlineData(-89.9, -179.9, 5)]
    [InlineData(89.9, 179.9, 5)]
    [InlineData(14.5995, 120.9842, 7)]
    public void Encode_ThenDecodeBbox_PointInsideBbox(double lat, double lng, int precision)
    {
        string hash = GeohashEncoder.Encode(lat, lng, precision);
        var (minLat, minLon, maxLat, maxLon) = GeohashEncoder.DecodeBbox(hash);

        Assert.True(lat >= minLat && lat <= maxLat,
            $"Latitude {lat} not in [{minLat}, {maxLat}]");
        Assert.True(lng >= minLon && lng <= maxLon,
            $"Longitude {lng} not in [{minLon}, {maxLon}]");
    }

    // ─── GetNeighbors Tests ───────────────────────────────────────────────

    [Fact]
    public void GetNeighbors_Returns8Neighbors()
    {
        string[] neighbors = GeohashEncoder.GetNeighbors("wdw5e0");
        Assert.Equal(8, neighbors.Length);
    }

    [Fact]
    public void GetNeighbors_AllSamePrecision()
    {
        string geohash = "wdw5e0";
        string[] neighbors = GeohashEncoder.GetNeighbors(geohash);
        Assert.All(neighbors, n => Assert.Equal(geohash.Length, n.Length));
    }

    [Fact]
    public void GetNeighbors_DoesNotContainSelf()
    {
        string geohash = "wdw5e0";
        string[] neighbors = GeohashEncoder.GetNeighbors(geohash);
        Assert.DoesNotContain(geohash, neighbors);
    }

    [Fact]
    public void GetNeighbors_AllDistinct()
    {
        string[] neighbors = GeohashEncoder.GetNeighbors("wdw5e0");
        Assert.Equal(8, neighbors.Distinct().Count());
    }

    [Fact]
    public void GetNeighbors_NeighborBboxesAreAdjacent()
    {
        string geohash = "wdw5e0";
        var (minLat, minLon, maxLat, maxLon) = GeohashEncoder.DecodeBbox(geohash);
        double latCenter = (minLat + maxLat) / 2.0;
        double lonCenter = (minLon + maxLon) / 2.0;
        double latSize = maxLat - minLat;
        double lonSize = maxLon - minLon;

        string[] neighbors = GeohashEncoder.GetNeighbors(geohash);

        // Each neighbor's center should be approximately one cell-width away
        foreach (var neighbor in neighbors)
        {
            var (nMinLat, nMinLon, nMaxLat, nMaxLon) = GeohashEncoder.DecodeBbox(neighbor);
            double nLatCenter = (nMinLat + nMaxLat) / 2.0;
            double nLonCenter = (nMinLon + nMaxLon) / 2.0;

            double latDist = Math.Abs(nLatCenter - latCenter);
            double lonDist = Math.Abs(nLonCenter - lonCenter);

            // Neighbor should be within ~1.5 cell widths in each direction
            Assert.True(latDist <= latSize * 1.5 + 1e-8,
                $"Neighbor {neighbor} lat distance {latDist} exceeds expected {latSize * 1.5}");
            Assert.True(lonDist <= lonSize * 1.5 + 1e-8,
                $"Neighbor {neighbor} lon distance {lonDist} exceeds expected {lonSize * 1.5}");
        }
    }

    [Fact]
    public void GetNeighbors_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeohashEncoder.GetNeighbors(null!));
        Assert.Throws<ArgumentException>(() => GeohashEncoder.GetNeighbors(""));
    }

    [Fact]
    public void GetNeighbors_NearPole_DoesNotThrow()
    {
        // Near north pole
        string hash = GeohashEncoder.Encode(89.9, 0.0, 6);
        string[] neighbors = GeohashEncoder.GetNeighbors(hash);
        Assert.Equal(8, neighbors.Length);
    }

    [Fact]
    public void GetNeighbors_NearDateLine_DoesNotThrow()
    {
        // Near the international date line
        string hash = GeohashEncoder.Encode(0.0, 179.9, 6);
        string[] neighbors = GeohashEncoder.GetNeighbors(hash);
        Assert.Equal(8, neighbors.Length);
    }

    // ─── GetGeohashesInBbox Tests ─────────────────────────────────────────

    [Fact]
    public void GetGeohashesInBbox_SmallArea_ReturnsNonEmpty()
    {
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.59, 120.97, 14.61, 120.99);
        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(6, h.Length));
    }

    [Fact]
    public void GetGeohashesInBbox_DefaultPrecision6()
    {
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.5, 120.9, 14.7, 121.1);
        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(6, h.Length));
    }

    [Fact]
    public void GetGeohashesInBbox_CustomPrecision7()
    {
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.59, 120.97, 14.61, 120.99, 7);
        Assert.NotEmpty(hashes);
        Assert.All(hashes, h => Assert.Equal(7, h.Length));
    }

    [Fact]
    public void GetGeohashesInBbox_AllCellsIntersectBbox()
    {
        double minLat = 14.5, minLon = 120.9, maxLat = 14.7, maxLon = 121.1;
        var hashes = GeohashEncoder.GetGeohashesInBbox(minLat, minLon, maxLat, maxLon);

        foreach (var hash in hashes)
        {
            var (cellMinLat, cellMinLon, cellMaxLat, cellMaxLon) = GeohashEncoder.DecodeBbox(hash);
            bool intersects = minLat <= cellMaxLat && maxLat >= cellMinLat
                           && minLon <= cellMaxLon && maxLon >= cellMinLon;
            Assert.True(intersects, $"Cell {hash} does not intersect the query bbox");
        }
    }

    [Fact]
    public void GetGeohashesInBbox_ContainsHashOfPointInBbox()
    {
        double lat = 14.6, lng = 121.0;
        string pointHash = GeohashEncoder.Encode(lat, lng, 6);
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.5, 120.9, 14.7, 121.1);
        Assert.Contains(pointHash, hashes);
    }

    [Fact]
    public void GetGeohashesInBbox_NoDuplicates()
    {
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.5, 120.9, 14.7, 121.1);
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }

    [Fact]
    public void GetGeohashesInBbox_InvalidPrecision_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GeohashEncoder.GetGeohashesInBbox(14.5, 120.9, 14.7, 121.1, 0));
    }

    [Fact]
    public void GetGeohashesInBbox_MetroManila_ReasonableCellCount()
    {
        // Metro Manila approximate bbox at precision 6
        var hashes = GeohashEncoder.GetGeohashesInBbox(14.35, 120.90, 14.75, 121.10);
        Assert.NotEmpty(hashes);
        // At precision 6 (~1.2km cells), Metro Manila should have a manageable number
        Assert.True(hashes.Count > 0 && hashes.Count < 500,
            $"Expected reasonable cell count for Metro Manila at precision 6, got {hashes.Count}");
    }
}
