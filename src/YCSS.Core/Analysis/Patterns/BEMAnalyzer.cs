using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using YCSS.Core.Models;

namespace YCSS.Core.Analysis.Patterns
{
    public interface IBEMAnalyzer
    {
        Task<BEMAnalysis> AnalyzeAsync(
            Dictionary<string, object> styles,
            CancellationToken ct = default);
    }

    public record BEMAnalysis(
        IReadOnlyList<BEMComponent> Components,
        IReadOnlyList<BEMRelationship> Relationships,
        IReadOnlyList<BEMSuggestion> Suggestions
    );

    public record BEMComponent(
        string Name,
        string? Block,
        string? Element,
        string? Modifier,
        Dictionary<string, object> Styles,
        HashSet<string> Dependencies
    );

    public record BEMRelationship(
        string SourceComponent,
        string TargetComponent,
        RelationType Type,
        double Confidence
    );

    public record BEMSuggestion(
        SuggestionType Type,
        string Description,
        string Current,
        string Suggested,
        double Confidence,
        bool IsBlockLevel
    );

    public enum RelationType
    {
        Parent,          // block__element
        Modifier,        // block--modifier
        ElementModifier, // block__element--modifier
        Variant,        // Alternative version of a component
        Extension,      // Shares properties but not BEM relationship
        Composition     // Used together frequently
    }

    public class BEMAnalyzer : IBEMAnalyzer
    {
        private readonly ILogger<BEMAnalyzer> _logger;

