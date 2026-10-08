using System.Text.Json;
using Anthro.Sim.Discovery;

namespace Anthro.Runner;

/// <summary>Reads docs/discovery-web.json into the sim's data model.</summary>
public static class WebLoader
{
    sealed class FileDto { public List<TileDto> Tiles { get; set; } = new(); }
    sealed class TileDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Sum { get; set; } = "";
        public List<StageDto> Stages { get; set; } = new();
    }
    sealed class StageDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Short { get; set; } = "";
        public bool Known { get; set; }
        public string? Variant { get; set; }
        public string Does { get; set; } = "";
        public string? Accident { get; set; }
        public List<NeedDto> Needs { get; set; } = new();
        public SimDto? Sim { get; set; }
    }
    sealed class SimDto
    {
        public List<string> During { get; set; } = new();
        public List<string> Materials { get; set; } = new();
        public List<string> Pressures { get; set; } = new();
        public string? Interest { get; set; }
        public string? Skill { get; set; }
        public string? Trait { get; set; }
        public string? Way { get; set; }
        public double Rate { get; set; } = 1;
    }
    sealed class NeedDto
    {
        public string T { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> Refs { get; set; } = new();
        public bool Any { get; set; }
    }

    public static DiscoveryWeb Load(string path)
    {
        var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? throw new InvalidDataException($"Could not read {path}");
        var tiles = dto.Tiles.Select(t => new SkillTile
        {
            Id = t.Id, Name = t.Name, Kind = t.Kind, Summary = t.Sum,
            Stages = t.Stages.Select(s => new Stage
            {
                Id = s.Id, Name = s.Name, Short = s.Short, Known = s.Known, Variant = s.Variant, Does = s.Does, Accident = s.Accident,
                Needs = s.Needs.Select(n => new Need { Type = n.T, Text = n.Text, Refs = n.Refs, Any = n.Any }).ToList(),
                Sim = s.Sim == null ? null : new SimRules
                {
                    During = s.Sim.During, Materials = s.Sim.Materials, Pressures = s.Sim.Pressures,
                    Interest = s.Sim.Interest, Skill = s.Sim.Skill, Trait = s.Sim.Trait, Way = s.Sim.Way, Rate = s.Sim.Rate,
                },
            }).ToList(),
        });
        return new DiscoveryWeb(tiles);
    }

    /// <summary>Walks up from the app folder to find docs/discovery-web.json in the repo.</summary>
    public static string? Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "discovery-web.json");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
