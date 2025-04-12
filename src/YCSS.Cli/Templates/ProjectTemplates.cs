using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Cli.Templates
{
    public static class ProjectTemplates
    {
        public static readonly Dictionary<string, string> Basic = new()
        {
            ["ycss.config.yaml"] = """
            output:
              format: css
              minify: false
              sourceMaps: true
            
            paths:
              tokens: styles/tokens.yaml
              components: styles/components/**/*.yaml
              output: dist/styles.css
            """,

            ["styles/tokens.yaml"] = """
            # Design Tokens
            colors:
              primary: "#1f2937"
              secondary: "#4b5563"
              accent: "#3b82f6"
              background: "#ffffff"
              text: "#111827"
            
            spacing:
              xs: "0.25rem"
              sm: "0.5rem"
              md: "1rem"
              lg: "1.5rem"
              xl: "2rem"
            
            typography:
              fontFamily:
                sans: "ui-sans-serif, system-ui, -apple-system"
                mono: "ui-monospace, monospace"
              fontSize:
                sm: "0.875rem"
                base: "1rem"
                lg: "1.125rem"
                xl: "1.25rem"
            
            radii:
              sm: "0.25rem"
              md: "0.375rem"
              lg: "0.5rem"
              full: "9999px"
            """,

            ["styles/components/button.yaml"] = """
            button:
              base:
                background-color: var(--colors-primary)
                color: white
                padding: var(--spacing-sm) var(--spacing-md)
                border-radius: var(--radii-md)
                font-family: var(--typography-fontFamily-sans)
                font-size: var(--typography-fontSize-base)
                transition: background-color 0.2s
              
              variants:
                secondary:
                  background-color: var(--colors-secondary)
                
                large:
                  padding: var(--spacing-md) var(--spacing-lg)
                  font-size: var(--typography-fontSize-lg)
            """
        };

        public static readonly Dictionary<string, string> ComponentLibrary = new(Basic)
        {
            ["README.md"] = """
            # Component Library
            
            This is a YCSS component library project.
            
            ## Getting Started
            
            1. Edit `styles/tokens.yaml` to define your design tokens
            2. Add components in `styles/components/`
            3. Run `ycss build` to compile
            
            ## Structure
            
            ```
            .
            ├── styles/
            │   ├── tokens.yaml      # Design tokens
            │   └── components/      # Component styles
            ├── dist/                # Compiled output
            └── ycss.config.yaml     # Configuration
            ```
            """,

            ["styles/components/card.yaml"] = """
            card:
              base:
                background-color: white
                border-radius: var(--radii-lg)
                box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1)
                overflow: hidden
              
              header:
                padding: var(--spacing-md)
                border-bottom: 1px solid var(--colors-secondary)
                font-weight: bold
              
              body:
                padding: var(--spacing-md)
              
              footer:
                padding: var(--spacing-md)
                border-top: 1px solid var(--colors-secondary)
            """,

            ["styles/components/input.yaml"] = """
            input:
              base:
                border: 1px solid var(--colors-secondary)
                border-radius: var(--radii-md)
                padding: var(--spacing-sm) var(--spacing-md)
                font-family: var(--typography-fontFamily-sans)
                font-size: var(--typography-fontSize-base)
                
              variants:
                error:
                  border-color: red
                  
                large:
                  padding: var(--spacing-md)
                  font-size: var(--typography-fontSize-lg)
            """
        };

        public static readonly Dictionary<string, string> DesignSystem = new(ComponentLibrary)
        {
            ["styles/themes/dark.yaml"] = """
            colors:
              primary: "#93c5fd"
              secondary: "#9ca3af"
              background: "#1f2937"
              text: "#f3f4f6"
            """,

            ["styles/themes/light.yaml"] = """
            colors:
              primary: "#3b82f6"
              secondary: "#4b5563"
              background: "#ffffff"
              text: "#111827"
            """,

            ["styles/utilities.yaml"] = """
            spacing:
              - property: margin
                values: [xs, sm, md, lg, xl]
              - property: padding
                values: [xs, sm, md, lg, xl]
            
            colors:
              - property: color
                values: [primary, secondary, text]
              - property: background-color
                values: [primary, secondary, background]
            
            typography:
              - property: font-size
                values: [sm, base, lg, xl]
            """
        };

        public static string GitIgnore = """
        # Build output
        dist/
        
        # Node modules
        node_modules/
        
        # Editor files
        .vscode/
        .idea/
        *.swp
        
        # OS files
        .DS_Store
        Thumbs.db
        """;
    }
}
