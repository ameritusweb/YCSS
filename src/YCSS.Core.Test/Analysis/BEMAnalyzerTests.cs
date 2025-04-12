using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Patterns;

namespace YCSS.Core.Test.Analysis
{
    [TestClass]
    public class BEMAnalyzerTests
    {
        private BEMAnalyzer _analyzer;
        private Mock<ILogger<BEMAnalyzer>> _logger;

        [TestInitialize]
        public void Setup()
        {
            _logger = new Mock<ILogger<BEMAnalyzer>>();
            _analyzer = new BEMAnalyzer(_logger.Object);
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithValidBEMStructure_DetectsRelationships()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["card"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#ffffff",
                    ["padding"] = "1rem",
                    ["border-radius"] = "4px"
                },
                ["card__header"] = new Dictionary<object, object>
                {
                    ["padding"] = "1rem",
                    ["border-bottom"] = "1px solid #e5e7eb"
                },
                ["card__body"] = new Dictionary<object, object>
                {
                    ["padding"] = "1rem"
                },
                ["card--primary"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["padding"] = "1rem",
                    ["border-radius"] = "4px"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Components.Count);

            // Verify component parsing
            var cardComponent = result.Components.First(c => c.Name == "card");
            Assert.AreEqual("card", cardComponent.Block);
            Assert.IsNull(cardComponent.Element);
            Assert.IsNull(cardComponent.Modifier);

            var headerComponent = result.Components.First(c => c.Name == "card__header");
            Assert.AreEqual("card", headerComponent.Block);
            Assert.AreEqual("header", headerComponent.Element);
            Assert.IsNull(headerComponent.Modifier);

            // Verify relationships
            var parentRelationship = result.Relationships.First(r => 
                r.SourceComponent == "card" && 
                r.TargetComponent == "card__header");
            Assert.AreEqual(RelationType.Parent, parentRelationship.Type);

            var modifierRelationship = result.Relationships.First(r =>
                r.SourceComponent == "card" &&
                r.TargetComponent == "card--primary");
            Assert.AreEqual(RelationType.Modifier, modifierRelationship.Type);
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithNonBEMNames_SuggestsImprovements()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["buttonPrimary"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["padding"] = "1rem"
                },
                ["button_secondary"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#4b5563",
                    ["padding"] = "1rem"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            var namingSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.Naming)
                .ToList();

            Assert.AreEqual(2, namingSuggestions.Count);
            Assert.IsTrue(namingSuggestions.Any(s => s.Current == "buttonPrimary"));
            Assert.IsTrue(namingSuggestions.Any(s => s.Current == "button_secondary"));
            Assert.IsTrue(namingSuggestions.All(s => s.Suggested.Contains("--")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithSimilarStyles_SuggestsRelationships()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["primary-button"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["align-items"] = "center",
                    ["padding"] = "1rem",
                    ["background-color"] = "#1f2937"
                },
                ["secondary-button"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["align-items"] = "center",
                    ["padding"] = "1rem",
                    ["background-color"] = "#4b5563"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            var relationshipSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.Relationship)
                .ToList();

            Assert.IsTrue(relationshipSuggestions.Any());
            Assert.IsTrue(relationshipSuggestions.Any(s =>
                s.Current.Contains("primary-button") &&
                s.Current.Contains("secondary-button")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithPartialComponent_SuggestsCommonElements()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["modal"] = new Dictionary<object, object>
                {
                    ["position"] = "fixed",
                    ["background-color"] = "#ffffff",
                    ["padding"] = "1rem"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            var elementSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.CommonPattern)
                .ToList();

            Assert.IsTrue(elementSuggestions.Any());
            Assert.IsTrue(elementSuggestions.Any(s => s.Suggested.Contains("modal__header")));
            Assert.IsTrue(elementSuggestions.Any(s => s.Suggested.Contains("modal__body")));
            Assert.IsTrue(elementSuggestions.Any(s => s.Suggested.Contains("modal__footer")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithoutModifiers_SuggestsCommonModifiers()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["button"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["padding"] = "1rem"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            var modifierSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.CommonPattern && 
                       s.Suggested.Contains("--"))
                .ToList();

            Assert.IsTrue(modifierSuggestions.Any());
            Assert.IsTrue(modifierSuggestions.Any(s => s.Suggested.Contains("--primary")));
            Assert.IsTrue(modifierSuggestions.Any(s => s.Suggested.Contains("--sm")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithDependencies_DetectsComposition()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["nav-item"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["align-items"] = "center"
                },
                ["nav-item__icon"] = new Dictionary<object, object>
                {
                    ["margin-right"] = "0.5rem",
                    ["color"] = "var(--color-primary)"
                },
                ["nav-item__text"] = new Dictionary<object, object>
                {
                    ["color"] = "inherit",
                    ["font-weight"] = "500"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);

            // Verify elements were detected
            Assert.IsTrue(result.Components.Any(c => 
                c.Block == "nav-item" && 
                c.Element == "icon"));
            Assert.IsTrue(result.Components.Any(c => 
                c.Block == "nav-item" && 
                c.Element == "text"));

            // Verify parent-child relationships
            var parentRelationships = result.Relationships
                .Where(r => r.Type == RelationType.Parent)
                .ToList();

            Assert.AreEqual(2, parentRelationships.Count);
            Assert.IsTrue(parentRelationships.All(r => r.SourceComponent == "nav-item"));
            Assert.IsTrue(parentRelationships.Any(r => r.TargetComponent == "nav-item__icon"));
            Assert.IsTrue(parentRelationships.Any(r => r.TargetComponent == "nav-item__text"));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithComplexStructure_HandlesMultipleLevels()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["form"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["flex-direction"] = "column",
                    ["gap"] = "1rem"
                },
                ["form__group"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["flex-direction"] = "column",
                    ["gap"] = "0.5rem"
                },
                ["form__group--horizontal"] = new Dictionary<object, object>
                {
                    ["flex-direction"] = "row",
                    ["align-items"] = "center"
                },
                ["form__group__label"] = new Dictionary<object, object>
                {
                    ["font-weight"] = "500",
                    ["color"] = "#374151"
                },
                ["form__group__input"] = new Dictionary<object, object>
                {
                    ["padding"] = "0.5rem",
                    ["border"] = "1px solid #d1d5db"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);

            // Verify all components were parsed correctly
            Assert.AreEqual(5, result.Components.Count);

            // Verify block component
            var formComponent = result.Components.First(c => c.Name == "form");
            Assert.AreEqual("form", formComponent.Block);
            Assert.IsNull(formComponent.Element);
            Assert.IsNull(formComponent.Modifier);

            // Verify element components
            var groupComponent = result.Components.First(c => c.Name == "form__group");
            Assert.AreEqual("form", groupComponent.Block);
            Assert.AreEqual("group", groupComponent.Element);
            Assert.IsNull(groupComponent.Modifier);

            // Verify modifier components
            var horizontalGroupComponent = result.Components.First(c => c.Name == "form__group--horizontal");
            Assert.AreEqual("form", horizontalGroupComponent.Block);
            Assert.AreEqual("group", horizontalGroupComponent.Element);
            Assert.AreEqual("horizontal", horizontalGroupComponent.Modifier);

            // Verify nested elements
            var labelComponent = result.Components.First(c => c.Name == "form__group__label");
            Assert.AreEqual("form", labelComponent.Block);
            Assert.AreEqual("group__label", labelComponent.Element);
            Assert.IsNull(labelComponent.Modifier);

            // Verify relationships were detected correctly
            var parentRelationships = result.Relationships
                .Where(r => r.Type == RelationType.Parent)
                .ToList();

            Assert.IsTrue(parentRelationships.Any(r =>
                r.SourceComponent == "form" &&
                r.TargetComponent == "form__group"));

            var modifierRelationships = result.Relationships
                .Where(r => r.Type == RelationType.ElementModifier)
                .ToList();

            Assert.IsTrue(modifierRelationships.Any(r =>
                r.SourceComponent == "form__group" &&
                r.TargetComponent == "form__group--horizontal"));
        }
    }
}