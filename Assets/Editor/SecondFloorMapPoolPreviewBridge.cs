using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Serves local PlotWeave previews with the same map generator used at runtime.</summary>
[InitializeOnLoad]
internal static class SecondFloorMapPoolPreviewBridge
{
    private const string RequestSuffix = ".request.json";
    private const string ClaimSuffix = ".processing.json";
    private const string ResponseSuffix = ".response.json";
    private const string PatternFolder = "Assets/Resources/Data/Maps/Patterns";
    private const double PollIntervalSeconds = 0.25;
    // Node's client timeout is shorter. Do not generate previews left behind by a stopped client.
    private static readonly TimeSpan MaxRequestAge = TimeSpan.FromSeconds(20);
    private const int MaxRequestBytes = 513 * 1024;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly Regex RequestName = new Regex(@"\A([0-9a-f]{32})\.request\.json\z");
    private static readonly Regex LibraryId = new Regex(@"\A[A-Za-z0-9_-]{1,64}\z");
    private static readonly Regex Sha256Hex = new Regex(@"\A[0-9A-Fa-f]{64}\z");
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string SpoolPath = Path.Combine(ProjectRoot, "Library", "Locus", "tmp", "plotweave-map-pools");
    private static double nextPoll;
    private static double nextWarning;
    private static bool busy;

    static SecondFloorMapPoolPreviewBridge() => EditorApplication.update += Update;

