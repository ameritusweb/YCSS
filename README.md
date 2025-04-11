# YCSS (YAML CSS) — Semantic, Pattern-Aware Style Compiler

## Overview
YCSS is a next-generation styling language and compiler built around YAML. It enables semantic, declarative, and intelligent styling through structured YAML definitions, replacing low-level SCSS complexity with composable, analyzable, and scalable visual architecture.

YCSS is more than just a preprocessor — it's a **style compiler** that:
- Parses YAML-based design tokens and component styles
- Detects patterns and style duplication
- Suggests utility classes and CSS variables
- Generates CSS/SCSS output
- Supports theming and component variants

---

## Features

- **Declarative YAML Stylesheets**
- **Token-Driven Design System Support**
- **Pattern Analysis & Clustering**
- **SCSS & CSS Code Generation**
- **Component and Variant Modeling**
- **Theming System (Light/Dark/Custom)**
- **Built-in Suggestions for Optimization**

---

## Why YCSS?

Modern styling ecosystems face several recurring challenges:

- 🔁 **Repetition & Duplication** — Common values and patterns repeat endlessly across CSS files.
- ❓ **Lack of Semantic Context** — Class names and style groupings are often arbitrary, fragile, or legacy-bound.
- 🤖 **No Built-In Analysis** — SCSS and utility-first tools offer no insight into what styles are being overused, under-optimized, or redundant.
- 🧩 **Poor Reusability & Maintainability** — Without structure, design systems drift and grow inconsistent over time.

**YCSS addresses these pain points** by offering a high-level, introspectable, and intelligence-driven approach:

- 📦 **Design Token Native** — Treats your design tokens as first-class citizens. Easily refactor into CSS variables or SCSS maps.
- 🧠 **Pattern-Aware Compiler** — Uses statistical techniques to detect duplicated values, clusters of properties, and high-cohesion design fragments.
- 💡 **Refactoring Insights** — Suggests utility class abstractions, variable extraction, and mixin grouping automatically.
- 🛠️ **Declarative, Not Procedural** — Write what your components are, not how the styles should unfold line by line.
- 🌐 **Framework-Agnostic Output** — Generate CSS, SCSS, or even just token maps — no runtime overhead, no proprietary markup.
- 📊 **Visualization & Auditing** — Export analysis results as graphs or reports to help teams see and improve the health of their style layer.

YCSS is designed for scale, clarity, and optimization — for teams and systems that want style intelligence without sacrificing simplicity.

---

## Getting Started

### Installation
bash
dotnet tool install --global ycss


### Example YAML
yaml
tokens:
  color-primary: "#1f2937"
  radius-md: "0.5rem"

components:
  card:
    base:
      class: card
      styles:
        - background-color: var(--color-primary)
        - border-radius: var(--radius-md)
        - padding: 2rem

    header:
      class: card__header
      styles:
        - font-weight: bold
        - padding: 1rem

    variants:
      compact:
        class: card--compact
        styles:
          - padding: 1rem


---

## CLI Commands

### Build
Generate CSS or SCSS output from YAML:
bash
ycss build design.yaml --out dist/styles.css


Optional flags:
- --format css (default) - Outputs standard CSS
- --format scss - Outputs SCSS with $tokens and nested syntax
- --theme dark - Applies theme overrides if defined in the YAML

Example:
bash
ycss build design.yaml --format scss --theme dark --out dist/dark-theme.scss


### Analyze
Generate optimization suggestions (utility classes, tokens, property clusters):
bash
ycss analyze design.yaml --report report.md


Optional flags:
- --verbose - Include raw pattern data
- --min-cohesion 0.6 - Set cohesion threshold

### Tokens Only
Output only the :root CSS variable definitions:
bash
ycss tokens design.yaml --out dist/tokens.css


Optional:
- --format scss to output as $variables

### Visualize
Generate a property correlation graph or cluster diagram (Graphviz-compatible):
bash
ycss visualize design.yaml --out dist/graph.dot


Optional flags:
- --format png|svg|dot
- --depth 3 - Controls cluster recursion

---

## How the Compiler Works

YCSS operates in multiple stages to transform declarative YAML into optimized CSS or SCSS:

### 1. Parsing Stage
- Reads YAML and maps tokens and components into internal models.
- Flattens nested structures and normalizes shorthand notations.

### 2. Token Resolution
- Builds a global token table.
- Resolves var(--token-name) references.
- Supports scoped or themed overrides.

### 3. Pattern Detection

#### Example Analysis Output
Here are some real-world examples of what YCSS might report during analysis:

**Example 1 — Utility Class Suggestion:**
Suggestion: Create utility class `.util-padding-lg`
Detected in:
- .card
- .card__body
- .modal
Shared properties:
  padding: 2rem;

Rationale: Padding 2rem appears in 6 different components. Use a shared utility class to reduce duplication.