        // BEM naming patterns
        private static readonly Regex BlockPattern = new(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$");
        private static readonly Regex ElementPattern = new(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*__[a-z0-9]+(?:-[a-z0-9]+)*$");
        private static readonly Regex ModifierPattern = new(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*--[a-z0-9]+(?:-[a-z0-9]+)*$");
        private static readonly Regex ElementModifierPattern = new(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*__[a-z0-9]+(?:-[a-z0-9]+)*--[a-z0-9]+(?:-[a-z0-9]+)*$");

        // Common component patterns
        private static readonly Dictionary<string, string[]> CommonElements = new()
        {
            ["card"] = new[] { "header", "body", "footer", "title", "content" },
            ["form"] = new[] { "group", "label", "input", "error", "help" },
            ["nav"] = new[] { "item", "link", "icon", "text", "dropdown" },
            ["list"] = new[] { "item", "header", "content", "footer" },
            ["modal"] = new[] { "header", "body", "footer", "close", "title" },
            ["table"] = new[] { "header", "row", "cell", "footer" },
            ["button"] = new[] { "icon", "text", "badge" }
        };

        private static readonly Dictionary<string, string[]> CommonModifiers = new()
        {
            ["size"] = new[] { "sm", "md", "lg", "xl" },
            ["color"] = new[] { "primary", "secondary", "success", "danger", "warning", "info" },
            ["state"] = new[] { "active", "disabled", "loading", "selected", "expanded" },
            ["layout"] = new[] { "horizontal", "vertical", "compact", "expanded" },
            ["alignment"] = new[] { "left", "center", "right", "top", "bottom" }
        };

        public BEMAnalyzer(ILogger<BEMAnalyzer> logger)
        {
            _logger = logger;
        }

        public async Task<BEMAnalysis> AnalyzeAsync(
            Dictionary<string, object> styles,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug("Starting BEM analysis for {Count} styles", styles.Count);

                // Extract components with BEM parsing
                var components = await ParseComponents(styles, ct);
                _logger.LogDebug("Parsed {Count} BEM components", components.Count);

                // Find relationships between components
                var relationships = await FindRelationships(components, ct);
                _logger.LogDebug("Found {Count} component relationships", relationships.Count);

                // Generate suggestions
                var suggestions = await GenerateSuggestions(components, relationships, ct);
                _logger.LogDebug("Generated {Count} BEM suggestions", suggestions.Count);

                return new BEMAnalysis(components, relationships, suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BEM analysis failed");
                throw;
            }
        }

        private async Task<List<BEMComponent>> ParseComponents(
            Dictionary<string, object> styles,
            CancellationToken ct)
        {
            var components = new List<BEMComponent>();

            foreach (var (name, value) in styles)
            {
                if (ct.IsCancellationRequested) break;
                if (value is not Dictionary<object, object> styleDict) continue;

                // Parse BEM parts
                var (block, element, modifier) = ParseBEMName(name);

                // Track dependencies (other components referenced in styles)
                var dependencies = FindDependencies(styleDict);

                components.Add(new BEMComponent(
                    Name: name,
                    Block: block,
                    Element: element,
                    Modifier: modifier,
                    Styles: styleDict.ToDictionary(
                        k => k.Key.ToString()!,
                        v => v.Value),
                    Dependencies: dependencies
                ));
            }

            return components;
        }

        private (string? Block, string? Element, string? Modifier) ParseBEMName(string name)
        {
            // Handle element with modifier
            if (ElementModifierPattern.IsMatch(name))
            {
                var parts = name.Split(new[] { "__", "--" }, StringSplitOptions.None);
                return (parts[0], parts[1], parts[2]);
            }

            // Handle block with modifier
            if (ModifierPattern.IsMatch(name))
            {
                var parts = name.Split("--");
                return (parts[0], null, parts[1]);
            }

            // Handle element
            if (ElementPattern.IsMatch(name))
            {
                var parts = name.Split("__");
                return (parts[0], parts[1], null);
            }

            // Handle block
            if (BlockPattern.IsMatch(name))
            {
                return (name, null, null);
            }

            return (null, null, null);
        }

        private HashSet<string> FindDependencies(Dictionary<object, object> styles)
        {
            var dependencies = new HashSet<string>();

            // Look for class references in selectors, variables, etc.
            foreach (var value in styles.Values)
            {
                var strValue = value?.ToString() ?? "";
                
                // Check for class references
                if (strValue.Contains('.'))
                {
                    var classRefs = Regex.Matches(strValue, @"\.([a-z][a-z0-9-_]*)")
                        .Select(m => m.Groups[1].Value);
                    dependencies.UnionWith(classRefs);
                }

                // Check for var() references
                if (strValue.Contains("var(--"))
                {
                    var varRefs = Regex.Matches(strValue, @"var\(--([a-z][a-z0-9-]*)\)")
                        .Select(m => m.Groups[1].Value);
                    dependencies.UnionWith(varRefs);
                }
            }

            return dependencies;
        }

        private async Task<List<BEMRelationship>> FindRelationships(
            List<BEMComponent> components,
            CancellationToken ct)
        {
            var relationships = new List<BEMRelationship>();

            foreach (var component in components)
            {
                if (ct.IsCancellationRequested) break;

                // Find parent-child relationships
                if (component.Element != null)
                {
                    var parent = components.FirstOrDefault(c => 
                        c.Block == component.Block && 
                        c.Element == null && 
                        c.Modifier == null);

                    if (parent != null)
                    {
                        relationships.Add(new BEMRelationship(
                            SourceComponent: parent.Name,
                            TargetComponent: component.Name,
                            Type: RelationType.Parent,
                            Confidence: 1.0
                        ));
                    }
                }

                // Find modifier relationships
                if (component.Modifier != null)
                {
                    var baseComponent = components.FirstOrDefault(c =>
                        c.Block == component.Block &&
                        c.Element == component.Element &&
                        c.Modifier == null);

                    if (baseComponent != null)
                    {
                        relationships.Add(new BEMRelationship(
                            SourceComponent: baseComponent.Name,
                            TargetComponent: component.Name,
                            Type: component.Element != null ? 
                                RelationType.ElementModifier : 
                                RelationType.Modifier,
                            Confidence: 1.0
                        ));
                    }
                }

                // Find extension relationships (similar styles but no BEM relationship)
                foreach (var other in components)
                {
                    if (other == component) continue;

                    var similarity = CalculateStyleSimilarity(
                        component.Styles,
                        other.Styles);

                    if (similarity >= 0.7) // High style similarity threshold
                    {
                        relationships.Add(new BEMRelationship(
                            SourceComponent: component.Name,
                            TargetComponent: other.Name,
                            Type: RelationType.Extension,
                            Confidence: similarity
                        ));
                    }
                }

                // Find composition relationships (frequently used together)
                foreach (var dep in component.Dependencies)
                {
                    var target = components.FirstOrDefault(c => c.Name == dep);
                    if (target != null)
                    {
                        relationships.Add(new BEMRelationship(
                            SourceComponent: component.Name,
                            TargetComponent: target.Name,
                            Type: RelationType.Composition,
                            Confidence: 0.8 // Could be refined based on usage analysis
                        ));
                    }
                }
            }

            return relationships;
        }

        private double CalculateStyleSimilarity(
            Dictionary<string, object> styles1,
            Dictionary<string, object> styles2)
        {
            var props1 = new HashSet<string>(styles1.Keys);
            var props2 = new HashSet<string>(styles2.Keys);

            var intersection = props1.Intersect(props2).Count();
            var union = props1.Union(props2).Count();

            return union == 0 ? 0 : (double)intersection / union;
        }

        private async Task<List<BEMSuggestion>> GenerateSuggestions(
            List<BEMComponent> components,
            List<BEMRelationship> relationships,
            CancellationToken ct)
        {
            var suggestions = new List<BEMSuggestion>();

            foreach (var component in components)
            {
                if (ct.IsCancellationRequested) break;

                // Check if this could be an element
                if (component.Element == null && 
                    TryInferElement(component, components, out var suggestedElement))
                {
                    suggestions.Add(new BEMSuggestion(
                        Type: SuggestionType.BEMStructure,
                        Description: "Component appears to be an element of another component",
                        Current: component.Name,
                        Suggested: $"{suggestedElement.Block}__{component.Name}",
                        Confidence: 0.8,
                        IsBlockLevel: false
                    ));
                }

                // Check if this could be a modifier
                if (component.Modifier == null &&
                    TryInferModifier(component, components, out var suggestedModifier))
                {
                    suggestions.Add(new BEMSuggestion(
                        Type: SuggestionType.BEMStructure,
                        Description: "Component appears to be a modifier of another component",
                        Current: component.Name,
                        Suggested: $"{suggestedModifier.Name}--{InferModifierName(component.Name)}",
                        Confidence: 0.8,
                        IsBlockLevel: true
                    ));
                }

                // Suggest common elements
                if (component.Element == null && 
                    component.Modifier == null &&
                    CommonElements.ContainsKey(component.Name))
                {
                    foreach (var element in CommonElements[component.Name])
                    {
                        if (!components.Any(c => 
                            c.Block == component.Name && 
                            c.Element == element))
                        {
                            suggestions.Add(new BEMSuggestion(
                                Type: SuggestionType.CommonPattern,
                                Description: $"Common {component.Name} element missing",
                                Current: "",
                                Suggested: $"{component.Name}__{element}",
                                Confidence: 0.7,
                                IsBlockLevel: false
                            ));
                        }
                    }
                }

                // Suggest common modifiers
                foreach (var (category, modifiers) in CommonModifiers)
                {
                    foreach (var modifier in modifiers)
                    {
                        var hasModifier = components.Any(c =>
                            c.Block == component.Block &&
                            c.Element == component.Element &&
                            c.Modifier == modifier);

                        if (!hasModifier && IsRelevantModifier(category, component.Styles))
                        {
                            suggestions.Add(new BEMSuggestion(
                                Type: SuggestionType.CommonPattern,
                                Description: $"Common {category} modifier missing",
                                Current: component.Name,
                                Suggested: $"{component.Name}--{modifier}",
                                Confidence: 0.6,
                                IsBlockLevel: component.Element == null
                            ));
                        }
                    }
                }

                // Suggest BEM structure improvements
                if (!IsValidBEMName(component.Name))
                {
                    var suggestedName = ImproveComponentName(component.Name);
                    if (suggestedName != component.Name)
                    {
                        suggestions.Add(new BEMSuggestion(
                            Type: SuggestionType.Naming,
                            Description: "Component name could better follow BEM conventions",
                            Current: component.Name,
                            Suggested: suggestedName,
                            Confidence: 0.9,
                            IsBlockLevel: !suggestedName.Contains("__")
                        ));
                    }
                }
            }

            // Look for missing relationships
            foreach (var rel in relationships)
            {
                if (ct.IsCancellationRequested) break;

                var source = components.First(c => c.Name == rel.SourceComponent);
                var target = components.First(c => c.Name == rel.TargetComponent);

                // Suggest converting extension to proper BEM relationship
                if (rel.Type == RelationType.Extension && rel.Confidence > 0.8)
                {
                    suggestions.Add(new BEMSuggestion(
                        Type: SuggestionType.Relationship,
                        Description: "Components share many styles and could be related through BEM",
                        Current: $"{source.Name}, {target.Name}",
                        Suggested: $"Consider making {target.Name} a modifier or element of {source.Name}",
                        Confidence: rel.Confidence,
                        IsBlockLevel: true
                    ));
                }
            }

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .ToList();
        }

        private bool TryInferElement(
            BEMComponent component,
            List<BEMComponent> allComponents,
            out BEMComponent parent)
        {
            // Look for components that this one might be an element of
            var candidates = allComponents
                .Where(c => c != component && 
                       c.Element == null && 
                       c.Modifier == null)
                .ToList();

            foreach (var candidate in candidates)
            {
                // Check if this component's name suggests it's an element
                if (component.Name.StartsWith($"{candidate.Name}-") ||
                    CommonElements.ContainsKey(candidate.Name) &&
                    CommonElements[candidate.Name].Contains(component.Name))
                {
                    parent = candidate;
                    return true;
                }

                // Check style similarity
                var similarity = CalculateStyleSimilarity(
                    component.Styles,
                    candidate.Styles);

                if (similarity >= 0.5)
                {
                    parent = candidate;
                    return true;
                }
            }

            parent = null!;
            return false;
        }

        private bool TryInferModifier(
            BEMComponent component,
            List<BEMComponent> allComponents,
            out BEMComponent baseComponent)
        {
            // Look for components this might be a modifier of
            var candidates = allComponents
                .Where(c => c != component &&
                       c.Block == component.Block &&
                       c.Modifier == null)
                .ToList();

            foreach (var candidate in candidates)
            {
                // Check style similarity
                var similarity = CalculateStyleSimilarity(
                    component.Styles,
                    candidate.Styles);

                if (similarity >= 0.5)
                {
                    baseComponent = candidate;
                    return true;
                }
            }

            baseComponent = null!;
            return false;
        }

        private string InferModifierName(string componentName)
        {
            // Try to extract a meaningful modifier name from the component name
            foreach (var (category, modifiers) in CommonModifiers)
            {
                foreach (var modifier in modifiers)
                {
                    if (componentName.Contains(modifier, StringComparison.OrdinalIgnoreCase))
                    {
                        return modifier;
                    }
                }
            }

            // Fallback: use the last part of the name
            var parts = componentName.Split('-');
            return parts[^1];
        }

        private bool IsValidBEMName(string name)
        {
            return BlockPattern.IsMatch(name) ||
                   ElementPattern.IsMatch(name) ||
                   ModifierPattern.IsMatch(name) ||
                   ElementModifierPattern.IsMatch(name);
        }

        private string ImproveComponentName(string name)
        {
            // Convert camelCase to kebab-case
            if (name.Any(char.IsUpper))
            {
                name = string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x : x.ToString()))
                    .ToLower();
            }

            // Fix common BEM syntax issues
            name = name.Replace("_", "-")              // Use hyphens for word separation
                      .Replace("---", "--")            // Fix triple hyphens
                      .Replace(".", "-")               // Remove dots
                      .Replace("--modifier-", "--")    // Fix verbose modifier names
                      .Replace("--variant-", "--")     // Fix verbose variant names
                      .Replace("__element-", "__");    // Fix verbose element names

            return name;
        }

        private bool IsRelevantModifier(string category, Dictionary<string, object> styles)
        {
            return category switch
            {
                "size" => styles.Keys.Any(k => k.Contains("size") || k.Contains("width") || k.Contains("height")),
                "color" => styles.Keys.Any(k => k.Contains("color") || k.Contains("background")),
                "state" => true, // States are relevant for most components
                "layout" => styles.Keys.Any(k => k.Contains("display") || k.Contains("flex") || k.Contains("grid")),
                "alignment" => styles.Keys.Any(k => k.Contains("align") || k.Contains("justify") || k.Contains("text")),
                _ => false
            };
        }
    }
}