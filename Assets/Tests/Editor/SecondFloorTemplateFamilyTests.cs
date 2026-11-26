using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class SecondFloorTemplateFamilyTests
{
    [Test]
    public void ReadsFlatChildBeforeRootAndKeepsItsOwnContent()
    {
        var root = FixtureTemplate();
        root["visualTypeId"] = "station";
        var child = CopyChild(root, "child");
        child.Remove("visualTypeId");
        child["name"] = "Independent title";
        child["tags"] = new JArray("content");
        child["regionIds"] = new JArray("B", "A");
        child["cells"][1]["type"] = "combat";
        child["cells"][1]["difficultyOffset"] = 3;
        child["cells"][1]["tags"] = new JArray("alternate");
        child["metrics"]["resourceOverride"] = 17;
        child["cells"] = new JArray(child["cells"].Reverse());
        child["openEdges"] = new JArray(child["openEdges"].Reverse());

        var library = Read(FixtureDocument(child, root));
        Assert.That(library.FamilyRoots.Select(t => t.Id), Is.EqualTo(new[] { "root" }));
        var member = library.Templates[0];
        Assert.That(member.ParentTemplateId, Is.EqualTo("root"));
        Assert.That(member.FamilyId, Is.EqualTo("root"));
        Assert.That(member.Name, Is.EqualTo("Independent title"));
        Assert.That(member.VisualTypeId, Is.EqualTo("station"));
        Assert.That(member.Resource, Is.EqualTo(17));
        Assert.That(member.Difficulty, Is.EqualTo(5));
        var cell = member.Variants[0].Cells.Single(c => c.Position == Vector2Int.right);
        Assert.That(cell.Type, Is.EqualTo("combat"));
        Assert.That(cell.DifficultyOffset, Is.EqualTo(3));
        Assert.That(cell.Tags, Is.EqualTo(new[] { "alternate" }));
    }

    [TestCase("missing", "unknown parent")]
    [TestCase("self", "reference itself")]
    [TestCase("nested", "directly reference a root")]
    [TestCase("cycle", "directly reference a root")]
    [TestCase("null", "must be a JSON String")]
    [TestCase("blank", "non-empty, trimmed")]
    [TestCase("footprint", "occupied cells and open edges")]
    [TestCase("edges", "occupied cells and open edges")]
    [TestCase("portal", "portal flag")]
    [TestCase("spawnCell", "spawn cell")]
    [TestCase("placedOrder", "placedOrder")]
    [TestCase("region", "topology settings")]
    [TestCase("rotate", "topology settings")]
    [TestCase("role", "topology settings")]
    [TestCase("spawnEligible", "topology settings")]
    [TestCase("noLoop", "topology settings")]
    [TestCase("visual", "visualTypeId must match")]
    [TestCase("max", "generation settings")]
    [TestCase("weight", "generation settings")]
    [TestCase("unique", "generation settings")]
    public void RejectsInvalidFamily(string change, string expectedError)
    {
        var root = FixtureTemplate();
        var child = CopyChild(root, "child");
        var nested = CopyChild(root, "nested");
        switch (change)
        {
            case "missing": child["parentTemplateId"] = "absent"; break;
            case "self": child["parentTemplateId"] = "child"; break;
            case "nested": child["parentTemplateId"] = "nested"; break;
            case "cycle": child["parentTemplateId"] = "nested"; nested["parentTemplateId"] = "child"; break;
            case "null": child["parentTemplateId"] = JValue.CreateNull(); break;
            case "blank": child["parentTemplateId"] = " "; break;
            case "footprint":
                foreach (var cell in child["cells"]) cell["rx"] = (int)cell["rx"] + 1;
                foreach (var edge in child["openEdges"]) edge["x"] = (int)edge["x"] + 1;
                break;
            case "edges": child["openEdges"][2]["x"] = 0; break;
            case "portal": child["cells"][1]["isPortal"] = true; break;
            case "spawnCell": child["cells"][0]["type"] = "empty"; child["cells"][1]["type"] = "spawn"; break;
            case "placedOrder": child["cells"][1]["placedOrder"] = 8; break;
            case "region": child["regionIds"] = new JArray("C"); break;
            case "rotate": child["allowRotate"] = false; break;
            case "role": root["spawnEligible"] = false; child["spawnEligible"] = false; child["role"] = "bridge"; break;
            case "spawnEligible": child["spawnEligible"] = false; break;
            case "noLoop": root["spawnEligible"] = false; child["spawnEligible"] = false; child["noLoop"] = true; break;
            case "visual": child["visualTypeId"] = "not-the-root"; break;
            case "max": child["generation"]["max"] = 1; break;
            case "weight": child["generation"]["weight"] = 5; break;
            case "unique": child["generation"]["unique"] = true; break;
        }
        var exception = Assert.Throws<FormatException>(() => Read(FixtureDocument(root, child, nested)));
        Assert.That(exception.Message, Does.Contain(expectedError));
    }

    [Test]
    public void MoreVersionsDoNotChangeFamilyOrGeometryAndActualVersionsDetermineMetrics()
    {
        var pools = ReadSamplePools();
        var original = SamplePatternDocument();
        var small = WithVersions(original, 1);
        var large = WithVersions(original, 8);
        var library = Read(large);
        var templates = library.Templates.ToDictionary(t => t.Id);

        const int seed = 31;
        var before = Generate(pools, small, seed);
        var after = Generate(pools, large, seed);
        Assert.That(after.Strips.Select(s => (s.Slot, templates[s.TemplateId].FamilyId, s.Origin, s.QuarterTurns)),
            Is.EqualTo(before.Strips.Select(s => (s.Slot, templates[s.TemplateId].FamilyId, s.Origin, s.QuarterTurns))));
        Assert.That(after.Joins.Select(j => (j.Left, j.Right, j.From, j.To)),
            Is.EqualTo(before.Joins.Select(j => (j.Left, j.Right, j.From, j.To))));
        Assert.That(after.Map.Spawn, Is.EqualTo(before.Map.Spawn));

        var chosen = after.Strips.Select(s => templates[s.TemplateId]).ToArray();
        Assert.That(chosen.Any(t => t.SpawnEligible && t.ParentTemplateId != null), Is.True);
        Assert.That(chosen.Any(t => t.Role == "normal" && !t.SpawnEligible && t.ParentTemplateId != null), Is.True);
        Assert.That(chosen.Any(t => t.Role == "bridge" && t.ParentTemplateId != null), Is.True);
        Assert.That(after.Resource, Is.EqualTo(chosen.Sum(t => t.Resource)));
        Assert.That(after.Difficulty, Is.EqualTo(chosen.Sum(t => t.Difficulty)));
        foreach (var family in chosen.GroupBy(t => t.FamilyId))
            Assert.That(family.Count(), Is.LessThanOrEqualTo(templates[family.Key].InstanceLimit));
        foreach (var strip in after.Strips.Where(s => templates[s.TemplateId].ParentTemplateId != null))
        {
            var tiles = after.Map.Tiles.Where(t => t.StripIds.Contains(strip.Id)).ToArray();
            Assert.That(tiles.All(t => t.Type == "family-test" || t.Type == "spawn"), Is.True);
            Assert.That(tiles.Any(t => t.Type == "family-test" && t.EnemyLevel.HasValue), Is.True);
        }
    }

    [Test]
    public void ChildrenCannotGiveAUniqueBridgeFamilyEnoughCapacityForThreeBridges()
    {
        var document = SamplePatternDocument();
        var templates = (JArray)document["stripTemplates"];
        var bridge = templates.OfType<JObject>().First(t => (string)t["role"] == "bridge");
        foreach (var other in templates.OfType<JObject>().Where(t => t != bridge && (string)t["role"] == "bridge").ToArray())
            other.Remove();
        bridge["generation"]["unique"] = true;
        for (int i = 0; i < 3; i++) templates.Add(CopyChild(bridge, "extra-bridge-" + i));
        var exception = Assert.Throws<InvalidOperationException>(() => Generate(ReadSamplePools(), document, 31));
        Assert.That(exception.Message, Does.Contain("no valid three-bridge layout"));
    }

    [Test]
    public void ChildNormalsDoNotAddCapacityToInitialRingFeasibility()
    {
        var document = SamplePatternDocument();
        var templates = (JArray)document["stripTemplates"];
        foreach (var normal in templates.OfType<JObject>().Where(t => (string)t["role"] == "normal" && !(bool)t["spawnEligible"]))
            normal["generation"]["max"] = 0;
        var lone = templates.OfType<JObject>().First(t => (string)t["role"] == "normal" && !(bool)t["spawnEligible"] && !(bool)t["noLoop"]);
        lone["generation"]["unique"] = true;
        lone["generation"]["max"] = 3;
        for (int i = 0; i < 3; i++) templates.Add(CopyChild(lone, "extra-normal-" + i));
        var exception = Assert.Throws<InvalidOperationException>(() => Generate(ReadSamplePools(), document, 31));
        Assert.That(exception.Message, Does.Contain("insufficient normal patterns"));
    }

    private static JObject WithVersions(JObject source, int count)
    {
        var document = (JObject)source.DeepClone();
        ((JArray)document["cellTypes"]).Add(JObject.Parse(@"{""id"":""family-test"",""resource"":10,""difficulty"":20,""supportsOffset"":true,""defaultOffset"":0}"));
        var templates = (JArray)document["stripTemplates"];
        foreach (var root in templates.OfType<JObject>().ToArray())
            for (int i = 0; i < count; i++)
            {
                var child = CopyChild(root, (string)root["id"] + "-version-" + i);
                child["metrics"]["resourceOverride"] = 500 + i;
                child["metrics"]["difficultyOverride"] = JValue.CreateNull();
                foreach (var cell in child["cells"].Where(c => (string)c["type"] != "spawn"))
                {
                    cell["type"] = "family-test";
                    cell["difficultyOffset"] = 1;
                    cell["tags"] = new JArray("version-" + i);
                }
                templates.Add(child);
            }
        return document;
    }

    private static SecondFloorMapPools ReadSamplePools()
    {
        var asset = Resources.Load<TextAsset>("Data/Maps/SecondFloor.map-pools");
        Assert.That(asset, Is.Not.Null);
        var pools = JObject.Parse(asset.text);
        ((JObject)pools["pools"][0]["entries"][0]).Remove("patternSha256");
        return SecondFloorMapPools.Read(pools.ToString());
    }

    private static JObject SamplePatternDocument()
    {
        var asset = Resources.Load<TextAsset>("Data/Maps/Patterns/Sample.patterns");
        Assert.That(asset, Is.Not.Null);
        return JObject.Parse(asset.text);
    }

    private static SecondFloorMapPools.GeneratedMap Generate(SecondFloorMapPools pools, JObject document, int seed)
    {
        var asset = new TextAsset(document.ToString()) { name = "Sample.patterns" };
        try { return pools.Generate("level-1", "standard-triangle", seed, asset); }
        finally { UnityEngine.Object.DestroyImmediate(asset); }
    }

    private static SecondFloorPatternLibrary Read(JObject document)
    {
        var asset = new TextAsset(document.ToString());
        try { return SecondFloorPatternLibrary.Read(asset); }
        finally { UnityEngine.Object.DestroyImmediate(asset); }
    }

    private static JObject CopyChild(JObject root, string id)
    {
        var child = (JObject)root.DeepClone();
        child["id"] = id;
        child["parentTemplateId"] = (string)root["id"];
        return child;
    }

    private static JObject FixtureDocument(params JObject[] templates) => new JObject
    {
        ["kind"] = "plotweave-strip-templates",
        ["schemaVersion"] = 3,
        ["cellTypes"] = JArray.Parse(@"[
            {""id"":""empty"",""resource"":0,""difficulty"":0,""supportsOffset"":false,""defaultOffset"":0},
            {""id"":""spawn"",""resource"":0,""difficulty"":0,""supportsOffset"":false,""defaultOffset"":0},
            {""id"":""combat"",""resource"":2,""difficulty"":5,""supportsOffset"":true,""defaultOffset"":0}
        ]"),
        ["stripTemplates"] = new JArray(templates)
    };

    private static JObject FixtureTemplate() => JObject.Parse(@"{
        ""id"":""root"",""name"":""Root"",""role"":""normal"",""spawnEligible"":true,""noLoop"":false,
        ""regionIds"":[""A"",""B""],""allowRotate"":true,
        ""metrics"":{""resourceOverride"":null,""difficultyOverride"":null},
        ""generation"":{""weight"":2,""max"":3,""unique"":false},
        ""cells"":[
            {""rx"":0,""ry"":0,""type"":""spawn"",""difficultyOffset"":0,""isPortal"":false,""placedOrder"":0},
            {""rx"":1,""ry"":0,""type"":""empty"",""difficultyOffset"":0,""isPortal"":false,""placedOrder"":1},
            {""rx"":0,""ry"":1,""type"":""empty"",""difficultyOffset"":0,""isPortal"":false,""placedOrder"":2},
            {""rx"":1,""ry"":1,""type"":""empty"",""difficultyOffset"":0,""isPortal"":false,""placedOrder"":3}
        ],
        ""openEdges"":[{""x"":0,""y"":0,""dir"":""E""},{""x"":0,""y"":1,""dir"":""E""},{""x"":1,""y"":0,""dir"":""S""}]
    }");
}