**Example 2 — CSS Variable Recommendation:**
Suggestion: Extract CSS variable --border-radius-sm
Detected in:
- .button, .input, .badge
Shared value: border-radius: 4px;
Usage count: 9
 

**Example 3 — High-Cohesion Property Group:**
Pattern Cluster Detected: layout-flex-row
Properties:
- display: flex
- flex-direction: row
- align-items: center
- gap: 1rem
Frequency: 11 components
Cohesion Score: 0.92

Suggestion: Abstract into mixin or reusable component block.


**Example 4 — Outlier Detection:**
Anomaly: padding: 1.125rem detected only once in .nav-link
Recommendation: Normalize to token-based spacing (e.g. --spacing-md = 1rem or --spacing-lg = 1.5rem)


**Example 5 — Correlation Insight:**
Strong Property Correlation:
  color + font-weight appear together in 89% of typographic components.
Consider grouping into a text-style token.


- Executes statistical analysis on the style rules to uncover latent structure and redundancy. This includes:
  - **Property-Value Frequency Analysis**: Identifies high-frequency values that may warrant tokenization or variable extraction.
  - **Pairwise Property Correlation (Jaccard Index)**: Measures the co-occurrence of style properties across components to assess how frequently properties appear together.
  - **Mutual Information Estimation**: Quantifies the reduction in uncertainty about one property given knowledge of another. High mutual information indicates potential semantic grouping.
  - **Chi-Square Test of Independence**: Evaluates whether the presence of one property is statistically dependent on another. A high chi-square score flags a non-random association, useful for discovering class-level design patterns.
  - **Cluster Analysis**: Groups properties into hierarchies based on similarity metrics (cohesion, correlation) to suggest reusable style abstractions or mixins.
  - **Value Distribution Modeling**: Constructs histograms for numeric values (e.g., margin, padding, border-radius) to detect outliers or non-standardized scales.
- Generates actionable suggestions for:
  - Utility class abstraction
  - CSS variable introduction
  - Semantic cluster naming and reuse
- Emits CSS/SCSS based on:
  - Selected output format (--format css|scss)
  - Token usage and theme layering
  - SCSS-specific syntax (e.g., $variables and mixins)
- Supports minified or pretty-printed output.

### 5. Visualization (Optional)
- Generates data structures representing property correlation graphs.
- Outputs DOT or SVG files for static or interactive visual analysis.

---

## Output Example
css
:root {
  --color-primary: #1f2937;
  --radius-md: 0.5rem;
}

.card {
  background-color: var(--color-primary);
  border-radius: var(--radius-md);
  padding: 2rem;
}

.card__header {
  font-weight: bold;
  padding: 1rem;
}

.card--compact {
  padding: 1rem;
}


---

## Comparison with Other Tools

| Feature / Tool         | YCSS                      | SCSS/SASS                  | Tailwind CSS              | CSS-in-JS (e.g., Emotion)     | Stylelint + Tokens Studio |
|------------------------|---------------------------|----------------------------|---------------------------|-------------------------------|----------------------------|
| **Source Format**     | YAML                      | SCSS                       | Utility-first CSS         | JavaScript                   | CSS / JSON / YAML          |
| **Tokens Support**    | Native (YAML-based)       | Partial (manual vars)      | Requires plugin           | Manual                       | Native (via plugin)        |
| **Pattern Detection** | ✅ Statistical, clustered  | ❌                         | ❌                        | ❌                            | ✅ (Lint rules only)        |
| **Theme System**      | Multi-theme via YAML      | Manual theme logic         | Manual via @apply       | Manual via props             | Manual token switching     |
| **Output Types**      | CSS / SCSS / Tokens       | CSS                        | CSS                       | Injected CSS                 | N/A                        |
| **Analysis CLI**      | ✅ Built-in                | ❌                         | ❌                        | ❌                            | ✅                          |
| **Visualization**     | ✅ Graph/Cluster export    | ❌                         | ❌                        | ❌                            | Limited                    |
| **Developer Ergonomics** | High (semantic YAML)    | Medium (expressive syntax) | Medium (opinionated)      | Low (JS coupling)            | Medium                     |

---

## Roadmap
- [x] YAML Style Definitions
- [x] Token-Driven Output
- [x] Pattern Analysis & Suggestion Engine
- [ ] IDE/Editor Plugins (VSCode, JetBrains)
- [ ] Web-based Visual Editor
- [ ] Integration with Blazor / React / Svelte

---

## Contributing
We welcome contributions! Help us build:
- More YAML syntax sugar
- Theme layering strategies
- Advanced suggestion logic
- SCSS mixin generation
- Integration plugins for popular frameworks

---

## License
MIT

---

## Author & Vision
YCSS is created by engineers who lived through the complexity of SCSS, the memory leaks of Silverlight, and the verbosity of CSS-in-JS. It's built to bring **clarity, performance, and semantic structure** to modern UI development.

> YCSS is not just a preprocessor. It's a compiler for the future of design systems.