    private static void Update()
    {
        double now = EditorApplication.timeSinceStartup;
        if (busy || now < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        nextPoll = now + PollIntervalSeconds;
        busy = true;
        try
        {
            if (!Directory.Exists(SpoolPath)) return;
            RequireNoReparsePoints(SpoolPath);
            foreach (string candidate in Directory.EnumerateFiles(SpoolPath, "*" + RequestSuffix))
            {
                Match match = RequestName.Match(Path.GetFileName(candidate));
                if (!match.Success) continue;
                Process(match.Groups[1].Value);
                break;
            }
        }
        catch (Exception ex)
        {
            // If the spool itself is inaccessible there is nowhere to write a response.
            if (now >= nextWarning)
            {
                nextWarning = now + 10;
                Debug.LogWarning($"PlotWeave map-pool preview spool: {ex.Message}");
            }
        }
        finally
        {
            busy = false;
        }
    }

    private static void Process(string id)
    {
        string requestPath = Path.Combine(SpoolPath, id + RequestSuffix);
        string claimPath = Path.Combine(SpoolPath, id + ClaimSuffix);
        string responsePath = Path.Combine(SpoolPath, id + ResponseSuffix);
        try
        {
            File.Move(requestPath, claimPath);
        }
        catch (FileNotFoundException)
        {
            return; // Node cancelled the request before we claimed it.
        }

        try
        {
            object response;
            try
            {
                if (!File.Exists(claimPath)) return;
                RequireNoReparsePoints(claimPath);
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(claimPath) > MaxRequestAge) return;
                response = GeneratePreview(ReadRequest(claimPath));
            }
            catch (Exception ex)
            {
                response = new { error = ex.Message };
            }

            if (!File.Exists(claimPath)) return;
            string temporaryPath = responsePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, Utf8))
                    writer.Write(JsonConvert.SerializeObject(response));
                if (!File.Exists(claimPath)) return;
                File.Move(temporaryPath, responsePath);
                // Node deletes request, claim, then response. If cancellation raced the move, remove the late response.
                if (!File.Exists(claimPath)) File.Delete(responsePath);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
        finally
        {
            File.Delete(claimPath);
        }
    }

    private static JObject ReadRequest(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        using (var text = new StreamReader(stream, Utf8))
        using (var json = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
        {
            if (stream.Length > MaxRequestBytes) throw new FormatException("Preview request exceeds the size limit.");
            var request = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (json.Read() || request.Count != 6 || !(request["config"] is JObject))
                throw new FormatException("Expected a preview request with config, poolId, entryId, seed, patternLibraryId, and patternSha256.");
            return request;
        }
    }

    private static object GeneratePreview(JObject request)
    {
        string poolId = GetString(request, "poolId");
        string entryId = GetString(request, "entryId");
        string patternId = GetString(request, "patternLibraryId");
        string patternHash = GetString(request, "patternSha256");
        if (request["seed"]?.Type != JTokenType.Integer || (double)request["seed"] < int.MinValue || (double)request["seed"] > int.MaxValue)
            throw new FormatException("Preview seed must be a signed 32-bit integer.");
        int seed = (int)request["seed"];
        if (!LibraryId.IsMatch(patternId)) throw new FormatException("Invalid pattern library ID.");
        if (!Sha256Hex.IsMatch(patternHash)) throw new FormatException("Invalid pattern library SHA-256.");

        var pools = SecondFloorMapPools.Read(request["config"].ToString(Formatting.None));
        string recipePatternId = pools.GetEntry(poolId, entryId).PatternLibraryId;
        if (!string.Equals(recipePatternId, patternId, StringComparison.Ordinal))
            throw new FormatException($"Entry '{entryId}' in pool '{poolId}' requires pattern library '{recipePatternId}', not '{patternId}'.");

        TextAsset patterns = LoadCurrentPatterns(patternId, patternHash);
        var generated = pools.Generate(poolId, entryId, seed, patterns);
        return ExportPreview(generated);
    }

    private static string GetString(JObject request, string field)
    {
        if (request[field]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)request[field]))
            throw new FormatException($"Preview request requires a non-empty '{field}' string.");
        return (string)request[field];
    }

    private static TextAsset LoadCurrentPatterns(string id, string expectedHash)
    {
        string assetPath = $"{PatternFolder}/{id}.patterns.json";
        string filePath = Path.Combine(ProjectRoot, assetPath);
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Published pattern library not found: {assetPath}");
        RequireNoReparsePoints(Path.GetFullPath(filePath));
        using (var sha256 = SHA256.Create())
        using (var file = File.OpenRead(filePath))
        {
            if (!HashMatches(sha256.ComputeHash(file), expectedHash))
                throw new InvalidOperationException($"Published pattern library '{id}' changed. Reload it in PlotWeave and retry the preview.");
        }

        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (AssetMatches(asset, expectedHash)) return asset;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (!AssetMatches(asset, expectedHash))
            throw new InvalidOperationException($"Unity did not import the requested bytes for pattern library '{id}'. Reload it in PlotWeave and retry.");
        return asset;
    }

    private static bool AssetMatches(TextAsset asset, string expectedHash)
    {
        if (!asset) return false;
        using (var sha256 = SHA256.Create())
            return HashMatches(sha256.ComputeHash(asset.bytes), expectedHash);
    }

    private static bool HashMatches(byte[] bytes, string expectedHash) =>
        string.Equals(BitConverter.ToString(bytes).Replace("-", ""), expectedHash, StringComparison.OrdinalIgnoreCase);

    private static void RequireNoReparsePoints(string path)
    {
        // Callers construct these paths exclusively from the project root, fixed folders and validated IDs.
        for (string current = path; !string.Equals(current, ProjectRoot, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("PlotWeave preview inputs cannot use symbolic links or junctions.");
        }
    }

    private static object ExportPreview(SecondFloorMapPools.GeneratedMap generated)
    {
        var map = generated.Map;
        var tiles = map.Tiles.OrderByDescending(tile => tile.Position.y).ThenBy(tile => tile.Position.x).ToArray();
        var cells = tiles.Select(tile => new
        {
            x = tile.Position.x,
            y = -tile.Position.y,
            type = tile.Type,
            enemyLevel = tile.EnemyLevel,
            stripIds = tile.StripIds
        }).ToArray();
        var edges = new List<object>();
        foreach (var tile in tiles)
        {
            AddEdge(map, tile.Position, Vector2Int.right, "E", edges);
            AddEdge(map, tile.Position, Vector2Int.down, "S", edges);
        }

        return new
        {
            floor = new
            {
                kind = "plotweave-floor-map",
                schemaVersion = 1,
                id = map.Id,
                spawn = new { x = map.Spawn.x, y = -map.Spawn.y },
                cells,
                edges
            },
            metrics = new
            {
                resource = generated.Resource,
                difficulty = generated.Difficulty,
                tileCount = cells.Length,
                width = cells.Max(cell => cell.x) - cells.Min(cell => cell.x) + 1,
                height = cells.Max(cell => cell.y) - cells.Min(cell => cell.y) + 1,
                resourceInRange = generated.ResourceWithinBudget,
                difficultyInRange = generated.DifficultyWithinBudget
            },
            layout = new
            {
                seed = generated.Seed,
                stripCounts = generated.StripCounts,
                strips = generated.Strips.Select(strip => new
                {
                    id = strip.Id,
                    slot = strip.Slot,
                    templateId = strip.TemplateId
                }).ToArray()
            }
        };
    }

    private static void AddEdge(SecondFloorFloorMap map, Vector2Int from, Vector2Int offset, string dir, List<object> edges)
    {
        var kind = map.GetEdge(from, from + offset);
        if (kind == SecondFloorEdgeKind.Wall) return;
        edges.Add(new { x = from.x, y = -from.y, dir, type = kind == SecondFloorEdgeKind.Door ? "door" : "open" });
    }
}
